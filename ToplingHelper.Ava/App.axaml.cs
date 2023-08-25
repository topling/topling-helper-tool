using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
using ToplingHelper.Ava.Views;
using ToplingHelperModels;

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
                            var options = new JsonSerializerOptions();
                            options.Converters.Add(new JsonStringEnumConverter());

                            userData = json?["ToplingUserData"]?.Deserialize<ToplingUserData>(options) ?? userData;
                            constants = json?["ToplingConstants"]?.Deserialize<ToplingConstants>(options) ?? constants;
                        }
                        catch (Exception e1)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {

                                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                                    .GetMessageBoxCustomWindow(new MessageBoxCustomParams
                                    {
                                        ContentTitle = "json注入错误",
                                        ContentMessage = e1.Message,
                                        FontFamily = "Microsoft YaHei,Simsun",
                                        ButtonDefinitions = new[]
                                            { new ButtonDefinition { Name = "确定", IsDefault = true }, },
                                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                    });
                                messageBoxStandardWindow.Show();
                            });
                        }
                    }

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