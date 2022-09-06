using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using ToplingHelperModels.Models;

namespace ToplingHelper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            //var mainWindow = new MainWindow(e.Args);
            var userData = new ToplingUserData();
            var constants = new ToplingConstants();
            if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            {
                var content = File.ReadAllText(e.Args[0]);
                try
                {
                    var json = JsonNode.Parse(content);

                    userData = json?["ToplingUserData"]?.Deserialize<ToplingUserData>() ?? userData;
                    constants = json?["ToplingConstants"]?.Deserialize<ToplingConstants>() ?? constants;
                }
                catch (Exception e1)
                {
                    Dispatcher.Invoke(() => MessageBox.Show(e1.ToString()));
                }

            }

            var mainWindow = new MainWindow(constants, userData);

            mainWindow.Show();
        }
    }
}
