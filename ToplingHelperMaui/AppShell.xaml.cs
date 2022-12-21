namespace ToplingHelperMaui
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(RichText), typeof(RichText));
        }
    }
}