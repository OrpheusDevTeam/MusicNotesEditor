using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MusicNotesEditor.Views
{
    public partial class FileArrangerWindow : Window
    {
        private ObservableCollection<FileItem> fileItems = new ObservableCollection<FileItem>();
        private Point startPoint;
        private FileItem draggedItem;

        public string[] SelectedFiles => fileItems.Select(f => f.FilePath).ToArray();

        public FileArrangerWindow()
        {
            InitializeComponent();
            filesListView.ItemsSource = fileItems;
            UpdateFileCount();
        }

        private void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All Supported Files|*.pdf;*.png;*.jpg;*.jpeg|PDF Files|*.pdf|Image Files|*.png;*.jpg;*.jpeg",
                Title = "Select PDF or Image Files"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AddFiles(openFileDialog.FileNames);
            }
        }

        private void AddFiles(string[] filePaths)
        {
            foreach (string filePath in filePaths)
            {
                if (fileItems.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileItem = new FileItem
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        FileType = Path.GetExtension(filePath).ToUpper(),
                        FileSize = FormatFileSize(fileInfo.Length),
                        Order = fileItems.Count + 1
                    };

                    fileItems.Add(fileItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading file {filePath}: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            GenerateThumbnails();
            UpdateFileCount();
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is FileItem fileItem)
                {
                    var previewWindow = new FilePreviewWindow(fileItem);
                    previewWindow.Owner = this;
                    previewWindow.ShowDialog();
                }
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        #region Drag and Drop

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(null);
            draggedItem = (sender as ListViewItem)?.Content as FileItem;
        }

        private void ListViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && draggedItem != null)
            {
                Point currentPoint = e.GetPosition(null);
                Vector diff = startPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(filesListView, draggedItem, DragDropEffects.Move);
                }
            }
        }

        private void FilesListView_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            return parent ?? FindVisualParent<T>(parentObject);
        }

        private void FilesListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(FileItem)) is FileItem sourceItem)
            {
                // Use the local method instead of extension method
                var targetItem = FindVisualParent<ListViewItem>((DependencyObject)e.OriginalSource)?.Content as FileItem;

                if (targetItem != null && sourceItem != targetItem)
                {
                    int oldIndex = fileItems.IndexOf(sourceItem);
                    int newIndex = fileItems.IndexOf(targetItem);

                    fileItems.Move(oldIndex, newIndex);
                    UpdateOrderNumbers();
                }
            }
        }

        #endregion

        #region Button Handlers

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (filesListView.SelectedItem is FileItem selectedItem)
            {
                int currentIndex = fileItems.IndexOf(selectedItem);
                if (currentIndex > 0)
                {
                    fileItems.Move(currentIndex, currentIndex - 1);
                    UpdateOrderNumbers();
                }
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (filesListView.SelectedItem is FileItem selectedItem)
            {
                int currentIndex = fileItems.IndexOf(selectedItem);
                if (currentIndex < fileItems.Count - 1)
                {
                    fileItems.Move(currentIndex, currentIndex + 1);
                    UpdateOrderNumbers();
                }
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = filesListView.SelectedItems.Cast<FileItem>().ToList();
            foreach (var item in selectedItems)
            {
                fileItems.Remove(item);
            }
            UpdateOrderNumbers();
            UpdateFileCount();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            fileItems.Clear();
            UpdateFileCount();
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            if (fileItems.Count == 0)
            {
                MessageBox.Show("Please select at least one file to process.", "No Files",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        #endregion

        private void UpdateOrderNumbers()
        {
            for (int i = 0; i < fileItems.Count; i++)
            {
                fileItems[i].Order = i + 1;
            }
            filesListView.Items.Refresh();
        }

        private void UpdateFileCount()
        {
            lblFileCount.Text = $"{fileItems.Count} file(s) selected";
            btnProcess.IsEnabled = fileItems.Count > 0;
        }

        private async void GenerateThumbnails()
        {
            foreach (var fileItem in fileItems)
            {
                if (fileItem.Thumbnail == null)
                {
                    await GenerateThumbnailAsync(fileItem);
                }
            }
        }

        private async Task GenerateThumbnailAsync(FileItem fileItem)
        {
            try
            {
                // Set a temporary placeholder immediately
                fileItem.Thumbnail = CreateDefaultThumbnail("Loading...");

                var thumbnail = await Task.Run(() => CreateThumbnail(fileItem.FilePath));
                if (thumbnail != null)
                {
                    fileItem.Thumbnail = thumbnail;
                }
                else
                {
                    fileItem.Thumbnail = CreateDefaultThumbnail("Error");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating thumbnail for {fileItem.FileName}: {ex.Message}");
                fileItem.Thumbnail = CreateDefaultThumbnail("Error");
            }
        }

        private BitmapImage CreateThumbnail(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            try
            {
                switch (extension)
                {
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                        return CreateImageThumbnail(filePath);
                    case ".pdf":
                        return CreatePdfThumbnail(filePath);
                    default:
                        return CreateDefaultThumbnail("Unsupported");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating thumbnail for {filePath}: {ex.Message}");
                return CreateDefaultThumbnail("Error");
            }
        }

        private BitmapImage CreateImageThumbnail(string imagePath)
        {
            try
            {
                var bitmapImage = new BitmapImage();
                using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = fileStream;
                    bitmapImage.DecodePixelWidth = 120; // Thumbnail size
                    bitmapImage.DecodePixelHeight = 90;
                    bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat;
                    bitmapImage.EndInit();
                }

                // If the image has transparency, convert it to a format that supports it better
                if (bitmapImage.Format == PixelFormats.Bgr32 ||
                    bitmapImage.Format == PixelFormats.Bgra32 ||
                    bitmapImage.Format == PixelFormats.Pbgra32)
                {
                    bitmapImage = ConvertToCompatibleFormat(bitmapImage);
                }

                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image {imagePath}: {ex.Message}");
                Debug.WriteLine($"Image format: {Path.GetExtension(imagePath)}");
                return CreateDefaultThumbnail("Image Error");
            }
        }

        private BitmapImage ConvertToCompatibleFormat(BitmapImage sourceImage)
        {
            try
            {
                var formatConvertedBitmap = new FormatConvertedBitmap();
                formatConvertedBitmap.BeginInit();
                formatConvertedBitmap.Source = sourceImage;
                formatConvertedBitmap.DestinationFormat = PixelFormats.Pbgra32; // Standard format that works well
                formatConvertedBitmap.EndInit();
                formatConvertedBitmap.Freeze();

                // Convert back to BitmapImage
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(formatConvertedBitmap));
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
            catch
            {
                return sourceImage; // Return original if conversion fails
            }
        }

        private BitmapImage CreatePdfThumbnail(string pdfPath)
        {
            // For now, return a PDF placeholder
            return CreateDefaultThumbnail("PDF");
        }

        private BitmapImage CreateDefaultThumbnail(string text = "Preview")
        {
            try
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Draw background
                    drawingContext.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, 60, 45));

                    // Draw border
                    drawingContext.DrawRectangle(null, new Pen(Brushes.DarkGray, 1), new Rect(0, 0, 60, 45));

                    // Draw text
                    var formattedText = new FormattedText(
                        text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        8, // Smaller font
                        Brushes.Black,
                        1.0);

                    // Center the text
                    double x = (60 - formattedText.Width) / 2;
                    double y = (45 - formattedText.Height) / 2;
                    drawingContext.DrawText(formattedText, new Point(x, y));
                }

                var renderTarget = new RenderTargetBitmap(60, 45, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);

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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating default thumbnail: {ex.Message}");
                // Return a simple colored rectangle as fallback
                return CreateSimpleColorThumbnail(Colors.LightGray);
            }
        }

        private BitmapImage CreateSimpleColorThumbnail(Color color)
        {
            try
            {
                var renderTarget = new RenderTargetBitmap(60, 45, 96, 96, PixelFormats.Pbgra32);
                var drawingVisual = new DrawingVisual();

                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 60, 45));
                }

                renderTarget.Render(drawingVisual);
                renderTarget.Freeze();

                // Convert RenderTargetBitmap to BitmapImage
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
            catch
            {
                // Ultimate fallback - return null
                return null;
            }
        }

    }

    public class FileItem : INotifyPropertyChanged
    {
        private int _order;
        private ImageSource _thumbnail;

        public int Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged(nameof(Order));
            }
        }

        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FileSize { get; set; }

        public ImageSource Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}