using System.ComponentModel;

namespace ToplingHelperModels.Models
{
    public class MySqlDataContext : INotifyPropertyChanged
    {
        private bool _isIsMyTopling = false;

        private bool _editServerId = false;


        public bool IsMySql
        {
            get => _isIsMyTopling;
            set
            {
                _isIsMyTopling = value;
                OnPropertyChanged(nameof(IsMySql));
                OnPropertyChanged(nameof(VisibleEditServerId));
            }
        }

        public bool VisibleEditServerId => _isIsMyTopling && _editServerId;

        public bool EditServerId
        {
            get => _editServerId;
            set
            {
                _editServerId = value;
                OnPropertyChanged(nameof(EditServerId));
                OnPropertyChanged(nameof(VisibleEditServerId));
            }
        }
        
        // Declare event
        public event PropertyChangedEventHandler? PropertyChanged;
        // OnPropertyChanged method to update property value in binding
        private void OnPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }
}
