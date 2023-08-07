using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using HyperText.Avalonia.Controls;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Models;

namespace ToplingHelper.Ava.Controls
{
    internal class MyHyperLink : Hyperlink
    {
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                Dispatcher.UIThread.Post(() =>
                {

                    var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                        .GetMessageBoxCustomWindow(new MessageBoxCustomParams
                        {
                            ContentTitle = "错误",
                            ContentMessage = "请选择服务商",
                            FontFamily = "Microsoft YaHei,苹方-简",
                            ButtonDefinitions = new[]
                                { new ButtonDefinition { Name = "确定", IsDefault = true }, },
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Width = 200
                        });
                    messageBoxStandardWindow.ShowDialog(GetBaseWindow());
                });
            }
            else
            {
                base.OnPointerPressed(e);
            }
            
        }

        private Window GetBaseWindow()
        {
            
            IControl control = this;
            while (control.Parent != null)
            {
                if (control.Parent is Window window)
                {
                    return window;
                }
                
            }

            throw new ArgumentOutOfRangeException();
        }
    }
}
