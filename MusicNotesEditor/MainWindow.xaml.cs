using MusicNotesEditor.Views;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace MusicNotesEditor
{
    public partial class MainWindow : Window
    {
        private const int WM_NCCALCSIZE = 0x0083;

        public MainWindow()
        {
            InitializeComponent();

            this.StateChanged += MainWindow_StateChanged;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Remove any window chrome effects
            this.SnapsToDevicePixels = true;
            this.UseLayoutRounding = true;

            MainFrame.Navigate(new MainMenuPage());
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Remove all non-client area (borders)
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }

            // Remove rounded corners (Windows 11+)
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
                var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(hwnd, attribute, ref preference, sizeof(uint));

                // Also remove margins
                attribute = DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE;
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, attribute, ref darkMode, sizeof(int));
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCCALCSIZE && wParam.ToInt32() == 1)
            {
                // Remove all non-client area (borders)
                handled = true;
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (IsMaximized())
            {
                MaximizeBtn.Content = "❑";
                MaximizeBtn.ToolTip = "Restore Down";
            }
            else
            {
                MaximizeBtn.Content = "❐";
                MaximizeBtn.ToolTip = "Maximize";
            }
        }

        private bool IsMaximized()
        {
            return this.WindowState == WindowState.Maximized ||
                   (Math.Abs(this.Width - SystemParameters.WorkArea.Width) < 1 &&
                    Math.Abs(this.Height - SystemParameters.WorkArea.Height) < 1);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void ToggleMaximize()
        {
            if (IsMaximized())
            {
                // Restore to normal size
                this.WindowState = WindowState.Normal;
                this.Width = 900;
                this.Height = 600;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                CenterWindow();
            }
            else
            {
                // Maximize to work area
                this.WindowState = WindowState.Maximized;
            }
        }

        private void CenterWindow()
        {
            this.Left = (SystemParameters.WorkArea.Width - this.Width) / 2;
            this.Top = (SystemParameters.WorkArea.Height - this.Height) / 2;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Windows API for removing borders and rounded corners
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int pvAttribute, uint cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute, uint cbAttribute);

        private enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20
        }

        private enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }
    }
}