using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MusicNotesEditor.ViewModels;


namespace MusicNotesEditor.Views
{
    public partial class FileArrangerPage : Page
    {
        private ObservableCollection<FileItem> fileItems = new ObservableCollection<FileItem>();
        private Point startPoint;
        private FileItem draggedItem;

        private readonly FileArrangerViewModel viewModel = new FileArrangerViewModel();

        public string[] SelectedFiles => fileItems.Select(f => f.FilePath).ToArray();

        public FileArrangerPage()
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

        private async void AddFiles(string[] filePaths)
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
                    await GenerateThumbnailAsync(fileItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading file {filePath}: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            UpdateFileCount();
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is FileItem fileItem)
                {
                    var previewWindow = new FilePreviewWindow(fileItem);
                    previewWindow.Owner = Window.GetWindow(this); // Get the parent window
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

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private async void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            if (fileItems.Count == 0)
            {
                MessageBox.Show("Please select at least one file to process.", "No Files",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get the ordered file paths
            string[] orderedFiles = SelectedFiles;

            try
            {
                // Disable UI during processing
                SetProcessingUI(true);

                // Show processing dialog or progress
                var progress = new Progress<string>(message =>
                {
                    // Update your UI with progress messages
                    lblFileCount.Text = message;
                });

                // Process files with Python
                string pythonResult = await ProcessFilesWithPythonAsync(orderedFiles, progress);

                // Python processing successful, navigate to Music Editor
                // You might want to pass the pythonResult to the MusicEditorPage
                NavigationService.Navigate(new MusicEditorPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing files: {ex.Message}\n\nPlease check that Python is installed and the script path is correct.",
                    "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable UI
                SetProcessingUI(false);
            }
        }

        private async Task<string> ProcessFilesWithPythonAsync(string[] orderedFiles, IProgress<string> progress)
        {
            string pythonScriptPath = @"C:\Users\jmosz\Desktop\Studia\ZPI Team Project\OMR\main.py";
            string pythonExecutable = "python";

            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report("Starting Python script...");

                    string arguments = $"\"{pythonScriptPath}\" {string.Join(" ", orderedFiles.Select(f => $"\"{f}\""))}";

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = pythonExecutable,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(pythonScriptPath)
                    };

                    using (var process = new Process())
                    {
                        process.StartInfo = processStartInfo;

                        var outputBuilder = new StringBuilder();
                        var errorBuilder = new StringBuilder();

                        process.OutputDataReceived += (sender, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                outputBuilder.AppendLine(e.Data);
                                progress?.Report($"Processing: {e.Data}");
                            }
                        };

                        process.ErrorDataReceived += (sender, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                                errorBuilder.AppendLine(e.Data);
                        };

                        progress?.Report("Executing Python script...");
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        bool completed = process.WaitForExit(30000);

                        if (!completed)
                        {
                            process.Kill();
                            throw new TimeoutException("Python script execution timed out");
                        }

                        if (process.ExitCode != 0)
                        {
                            string errorMessage = errorBuilder.ToString();
                            throw new Exception($"Python script failed: {errorMessage}");
                        }

                        progress?.Report("Python script completed successfully");
                        return outputBuilder.ToString();
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error: {ex.Message}");
                    throw new Exception($"Failed to execute Python script: {ex.Message}", ex);
                }
            });
        }

        private void SetProcessingUI(bool isProcessing)
        {
            btnProcess.IsEnabled = !isProcessing;
            btnSelectFiles.IsEnabled = !isProcessing;
            btnImportMusicXml.IsEnabled = !isProcessing;
            btnMoveUp.IsEnabled = !isProcessing;
            btnMoveDown.IsEnabled = !isProcessing;
            btnRemove.IsEnabled = !isProcessing;
            btnClearAll.IsEnabled = !isProcessing;

            if (isProcessing)
            {
                btnProcess.Content = "Processing...";
            }
            else
            {
                btnProcess.Content = "Process";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
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
                    bitmapImage.DecodePixelWidth = 120; // Reduce size for thumbnail
                    bitmapImage.DecodePixelHeight = 90; // Maintain aspect ratio
                    bitmapImage.Rotation = Rotation.Rotate0;
                    bitmapImage.EndInit();
                }
                bitmapImage.Freeze(); // Important for cross-thread access
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image {imagePath}: {ex.Message}");
                return CreateDefaultThumbnail("Image Error");
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

        private void BtnImportMusicXml_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "MusicXML files (*.musicxml, *.xml)|*.musicxml;*.xml|All files (*.*)|*.*",
                Title = "Select MusicXML File",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string musicXmlFilePath = openFileDialog.FileName;

                try
                {
                    // Validate the MusicXML file if needed
                    viewModel.TestData(musicXmlFilePath);
                    if (viewModel.ValidateMusicXmlWithXsd(musicXmlFilePath))
                    {
                        // Navigate directly to Music Editor with the MusicXML file
                        NavigationService.Navigate(new MusicEditorPage(musicXmlFilePath));
                    }
                    else
                    {
                        MessageBox.Show("The selected file is not a valid MusicXML file.",
                            "Invalid File", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading MusicXML file: {ex.Message}",
                        "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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