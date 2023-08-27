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
    /// Interaction logic for AdminList.xaml
    /// </summary>
    public partial class AdminList : UserControl, Closable
    {
        AdminAccounts m_adminAccounts;
        Action m_onNewAccountCallback;
        Action<long> m_onChangePasswordCallback;
        Action<long> m_onRemoveFromAdminsCallback;

        public AdminList(AdminAccounts adminAccounts, Action onNewAccountCallback, Action<long> onChangePasswordCallback, Action<long> onRemoveFromAdminsCallback)
        {
            InitializeComponent();

            m_adminAccounts = adminAccounts;
            m_onNewAccountCallback = onNewAccountCallback;
            m_onChangePasswordCallback = onChangePasswordCallback;
            m_onRemoveFromAdminsCallback = onRemoveFromAdminsCallback;
            ThreadPool.QueueUserWorkItem(delegate { Load(); });
        }

        bool m_closed = false;
        public bool closed { get { return m_closed; } }
        public void close()
        {
            m_closed = true;
        }

        void Load()
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
                    command.CommandText = "Softnet_AdminTool_GetAdminAccounts";

                    SqlDataReader dataReader = command.ExecuteReader();
                    if (dataReader.FieldCount == 0)
                    {
                        dataReader.Close();
                        OnError("The specified database does not contain required objects");
                        return;
                    }
                    
                    List<AdminAccount> adminAccounts = new List<AdminAccount>();
                    while (dataReader.Read())
                    {
                        AdminAccount adminAccount = new AdminAccount();
                        adminAccount.ownerId = (long)dataReader[0];
                        adminAccount.fullName = (string)dataReader[1];
                        adminAccount.userName = (string)dataReader[2];
                        adminAccounts.Add(adminAccount);
                    }
                    dataReader.Close();

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        foreach (AdminAccount adminAccount in adminAccounts)
                        { 
                            ListBoxItem item = new ListBoxItem();
                            item.Cursor = Cursors.Hand;                            
                            item.Selected += new RoutedEventHandler(ListItem_Selected);
                            Label accountName = new Label();
                            accountName.FontSize = 14;
                            accountName.Content = string.Format("{0}  ({1})", adminAccount.fullName, adminAccount.userName);
                            item.Content = accountName;
                            item.Tag = adminAccount.ownerId;
                            ListBox_Admins.Items.Add(item);
                        }
                        Button_NewAccount.Visibility = System.Windows.Visibility.Visible;
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

        void ListItem_Selected(object sender, RoutedEventArgs e)
        {
            Button_ChangePassword.Visibility = System.Windows.Visibility.Visible;
            Button_RevokeAdmin.Visibility = System.Windows.Visibility.Visible;
        }        

        void ChangePassword_Click(object sender, EventArgs e)
        {            
            if (ListBox_Admins.SelectedItem == null)
                return;
            ListBoxItem item = (ListBoxItem)ListBox_Admins.SelectedItem;
            long ownerId = (long)item.Tag;
            m_onChangePasswordCallback(ownerId);
        }

        void RevokeAdmin_Click(object sender, EventArgs e)
        {
            if (ListBox_Admins.SelectedItem == null)
                return;
            ListBoxItem item = (ListBoxItem)ListBox_Admins.SelectedItem;
            long ownerId = (long)item.Tag;
            m_onRemoveFromAdminsCallback(ownerId);
        }

        void NewAccount_Click(object sender, EventArgs e)
        {
            m_onNewAccountCallback();
        }

        void OnError(string message)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                m_adminAccounts.OnError(this, message);
            }));
        }

        class AdminAccount
        {
            public long ownerId;
            public string fullName;
            public string userName;
        }
    }
}
