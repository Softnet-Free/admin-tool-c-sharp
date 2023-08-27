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
    /// Interaction logic for RemoveFromAdmins.xaml
    /// </summary>
    public partial class RemoveFromAdmins : UserControl, Closable
    {
        AdminAccounts m_adminAccounts;
        Action<Closable> m_onCompletedCallback;

        public RemoveFromAdmins(AdminAccounts adminAccounts, Action<Closable> onCompletedCallback)
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

        public void remove(long ownerId)
        {
            ThreadPool.QueueUserWorkItem(delegate { execute(ownerId); });
            ThreadPool.QueueUserWorkItem(delegate { PrintWaitMessage(); });
        }

        void execute(long ownerId)
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
                    command.CommandText = "Softnet_AdminTool_RemoveFromAdmins";

                    command.Parameters.Add("@OwnerId", SqlDbType.BigInt);
                    command.Parameters["@OwnerId"].Direction = ParameterDirection.Input;
                    command.Parameters["@OwnerId"].Value = ownerId;

                    command.ExecuteNonQuery();

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        m_onCompletedCallback(this);
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
            catch (InvalidOperationException) { }
        }

        void PrintWaitMessage()
        {
            Thread.Sleep(1000);
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                if (m_closed == false)
                    TextBlock_Message.Text = "The operation is pending, please wait ...";
            }));
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
