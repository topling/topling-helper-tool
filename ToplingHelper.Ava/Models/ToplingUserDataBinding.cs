using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Avalonia.Controls.Notifications;
using ToplingHelperModels;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Models
{
    internal sealed class ToplingUserDataBinding : ToplingUserData, INotifyPropertyChanged
    {

        public ToplingUserDataBinding(ToplingUserData userData)
        {
            var props = typeof(ToplingUserData)
                .GetProperties().ToList();
            foreach (var prop in props)
            {
                var propInfo = typeof(ToplingUserDataBinding)
                    .GetProperty(prop.Name)!;
                propInfo.SetValue(this, prop.GetValue(userData));
                OnPropertyChanged(propInfo.Name);
            }
        }


        private bool _editServerId = false;

        private bool _useGtid;

        public new Provider Provider
        {
            get => base.Provider;
            set
            {
                base.Provider = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RamUrl));
            }
        }

        public string RamUrl =>
            Provider switch
            {
                Provider.AliYun => "https://ram.console.aliyun.com/manage",
                Provider.Aws => "https://us-east-1.console.aws.amazon.com/iamv2/home#/security_credentials",
                Provider.Unknown => "",
                _ => ""
            };

        public bool UseGtid
        {
            get => _useGtid;
            set
            {
                _useGtid = value;
                OnPropertyChanged(nameof(VisibleEditServerId));
            }
        }

        public new InstanceType CreatingInstanceType
        {
            get => base.CreatingInstanceType;
            set
            {
                base.CreatingInstanceType = value;
                OnPropertyChanged(nameof(VisibleEditServerId));
                OnPropertyChanged(nameof(IsMySql));
                OnPropertyChanged(nameof(IsTodis));
            }
        }

        public bool VisibleEditServerId => IsMySql && _editServerId;


        public bool EditServerId
        {
            get => _editServerId;
            set
            {
                _editServerId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VisibleEditServerId));
            }
        }

        public bool IsMySql => CreatingInstanceType == InstanceType.MyTopling;

        public bool IsTodis => CreatingInstanceType == InstanceType.Todis;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
