using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.CodeAnalysis;
using ToplingHelper.Ava.Models;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ChangeServiceInstanceType(object? sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && DataContext is not null)
            {
                var context = DataContext as ToplingUserDataBinding;
                context!.CreatingInstanceType = btn.Name switch
                {
                    "MySqlRadio" => ToplingUserData.InstanceType.MyTopling,
                    "TodisRadio" => ToplingUserData.InstanceType.Todis,
                    _ => context!.CreatingInstanceType
                };
            }
            //throw new System.NotImplementedException();
        }

    }
}