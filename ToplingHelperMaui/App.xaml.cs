#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace ToplingHelperMaui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SetMainWindowStartSize(880, 960);
            MainPage = new AppShell();
        }

        private void SetMainWindowStartSize(int width, int height)
        {
            Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(
                nameof(IWindow), (handler, view) => {
#if WINDOWS
                    var mauiWindow = handler.VirtualView;
                    var nativeWindow = handler.PlatformView;
                    nativeWindow.Activate();
                    IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                    AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    var a = DeviceDisplay.Current.MainDisplayInfo.Density;
                    appWindow.Resize(new SizeInt32(width / 96 * a, height / 96 * a)); 
#endif
#if MACCATALYST
                    var size = new CoreGraphics.CGSize(width, height);
                    handler.PlatformView.WindowScene.SizeRestrictions.MinimumSize = size;
                    handler.PlatformView.WindowScene.SizeRestrictions.MaximumSize = size; 
#endif
                }
            );
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            Window window = base.CreateWindow(activationState);
            window.Title = "拓扑岭自动化工具";
            return window;
        }
    }
}