using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Net.WebRequestMethods;

namespace MusicNotesEditor.Views
{
    public partial class FilePreviewWindow : Window
    {
        private const int WM_NCCALCSIZE = 0x0083;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int RESIZE_MARGIN = 6;

        public FileItem FileItem { get; set; }
        public ImageSource HighResolutionImage { get; set; }
        public string FileInfo { get; set; }

        public FilePreviewWindow(FileItem fileItem)
        {
            FileItem = fileItem;
            InitializeComponent();
            LoadHighResolutionImage();
            UpdateFileInfo();
            DataContext = this;
        }

        private void LoadHighResolutionImage()
        {
            try
            {
                var extension = Path.GetExtension(FileItem.FilePath).ToLower();

                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                {
                    HighResolutionImage = LoadFullResolutionImage(FileItem.FilePath);
                }
                else
                {
                    HighResolutionImage = FileItem.Thumbnail;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading high resolution image: {ex.Message}",
                    "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                HighResolutionImage = CreateHighResPlaceholder();
            }
        }

        private BitmapImage LoadFullResolutionImage(string imagePath)
        {
            try
            {
                var bitmapImage = new BitmapImage();
                using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = fileStream;

                    // Don't set DecodePixelWidth/Height for full resolution
                    // Let WPF handle the scaling with Stretch="Uniform"
                    bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmapImage.EndInit();
                }
                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading full resolution image: {ex.Message}");
                return CreateHighResPlaceholder();
            }
        }

        private BitmapImage CreateHighResPlaceholder()
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, 400, 400));
                drawingContext.DrawRectangle(null, new Pen(Brushes.Gray, 2), new Rect(0, 0, 400, 400));

                var formattedText = new FormattedText(
                    "High Resolution Preview Not Available",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    16,
                    Brushes.Black,
                    1.0);

                double x = (400 - formattedText.Width) / 2;
                double y = (400 - formattedText.Height) / 2;
                drawingContext.DrawText(formattedText, new Point(x, y));
            }

            var renderTarget = new RenderTargetBitmap(400, 400, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();

            var bitmapImage = new BitmapImage();
            using (var stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                encoder.Save(stream);
                stream.Position = 0;

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            }
            bitmapImage.Freeze();
            return bitmapImage;
        }

        private void UpdateFileInfo()
        {
            var fileInfo = new FileInfo(FileItem.FilePath);
            FileInfo = $"Type: {FileItem.FileType} | Size: {FileItem.FileSize} | Dimensions: {GetImageDimensions()}";
        }

        private string GetImageDimensions()
        {
            try
            {
                var extension = Path.GetExtension(FileItem.FilePath).ToLower();
                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                {
                    var bitmapImage = new BitmapImage(new Uri(FileItem.FilePath));
                    return $"{bitmapImage.PixelWidth} x {bitmapImage.PixelHeight}";
                }
            }
            catch
            {
                // Ignore errors
            }
            return "Unknown";
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Enable custom window chrome handling
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
            switch (msg)
            {
                case WM_NCCALCSIZE:
                    // Remove standard title bar but keep resize borders
                    if (wParam.ToInt32() == 1)
                    {
                        handled = true;
                        return IntPtr.Zero;
                    }
                    break;

                case WM_NCHITTEST:
                    // Custom hit testing for resize and drag
                    var result = HitTestNCA(hwnd, wParam, lParam);
                    if (result != IntPtr.Zero)
                    {
                        handled = true;
                        return result;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private IntPtr HitTestNCA(IntPtr hwnd, IntPtr wParam, IntPtr lParam)
        {
            // Get the point coordinates
            var screenPoint = new Point((int)lParam & 0xFFFF, (int)lParam >> 16);

            // Convert to window-relative coordinates
            var windowPoint = PointFromScreen(screenPoint);

            // Define resize margins
            var resizeLeft = windowPoint.X <= RESIZE_MARGIN;
            var resizeRight = windowPoint.X >= ActualWidth - RESIZE_MARGIN;
            var resizeTop = windowPoint.Y <= RESIZE_MARGIN;
            var resizeBottom = windowPoint.Y >= ActualHeight - RESIZE_MARGIN;

            // Determine which resize cursor to show
            if (resizeTop && resizeLeft) return new IntPtr(HTTOPLEFT);
            if (resizeTop && resizeRight) return new IntPtr(HTTOPRIGHT);
            if (resizeBottom && resizeLeft) return new IntPtr(HTBOTTOMLEFT);
            if (resizeBottom && resizeRight) return new IntPtr(HTBOTTOMRIGHT);
            if (resizeLeft) return new IntPtr(HTLEFT);
            if (resizeRight) return new IntPtr(HTRIGHT);
            if (resizeTop) return new IntPtr(HTTOP);
            if (resizeBottom) return new IntPtr(HTBOTTOM);

            // Check if click is in title bar area for dragging
            var titleBarRect = new Rect(0, 0, ActualWidth, 32);
            if (titleBarRect.Contains(windowPoint))
            {
                // Exclude the window buttons area from dragging
                var buttonsRect = new Rect(ActualWidth - 105, 0, 105, 32); // 3 buttons * 35 width each
                if (!buttonsRect.Contains(windowPoint))
                {
                    return new IntPtr(HTCAPTION);
                }
            }

            return new IntPtr(HTCLIENT);
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
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

        public void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.BorderThickness = new System.Windows.Thickness(8);
            }
            else
            {
                this.BorderThickness = new System.Windows.Thickness(0);
            }
        }

        private bool IsMaximized()
        {
            return WindowState == WindowState.Maximized;
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
                    // For maximized windows, dragging will restore and move
                    if (WindowState == WindowState.Maximized)
                    {
                        // Calculate the position to restore to based on mouse position
                        var screenPoint = PointToScreen(e.GetPosition(this));

                        // Temporarily set window to normal to calculate position
                        WindowState = WindowState.Normal;

                        // Calculate new window position (centered on mouse X, top at 0)
                        var newWidth = 900; // Your default width
                        Left = Math.Max(0, screenPoint.X - (newWidth / 2));
                        Top = 0;

                        // Now start the drag
                        this.DragMove();
                    }
                    else
                    {
                        this.DragMove();
                    }
                }
            }
        }

        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                // Restore to your preferred default size
                Width = 900;
                Height = 600;
                CenterWindow();
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void CenterWindow()
        {
            Left = (SystemParameters.WorkArea.Width - Width) / 2;
            Top = (SystemParameters.WorkArea.Height - Height) / 2;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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