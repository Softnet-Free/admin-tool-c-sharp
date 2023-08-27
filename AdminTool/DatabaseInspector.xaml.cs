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
    /// Interaction logic for DatabaseInspector.xaml
    /// </summary>
    public partial class DatabaseInspector : UserControl, Closable
    {
        MainWindow m_mainWindow;
        Action<Closable> m_onSuccessCallback;
        Action<Closable> m_onCancelCallback;

        public DatabaseInspector(MainWindow mainWindow, Action<Closable> onSuccessCallback, Action<Closable> onCancelCallback)
        {
            InitializeComponent();
            m_mainWindow = mainWindow;
            m_onSuccessCallback = onSuccessCallback;
            m_onCancelCallback = onCancelCallback;

            ThreadPool.QueueUserWorkItem(Execute);
            ThreadPool.QueueUserWorkItem(PrintWaitMessage);
        }

        bool m_closed = false;
        public bool closed { get { return m_closed; } }
        public void close()
        {
            m_closed = true;
            if (m_Connection != null)
                m_Connection.Close();   
        }

        bool m_ConnectionEstablished = false;
        SqlConnection m_Connection = null;

        void PrintWaitMessage(object noData)
        {
            Thread.Sleep(500);                        
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                if (m_ConnectionEstablished == false)                    
                    Panel_Wait.Visibility = System.Windows.Visibility.Visible;
            }));            
        }

        void Execute(object noData)
        {
            try
            {
                if (ValidateDatabase() != 0)
                {                    
                    OnError("The specified database is not a valid softnet database.");
                    return;
                }

                Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                {
                    m_ConnectionEstablished = true;
                    Panel_Wait.Visibility = System.Windows.Visibility.Collapsed;
                }));

                if (InspectMembership() != 0)
                {
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        Panel_Wait.Visibility = System.Windows.Visibility.Collapsed;
                        Panel_Setup.Visibility = System.Windows.Visibility.Visible;
                    }));
                }
                else
                {
                    m_mainWindow.m_databaseInspected = true;
                    OnSuccess();
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

        private void ConnectingCancel_Click(object sender, RoutedEventArgs e)
        {
            if (m_Connection != null)
                m_Connection.Close();            
            m_onCancelCallback(this);
        }

        private void SetupYes_Click(object sender, RoutedEventArgs e)
        {
            Panel_Setup.Visibility = System.Windows.Visibility.Collapsed;
            ThreadPool.QueueUserWorkItem(delegate { SetupMembership(); });
        }

        private void SetupNo_Click(object sender, RoutedEventArgs e)
        {
            m_onCancelCallback(this);
        }

        private void SetupSuccessOk_Click(object sender, RoutedEventArgs e)
        {
            m_onSuccessCallback(this);
        }

        int ValidateDatabase()
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                {
                    m_Connection = connection;
                }));

                connection.Open();

                SqlCommand command = new SqlCommand();
                command.Connection = connection;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "Softnet_AdminTool_ValidateDatabase";

                command.Parameters.Add("@ReturnValue", SqlDbType.Int);
                command.Parameters["@ReturnValue"].Direction = ParameterDirection.ReturnValue;

                command.ExecuteNonQuery();
                return (int)command.Parameters["@ReturnValue"].Value;                    
            }
        }

        int InspectMembership()
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                m_Connection = connection;

                SqlCommand command = new SqlCommand();
                command.Connection = connection;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "Softnet_AdminTool_InspectMembership";

                command.Parameters.Add("@ReturnValue", SqlDbType.Int);
                command.Parameters["@ReturnValue"].Direction = ParameterDirection.ReturnValue;

                command.ExecuteNonQuery();
                return (int)command.Parameters["@ReturnValue"].Value;
            }
        }

        void SetupMembership()
        {
            try
            {
                Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    m_Connection = connection;

                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "Softnet_AdminTool_SetupMembership";

                    command.ExecuteNonQuery();
                    m_mainWindow.m_databaseInspected = true;

                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        Panel_Success.Visibility = System.Windows.Visibility.Visible;
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

        void OnError(string message)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                m_mainWindow.OnError(this, message);
            }));
        }

        void OnSuccess()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                m_onSuccessCallback(this);
            }));
        }
    }
}
