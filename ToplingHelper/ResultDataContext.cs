using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ToplingHelper.Annotations;

namespace ToplingHelper
{
    public class ResultDataContext : INotifyPropertyChanged
    {
        private string _toplingVpcId;
        private string _ecsId;
        private string _userVpcId;
        private string _cenId;
        private string _instancePrivateIp;
        public event PropertyChangedEventHandler PropertyChanged;

        public string ToplingVpcId
        {
            get => _toplingVpcId;
            set
            {
                _toplingVpcId = value;
                OnPropertyChanged(nameof(ToplingVpcId));
            }
            
        }

        public string EcsId
        {
            get => _ecsId;
            set
            {
                _ecsId = value;
                OnPropertyChanged(nameof(EcsId));
            }
        }

        public string UserVpcId
        {
            get => _userVpcId;
            set
            {
                _userVpcId = value;
                OnPropertyChanged(nameof(UserVpcId));
            }
        }

        public string CenId
        {
            get => _cenId;
            set
            {
                _cenId = value;
                OnPropertyChanged(nameof(CenId));
                OnPropertyChanged(nameof(UserCenUrl));
            }
        }


        public string UserCenUrl => $"https://cen.console.aliyun.com/cen/detail/{CenId}/attachInstance";

        public string InstancePrivateIp
        {
            get => _instancePrivateIp;
            set
            {
                _instancePrivateIp = value;
                OnPropertyChanged(nameof(InstancePrivateIp));
            }
        }

        // OnPropertyChanged method to update property value in binding
        private void OnPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }
}
