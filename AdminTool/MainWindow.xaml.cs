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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool m_databaseInspected = false;

        public MainWindow()
        {
            InitializeComponent();
            ThreadPool.QueueUserWorkItem(delegate { Init(); });
        }

        void Init()
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            string connectionString = configuration.ConnectionStrings.ConnectionStrings["Softnet"].ConnectionString;
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    x_WorkFrame.Content = new ConnectionParams(this, ConnectionParams_OnApply, ConnectionParams_OnCancel);
                }
                else
                {
                    x_WorkFrame.Content = new DatabaseInspector(this, DatabaseInspector_OnSuccess, DatabaseInspector_OnCancel);
                }
            }));
        }

        public void OnError(Closable source, string message)
        {
            if (source.closed)
                return;
            source.close();
            x_WorkFrame.Content = new ErrorControl(message);
        }

        void ConnectionParams_Click(object sender, RoutedEventArgs e)
        {
            if (x_WorkFrame.Content != null)
                ((Closable)x_WorkFrame.Content).close();
            x_WorkFrame.Content = new ConnectionParams(this, ConnectionParams_OnApply, ConnectionParams_OnCancel);
        }

        void ConnectionParams_OnApply()
        {
            x_WorkFrame.Content = new DatabaseInspector(this, DatabaseInspector_OnSuccess, DatabaseInspector_OnCancel);
        }

        void ConnectionParams_OnCancel()
        {
            x_WorkFrame.Content = null;
        }

        void DatabaseInspector_OnSuccess(Closable source)
        {
            if (source.closed)
                return;
            source.close();            

            x_WorkFrame.Content = new AdminAccounts(this);            
        }

        void DatabaseInspector_OnCancel(Closable source)
        {
            if (source.closed)
                return;
            source.close();

            x_WorkFrame.Content = null;
        }

        void AdminAccounts_Click(object sender, RoutedEventArgs e)
        {
            if (m_databaseInspected == false)
                return;

            if (x_WorkFrame.Content != null)
                ((Closable)x_WorkFrame.Content).close();

            x_WorkFrame.Content = new AdminAccounts(this);
        }

        void Search_Click(object sender, RoutedEventArgs e)
        {
            if (m_databaseInspected == false)
                return;

            if (x_WorkFrame.Content != null)
                ((Closable)x_WorkFrame.Content).close();

            x_WorkFrame.Content = new Search(this, Search_OnCompleted);
        }

        void Search_OnCompleted(Closable source)
        {
            if (source.closed)
                return;
            source.close();
            x_WorkFrame.Content = new AdminAccounts(this);
        }
    }
}
