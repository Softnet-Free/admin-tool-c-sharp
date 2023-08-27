/*
*   Copyright 2023 Robert Koifman
*   
*   Licensed under the Apache License, Version 2.0 (the "License");
*   you may not use this file except in compliance with the License.
*   You may obtain a copy of the License at
*
*   http://www.apache.org/licenses/LICENSE-2.0
*
*   Unless required by applicable law or agreed to in writing, software
*   distributed under the License is distributed on an "AS IS" BASIS,
*   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*   See the License for the specific language governing permissions and
*   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;

namespace AdminTool
{
    /// <summary>
    /// Interaction logic for Search.xaml
    /// </summary>
    public partial class Search : UserControl, Closable
    {
        MainWindow m_mainWindow;
        Action<Closable> m_onComletedCallback;

        public Search(MainWindow mainWindow, Action<Closable> onComletedCallback)
        {
            InitializeComponent();
            m_mainWindow = mainWindow;
            m_onComletedCallback = onComletedCallback;
        }

        bool m_closed = false;
        public bool closed { get { return m_closed; } }
        public void close()
        {
            m_closed = true;
        }

        public void OnError(Closable source, string message)
        {
            if (source.closed)
                return;
            source.close();
            m_mainWindow.OnError(this, message);
        }

        string m_filter;
        long m_firstId = 0;
        long m_lastId = 0;
        bool m_operation_is_pending = false;

        void FindUsers_Click(object sender, EventArgs e)
        {
            if (m_operation_is_pending)
                return;

            m_filter = TextBox_Filter.Text;
            if (m_filter.Length == 0)
                m_filter = "%";
            else
            {
                string[] filterParts = m_filter.Split(' ');
                m_filter = "%";
                for (int i = 0; i < filterParts.Length; i++)
                    m_filter = m_filter + filterParts[i] + "%";
            }

            ListBox_Users.Items.Clear();

            Button_ChangePassword.Visibility = System.Windows.Visibility.Hidden;
            Button_AddToAdmins.Visibility = System.Windows.Visibility.Hidden;

            Button_FindNext.Visibility = System.Windows.Visibility.Hidden;
            Button_FindPrev.Visibility = System.Windows.Visibility.Hidden;

            //m_last_command = 0;
            m_firstId = 0;
            m_lastId = 0;
            m_operation_is_pending = true;

            ThreadPool.QueueUserWorkItem(delegate { findUsers(); });
        }

        void FindNextUsers_Click(object sender, EventArgs e)
        {
            if (m_operation_is_pending)
                return;

            ListBox_Users.Items.Clear();

            Button_ChangePassword.Visibility = System.Windows.Visibility.Hidden;
            Button_AddToAdmins.Visibility = System.Windows.Visibility.Hidden;

            m_operation_is_pending = true;
            ThreadPool.QueueUserWorkItem(delegate { findNextUsers(); });
        }

        void FindPrevUsers_Click(object sender, EventArgs e)
        {
            if (m_operation_is_pending)
                return;

            ListBox_Users.Items.Clear();

            Button_ChangePassword.Visibility = System.Windows.Visibility.Hidden;
            Button_AddToAdmins.Visibility = System.Windows.Visibility.Hidden;

            m_operation_is_pending = true;
            ThreadPool.QueueUserWorkItem(delegate { findPrevUsers(); });
        }

        void ListItem_Selected(object sender, RoutedEventArgs e)
        {
            Button_ChangePassword.Visibility = System.Windows.Visibility.Visible;
            Button_AddToAdmins.Visibility = System.Windows.Visibility.Visible;
        }  

        void ChangePassword_Click(object sender, EventArgs e)
        {
            if (ListBox_Users.SelectedItem == null)
                return;
            ListBoxItem item = (ListBoxItem)ListBox_Users.SelectedItem;
            long ownerId = (long)item.Tag;

            if (x_Content.Content != null)
                ((Closable)x_Content.Content).close();

            ListBox_Users.SelectedItem = null;
            Button_ChangePassword.Visibility = System.Windows.Visibility.Collapsed;
            Button_AddToAdmins.Visibility = System.Windows.Visibility.Collapsed;

            Grid_Search.Visibility = System.Windows.Visibility.Collapsed;
            x_Content.Visibility = System.Windows.Visibility.Visible;
            x_Content.Content = new ChangeUserPassword(ownerId, this, ChangePassword_OnCompleted);
        }

        void AddToAdmins_Click(object sender, EventArgs e)
        {
            if (ListBox_Users.SelectedItem == null)
                return;
            ListBoxItem item = (ListBoxItem)ListBox_Users.SelectedItem;
            long ownerId = (long)item.Tag;
            m_operation_is_pending = true;
            ThreadPool.QueueUserWorkItem(delegate { addToAdmins(ownerId); });
        }

        void findUsers()
        {
            try
            {
                Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "Softnet_AdminTool_FindUsers";

                    command.Parameters.Add("@Filter", SqlDbType.NVarChar, 256);
                    command.Parameters["@Filter"].Direction = ParameterDirection.Input;
                    command.Parameters["@Filter"].Value = m_filter;

                    SqlDataReader dataReader = command.ExecuteReader();
                    if (dataReader.FieldCount == 0)
                    {
                        dataReader.Close();
                        OnError("The specified database does not contain required objects");
                        return;
                    }

                    List<UserAccount> userAccounts = new List<UserAccount>();
                    while (dataReader.Read())
                    {
                        UserAccount userAccount = new UserAccount();
                        userAccount.ownerId = (long)dataReader[0];
                        userAccount.fullName = (string)dataReader[1];
                        userAccount.userName = (string)dataReader[2];
                        userAccounts.Add(userAccount);
                    }
                    dataReader.Close();

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        foreach (UserAccount userAccount in userAccounts)
                        {
                            ListBoxItem item = new ListBoxItem();
                            item.Cursor = Cursors.Hand;
                            item.Selected += new RoutedEventHandler(ListItem_Selected);
                            Label accountName = new Label();
                            accountName.FontSize = 14;
                            accountName.Content = string.Format("{0}  ({1})", userAccount.fullName, userAccount.userName);
                            item.Content = accountName;
                            item.Tag = userAccount.ownerId;
                            ListBox_Users.Items.Add(item);
                        }

                        if (userAccounts.Count > 0)
                        {
                            Button_FindNext.Visibility = System.Windows.Visibility.Visible;
                            Button_FindPrev.Visibility = System.Windows.Visibility.Visible;

                            m_firstId = userAccounts[0].ownerId;
                            m_lastId = userAccounts[userAccounts.Count - 1].ownerId;
                        }

                        m_operation_is_pending = false;
                    }));
                }
            }
            catch (SqlException ex)
            {
                OnError(ex.Message);
            }
            catch (ConfigurationErrorsException ex)
            {
                OnError(ex.Message);
            }
        }

        void findNextUsers()
        {
            try
            {
                Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "Softnet_AdminTool_FindNextUsers";

                    command.Parameters.Add("@Filter", SqlDbType.NVarChar, 256);
                    command.Parameters["@Filter"].Direction = ParameterDirection.Input;
                    command.Parameters["@Filter"].Value = m_filter;

                    command.Parameters.Add("@OwnerId", SqlDbType.BigInt);
                    command.Parameters["@OwnerId"].Direction = ParameterDirection.Input;
                    command.Parameters["@OwnerId"].Value = m_lastId;

                    SqlDataReader dataReader = command.ExecuteReader();
                    if (dataReader.FieldCount == 0)
                    {
                        dataReader.Close();
                        OnError("The specified database does not contain required objects");
                        return;
                    }

                    List<UserAccount> userAccounts = new List<UserAccount>();
                    while (dataReader.Read())
                    {
                        UserAccount userAccount = new UserAccount();
                        userAccount.ownerId = (long)dataReader[0];
                        userAccount.fullName = (string)dataReader[1];
                        userAccount.userName = (string)dataReader[2];
                        userAccounts.Add(userAccount);
                    }
                    dataReader.Close();

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        foreach (UserAccount userAccount in userAccounts)
                        {
                            ListBoxItem item = new ListBoxItem();
                            item.Cursor = Cursors.Hand;
                            item.Selected += new RoutedEventHandler(ListItem_Selected);
                            Label accountName = new Label();
                            accountName.FontSize = 14;
                            accountName.Content = string.Format("{0}  ({1})", userAccount.fullName, userAccount.userName);
                            item.Content = accountName;
                            item.Tag = userAccount.ownerId;
                            ListBox_Users.Items.Add(item);
                        }

                        if (userAccounts.Count > 0)
                        {
                            m_firstId = userAccounts[0].ownerId;
                            m_lastId = userAccounts[userAccounts.Count - 1].ownerId;
                        }
                        else
                        {
                            m_firstId = m_lastId + 1;
                        }

                        m_operation_is_pending = false;
                    }));
                }
            }
            catch (SqlException ex)
            {
                OnError(ex.Message);
            }
            catch (ConfigurationErrorsException ex)
            {
                OnError(ex.Message);
            }
        }

        void findPrevUsers()
        {
            try
            {
                Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "Softnet_AdminTool_FindPrevUsers";

                    command.Parameters.Add("@Filter", SqlDbType.NVarChar, 256);
                    command.Parameters["@Filter"].Direction = ParameterDirection.Input;
                    command.Parameters["@Filter"].Value = m_filter;

                    command.Parameters.Add("@OwnerId", SqlDbType.BigInt);
                    command.Parameters["@OwnerId"].Direction = ParameterDirection.Input;
                    command.Parameters["@OwnerId"].Value = m_firstId;

                    SqlDataReader dataReader = command.ExecuteReader();
                    if (dataReader.FieldCount == 0)
                    {
                        dataReader.Close();
                        OnError("The specified database does not contain required objects");
                        return;
                    }

                    List<UserAccount> userAccounts = new List<UserAccount>();
                    while (dataReader.Read())
                    {
                        UserAccount userAccount = new UserAccount();
                        userAccount.ownerId = (long)dataReader[0];
                        userAccount.fullName = (string)dataReader[1];
                        userAccount.userName = (string)dataReader[2];
                        userAccounts.Add(userAccount);
                    }
                    dataReader.Close();

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        for (int i = userAccounts.Count-1; i >=0; i--)
                        {
                            UserAccount userAccount = userAccounts[i];
                            ListBoxItem item = new ListBoxItem();
                            item.Cursor = Cursors.Hand;
                            item.Selected += new RoutedEventHandler(ListItem_Selected);
                            Label accountName = new Label();
                            accountName.FontSize = 14;
                            accountName.Content = string.Format("{0}  ({1})", userAccount.fullName, userAccount.userName);
                            item.Content = accountName;
                            item.Tag = userAccount.ownerId;
                            ListBox_Users.Items.Add(item);
                        }

                        if (userAccounts.Count > 0)
                        {
                            m_lastId = userAccounts[0].ownerId;
                            m_firstId = userAccounts[userAccounts.Count - 1].ownerId;
                        }
                        else
                        {
                            m_lastId = m_firstId - 1;
                        }

                        m_operation_is_pending = false;
                    }));
                }
            }
            catch (SqlException ex)
            {
                OnError(ex.Message);
            }
            catch (ConfigurationErrorsException ex)
            {
                OnError(ex.Message);
            }
        }

        void addToAdmins(long ownerId)
        {
            try
            {
                Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "Softnet_AdminTool_AddToAdmins";

                    command.Parameters.Add("@OwnerId", SqlDbType.BigInt);
                    command.Parameters["@OwnerId"].Direction = ParameterDirection.Input;
                    command.Parameters["@OwnerId"].Value = ownerId;

                    command.Parameters.Add("@ReturnValue", SqlDbType.Int);
                    command.Parameters["@ReturnValue"].Direction = ParameterDirection.ReturnValue;

                    command.ExecuteNonQuery();
                    int resultCode = (int)command.Parameters["@ReturnValue"].Value;   

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {                        
                        m_operation_is_pending = false;
                        if (resultCode == 0)
                        {
                            if (ListBox_Users.SelectedIndex >= 0)
                            {
                                ListBox_Users.Items.RemoveAt(ListBox_Users.SelectedIndex);
                            }
                            else if (resultCode == -1)
                            {
                                OnError("The database does not contain required membership objects.");
                            }
                        }
                    }));
                }
            }
            catch (SqlException ex)
            {
                OnError(ex.Message);
            }
            catch (ConfigurationErrorsException ex)
            {
                OnError(ex.Message);
            }
        }

        void ChangePassword_OnCompleted(Closable source)
        {
            if (source.closed)
                return;
            source.close();
            Grid_Search.Visibility = System.Windows.Visibility.Visible;
            x_Content.Visibility = System.Windows.Visibility.Collapsed;
            x_Content.Content = null;
        }

        void OnError(string message)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                m_mainWindow.OnError(this, message);
            }));
        }

        class UserAccount
        {
            public long ownerId;
            public string fullName;
            public string userName;
        }
    }
}
