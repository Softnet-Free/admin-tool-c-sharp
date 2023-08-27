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

namespace AdminTool
{
    /// <summary>
    /// Interaction logic for ConnectionConfig.xaml
    /// </summary>
    public partial class ConnectionParams : UserControl, Closable
    {
        MainWindow m_mainWindow;
        Action m_onApplyCallback;
        Action m_onCancelCallback;

        public ConnectionParams(MainWindow mainWindow, Action onApplyCallback, Action onCancelCallback)
        {
            InitializeComponent();
            m_mainWindow = mainWindow;
            m_onApplyCallback = onApplyCallback;
            m_onCancelCallback = onCancelCallback;
            Load();
        }

        bool m_closed = false;
        public bool closed { get { return m_closed; } }
        public void close()
        {
            m_closed = true;
        }

        public void Load()
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                resetConnectionString();
            }
            else
            {
                try
                {
                    SqlConnectionStringBuilder sqlConnStringBuilder = new SqlConnectionStringBuilder(connectionString);
                    if (sqlConnStringBuilder.IntegratedSecurity)
                    {
                        TrustedConnection.IsSelected = true;
                        TextBox_TrustedConnectionString.Text = sqlConnStringBuilder.ConnectionString;
                    }
                    else
                    {
                        StandartConnection.IsSelected = true;

                        TextBox_Server.Text = sqlConnStringBuilder.DataSource;
                        TextBox_Database.Text = sqlConnStringBuilder.InitialCatalog;
                        TextBox_UserId.Text = sqlConnStringBuilder.UserID;
                        TextBox_Password.Password = sqlConnStringBuilder.Password;

                        TextBox_TrustedConnectionString.Text = "Data Source=.;Initial Catalog=Softnet;Integrated Security=SSPI;";
                    }
                }
                catch (Exception)
                {
                    resetConnectionString();
                }            
            }
        }

        void resetConnectionString()
        {
            StandartConnection.IsSelected = true;
            TextBox_Server.Text = "";
            TextBox_Database.Text = "Softnet";
            TextBox_UserId.Text = "";
            TextBox_Password.Password = "";

            TextBox_TrustedConnectionString.Text = "Data Source=.;Initial Catalog=Softnet;Integrated Security=SSPI;";
        }

        void ApplyStandartConnection_Click(object sender, EventArgs e)
        {
            m_mainWindow.m_databaseInspected = false;

            Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString =
                string.Format("Data Source={0}; Initial Catalog={1}; User ID={2}; Password={3};", TextBox_Server.Text, TextBox_Database.Text, TextBox_UserId.Text, TextBox_Password.Password);
            configuration.Save(ConfigurationSaveMode.Modified, false);
            ConfigurationManager.RefreshSection("connectionStrings");

            m_onApplyCallback();
        }

        void ApplyTrustedConnection_Click(object sender, EventArgs e)
        {
            m_mainWindow.m_databaseInspected = false;

            Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString = TextBox_TrustedConnectionString.Text;
            configuration.Save(ConfigurationSaveMode.Modified, false);
            ConfigurationManager.RefreshSection("connectionStrings");

            m_onApplyCallback();
        }

        void Cancel_Click(object sender, EventArgs e)
        {
            m_onCancelCallback();
        }
    }
}
