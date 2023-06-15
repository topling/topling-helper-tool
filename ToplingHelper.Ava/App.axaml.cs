using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Models;
using Newtonsoft.Json;
using ToplingHelper.Ava.Models;
using ToplingHelper.Ava.ViewModels;
using ToplingHelper.Ava.Views;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Startup += (sender, e) =>
                {
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
                            Dispatcher.UIThread.Post(() =>
                            {
                                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                                    .GetMessageBoxCustomWindow(new MessageBoxCustomParams
                                    {
                                        ContentTitle = "json注入错误",
                                        ContentMessage = JsonConvert.SerializeObject(e1.Data),
                                        FontFamily = "Microsoft YaHei,Simsun",
                                        ButtonDefinitions = new[]
                                            { new ButtonDefinition { Name = "确定", IsDefault = true }, },
                                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                    });
                                messageBoxStandardWindow.Show();
                            });
                        }
                    }
    
                    userData.AccessId = "123";
                    desktop.MainWindow = new MainWindow()
                    {
                        ToplingConstants = constants,
                        DataContext = new ToplingUserDataBinding(userData)
                    };
                };
            }
            

            base.OnFrameworkInitializationCompleted();
        }
    }
}