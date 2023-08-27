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
    /// Interaction logic for AdminAccounts.xaml
    /// </summary>
    public partial class AdminAccounts : UserControl, Closable
    {
        MainWindow m_mainWindow;
        
        public AdminAccounts(MainWindow mainWindow)
        {
            InitializeComponent();

            m_mainWindow = mainWindow;
            x_Content.Content = new AdminList(this, AdminList_OnNewAccount, AdminList_OnChangePassword, AdminList_OnRemoveFromAdmins);
        }

        bool m_closed = false;
        public bool closed { get { return m_closed; } }
        public void close()
        {
            m_closed = true;
        }        

        void AdminList_OnNewAccount()
        {
            x_Content.Content = new NewAccount(this, NewAccount_OnCompleted);
        }

        void AdminList_OnChangePassword(long ownerId)
        {
            x_Content.Content = new ChangeAdminPassword(ownerId, this, ChangePassword_OnCompleted);           
        }

        void AdminList_OnRemoveFromAdmins(long ownerId)
        {
            RemoveFromAdmins actuator = new RemoveFromAdmins(this, RemoveFromAdmins_OnCompleted);
            x_Content.Content = actuator;
            actuator.remove(ownerId);
        }

        void NewAccount_OnCompleted(Closable source)
        {
            if (source.closed)
                return;
            source.close();
            x_Content.Content = new AdminList(this, AdminList_OnNewAccount, AdminList_OnChangePassword, AdminList_OnRemoveFromAdmins);
        }

        void ChangePassword_OnCompleted(Closable source)
        {
            if (source.closed)
                return;
            source.close();
            x_Content.Content = new AdminList(this, AdminList_OnNewAccount, AdminList_OnChangePassword, AdminList_OnRemoveFromAdmins);
        }

        void RemoveFromAdmins_OnCompleted(Closable source)
        {
            if (source.closed)
                return;
            source.close();
            x_Content.Content = new AdminList(this, AdminList_OnNewAccount, AdminList_OnChangePassword, AdminList_OnRemoveFromAdmins);
        }

        public void OnError(Closable source, string message)
        {
            if (source.closed)
                return;
            source.close();
            m_mainWindow.OnError(this, message);
        }
    }
}
