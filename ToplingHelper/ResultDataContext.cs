using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ToplingHelper.Annotations;
using ToplingHelperModels.Models;

namespace ToplingHelper
{
    public class ResultDataContext : INotifyPropertyChanged
    {
        private string _toplingVpcId;
        private string _ecsId;
        private string _userVpcId;
        private string _instancePrivateIp;
        private string _peerId;
        private string _routeId;
        private ToplingConstants _constants;
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

        public ToplingConstants Constants
        {
            get => _constants;
            set
            {
                _constants = value;
                OnPropertyChanged(nameof(Constants));
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

        public string PeerId
        {
            get => _peerId;
            set
            {
                _peerId = value;
                OnPropertyChanged(nameof(PeerId));
            }
        }

        public string RouteId
        {
            get => _routeId;
            set
            {
                _routeId = value;
                OnPropertyChanged(nameof(RouteId));
                OnPropertyChanged(nameof(RouteUrl));
            }
        }

        public string RouteUrl =>
            $"https://vpcnext.console.aliyun.com/vpc/{_constants.ToplingTestRegion}/route-tables/{RouteId}";

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
