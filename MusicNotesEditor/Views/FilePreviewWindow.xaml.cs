using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MusicNotesEditor.Views
{
    public partial class FilePreviewWindow : Window
    {
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}