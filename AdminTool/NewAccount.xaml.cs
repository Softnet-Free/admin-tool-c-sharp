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
    /// Interaction logic for NewAccount.xaml
    /// </summary>
    public partial class NewAccount : UserControl, Closable
    {
        AdminAccounts m_adminAccounts;
        Action<Closable> m_onCompletedCallback;

        public NewAccount(AdminAccounts adminAccounts, Action<Closable> onCompletedCallback)
        {
            InitializeComponent();
            m_adminAccounts = adminAccounts;
            m_onCompletedCallback = onCompletedCallback;
        }

        bool m_closed = false;
        public bool closed { get { return m_closed; } }
        public void close()
        {
            m_closed = true;
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            m_onCompletedCallback(this);
        }

        void Create_Click(object sender, RoutedEventArgs e)
        {
            string accountName = TextBox_AccountName.Text.Trim();
            string password = TextBox_Password.Password;
            string fullName = TextBox_FullName.Text.Trim();
            string email = TextBox_Email.Text.Trim();

            TextBlock_AppMessage.Text = "";
            if (accountName.Length < 4 || accountName.Length > 64)
            {
                TextBlock_AppMessage.Text = "The length of the account name must be in the range [4 - 64].";
                return;
            }

            if (password.Length < 10 || password.Length > 64)
            {
                TextBlock_AppMessage.Text = "The length of the password must be in the range [10 - 64].";
                return;
            }

            if (fullName.Length < 2 || fullName.Length > 256)
            {
                TextBlock_AppMessage.Text = "The length of the user name must be in the range [2 - 256].";
                return;
            }

            if (email.Length < 5 || email.Length > 256)
            {
                TextBlock_AppMessage.Text = "The length of the email must be in the range [5 - 256].";
                return;
            }

            if (!EMailValidator.IsValid(email))
            {
                TextBlock_AppMessage.Text = "The email address is not in a valid format.";
                return;
            }            

            ThreadPool.QueueUserWorkItem(delegate { Execute(accountName, password, fullName, email); });
        }

        void Execute(string accountName, string password_str, string fullName, string email)
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
                    command.CommandText = "Softnet_AdminTool_CreateAdminAccount";

                    command.Parameters.Add("@AccountName", SqlDbType.NVarChar, 256);
                    command.Parameters["@AccountName"].Direction = ParameterDirection.Input;
                    command.Parameters["@AccountName"].Value = accountName;

                    command.Parameters.Add("@SaltedPassword", SqlDbType.NVarChar, 128);
                    command.Parameters["@SaltedPassword"].Direction = ParameterDirection.Input;
                    command.Parameters["@SaltedPassword"].Value = salted_password_b64;

                    command.Parameters.Add("@Salt", SqlDbType.NVarChar, 128);
                    command.Parameters["@Salt"].Direction = ParameterDirection.Input;
                    command.Parameters["@Salt"].Value = salt_b64;

                    command.Parameters.Add("@UserFullName", SqlDbType.NVarChar, 256);
                    command.Parameters["@UserFullName"].Direction = ParameterDirection.Input;
                    command.Parameters["@UserFullName"].Value = fullName;

                    command.Parameters.Add("@Email", SqlDbType.NVarChar, 256);
                    command.Parameters["@Email"].Direction = ParameterDirection.Input;
                    command.Parameters["@Email"].Value = email;

                    command.Parameters.Add("@LoweredEmail", SqlDbType.NVarChar, 256);
                    command.Parameters["@LoweredEmail"].Direction = ParameterDirection.Input;
                    command.Parameters["@LoweredEmail"].Value = email.ToLower();

                    command.Parameters.Add("@ReturnValue", SqlDbType.Int);
                    command.Parameters["@ReturnValue"].Direction = ParameterDirection.ReturnValue;

                    command.ExecuteNonQuery();
                    int returnCode = (int)command.Parameters["@ReturnValue"].Value;

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        if (returnCode == 0)
                        {
                            m_onCompletedCallback(this);
                        }
                        else if (returnCode == 1)
                        {
                            TextBlock_AppMessage.Text = string.Format("The account '{0}' already exists.", accountName);
                        }
                        else if (returnCode == 2)
                        {
                            TextBlock_AppMessage.Text = string.Format("The Email '{0}' is already in use.", email);
                        }
                        else if (returnCode == -1)
                        {
                            m_adminAccounts.OnError(this, "The database does not contain required membership objects.");
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

        void OnError(string message)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                m_adminAccounts.OnError(this, message);
            }));
        }
    }
}
