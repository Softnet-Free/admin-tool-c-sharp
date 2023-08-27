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
using System.Security.Cryptography;

namespace AdminTool
{
    /// <summary>
    /// Interaction logic for ChangePassword.xaml
    /// </summary>
    public partial class ChangeAdminPassword : UserControl, Closable
    {
        long m_ownerId;
        AdminAccounts m_adminAccounts;
        Action<Closable> m_onCompletedCallback;

        public ChangeAdminPassword(long ownerId, AdminAccounts adminAccounts, Action<Closable> onCompletedCallback)
        {
            InitializeComponent();

            m_ownerId = ownerId;
            m_adminAccounts = adminAccounts;
            m_onCompletedCallback = onCompletedCallback;
            ThreadPool.QueueUserWorkItem(delegate { load(); });
        }

        bool m_closed = false;
        public bool closed { get { return m_closed; } }
        public void close()
        {
            m_closed = true;
        }

        void load()
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
                    command.CommandText = "Softnet_AdminTool_GetAccountProps";

                    command.Parameters.Add("@OwnerId", SqlDbType.BigInt);
                    command.Parameters["@OwnerId"].Direction = ParameterDirection.Input;
                    command.Parameters["@OwnerId"].Value = m_ownerId;

                    command.Parameters.Add("@AccountName", SqlDbType.NVarChar, 256);
                    command.Parameters["@AccountName"].Direction = ParameterDirection.Output;

                    command.Parameters.Add("@UserFullName", SqlDbType.NVarChar, 256);
                    command.Parameters["@UserFullName"].Direction = ParameterDirection.Output;

                    command.Parameters.Add("@ReturnValue", SqlDbType.Int);
                    command.Parameters["@ReturnValue"].Direction = ParameterDirection.ReturnValue;

                    command.ExecuteNonQuery();

                    int returnCode = (int)command.Parameters["@ReturnValue"].Value;                    
                    if (returnCode == 0)
                    {
                        string userName = (string)command.Parameters["@UserFullName"].Value;
                        string accountName = (string)command.Parameters["@AccountName"].Value;

                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                        {
                            TextBlock_UserName.Text = userName;
                            TextBlock_AccountName.Text = accountName;
                            Button_Change.Visibility = System.Windows.Visibility.Visible;
                        }));
                    }
                    else if (returnCode == 1)
                    {
                        OnError("The account not found.");
                    }                    
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

        void execute(string password_str)
        {
            try
            {
                Random rnd = new Random();
                byte[] salt = new byte[16];
                rnd.NextBytes(salt);

                byte[] password = Encoding.Unicode.GetBytes(password_str);
                byte[] salt_and_password = new byte[password.Length + salt.Length];
                Buffer.BlockCopy(salt, 0, salt_and_password, 0, salt.Length);
                Buffer.BlockCopy(password, 0, salt_and_password, salt.Length, password.Length);

                SHA1CryptoServiceProvider sha1CSP = new SHA1CryptoServiceProvider();
                byte[] salted_password = sha1CSP.ComputeHash(salt_and_password);

                string salt_b64 = Convert.ToBase64String(salt);
                string salted_password_b64 = Convert.ToBase64String(salted_password);

                Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "Softnet_AdminTool_ChangePassword";

                    command.Parameters.Add("@OwnerId", SqlDbType.BigInt);
                    command.Parameters["@OwnerId"].Direction = ParameterDirection.Input;
                    command.Parameters["@OwnerId"].Value = m_ownerId;

                    command.Parameters.Add("@SaltedPassword", SqlDbType.NVarChar, 128);
                    command.Parameters["@SaltedPassword"].Direction = ParameterDirection.Input;
                    command.Parameters["@SaltedPassword"].Value = salted_password_b64;

                    command.Parameters.Add("@Salt", SqlDbType.NVarChar, 128);
                    command.Parameters["@Salt"].Direction = ParameterDirection.Input;
                    command.Parameters["@Salt"].Value = salt_b64;

                    command.Parameters.Add("@ReturnValue", SqlDbType.Int);
                    command.Parameters["@ReturnValue"].Direction = ParameterDirection.ReturnValue;

                    command.ExecuteNonQuery();

                    int returnCode = (int)command.Parameters["@ReturnValue"].Value;
                    if (returnCode == 0)
                    {                        
                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                        {
                            m_onCompletedCallback(this);
                        }));
                    }
                    else if (returnCode == 1)
                    {
                        OnError("The account not found.");
                    }
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

        void Change_Click(object sender, RoutedEventArgs e)
        {
            string password = TextBox_Password.Password;
            if (password.Length < 10 || password.Length > 64)
            {
                TextBlock_AppMessage.Text = "The length of the password must be in the range [10 - 64].";
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate { execute(password); });
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            m_onCompletedCallback(this);
        }

        void OnError(string message)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                m_adminAccounts.OnError(this, message);
            }));
        }
    }
}
