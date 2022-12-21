namespace ToplingHelperMaui
{
    public partial class MainPage : ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
        }

        private void Set_Todis(object sender, CheckedChangedEventArgs e)
        {

        }

        private void Set_MyTopling(object sender, CheckedChangedEventArgs e)
        {

        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            Shell.Current.GoToAsync(nameof(RichText));
            //Application.Current!.OpenWindow(new Window(new RichText()));
        }
    }
}