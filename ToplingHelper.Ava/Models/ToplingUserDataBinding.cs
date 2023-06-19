﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Models
{
    internal sealed class ToplingUserDataBinding : ToplingUserData, INotifyPropertyChanged
    {
        public ToplingUserDataBinding()
        {

        }

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
