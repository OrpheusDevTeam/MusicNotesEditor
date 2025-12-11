using Microsoft.Win32;
using MusicNotesEditor.LocalServer;
using MusicNotesEditor.ViewModels;
using Newtonsoft.Json;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MusicNotesEditor.Views
{
    public partial class FileArrangerPage : Page
    {
        private ObservableCollection<FileItem> fileItems = new ObservableCollection<FileItem>();
        private Point startPoint;
        private FileItem draggedItem;

        private readonly FileArrangerViewModel viewModel = new FileArrangerViewModel();

        public string[] SelectedFiles => fileItems.Select(f => f.FilePath).ToArray();

        private CertAndServer? _server;

        public FileArrangerPage()
        {
            InitializeComponent();
            filesListView.ItemsSource = fileItems;
            DataContext = viewModel;
            ConnectEurydice();

            Unloaded += FileArrangerPage_Unloaded;
        }

        private void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All Supported Files|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|PDF Files|*.pdf|Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff",
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

                    // Validate file exists and is accessible
                    if (!fileInfo.Exists)
                    {
                        MessageBox.Show($"File not found: {filePath}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    // Check file size (limit to 100MB to prevent memory issues)
                    if (fileInfo.Length > 100 * 1024 * 1024)
                    {
                        MessageBox.Show($"File too large: {filePath} ({FormatFileSize(fileInfo.Length)})\nMaximum size is 100MB.",
                            "File Too Large", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    var fileItem = new FileItem
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        FileType = Path.GetExtension(filePath).ToUpper().TrimStart('.'),
                        FileSize = FormatFileSize(fileInfo.Length),
                        Order = fileItems.Count + 1
                    };

                    fileItems.Add(fileItem);

                    // Generate thumbnail in background without blocking UI
                    _ = Task.Run(async () => await GenerateThumbnailAsync(fileItem));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading file {Path.GetFileName(filePath)}: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is FileItem fileItem)
            {
                try
                {
                    var previewWindow = new FilePreviewWindow(fileItem);
                    previewWindow.Owner = Window.GetWindow(this);
                    previewWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening preview: {ex.Message}", "Preview Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
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

        #region Thumbnail Generation - FIXED VERSION

        private async Task GenerateThumbnailAsync(FileItem fileItem)
        {
            try
            {
                // Set loading placeholder on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    fileItem.Thumbnail = CreateDefaultThumbnail("Loading...", Colors.LightGray);
                });

                var thumbnail = await Task.Run(() => CreateThumbnail(fileItem.FilePath));

                if (thumbnail != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        fileItem.Thumbnail = thumbnail;
                    });
                }
                else
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        fileItem.Thumbnail = CreateDefaultThumbnail("Error", Colors.LightPink);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating thumbnail for {fileItem.FileName}: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    fileItem.Thumbnail = CreateDefaultThumbnail("Error", Colors.LightPink);
                });
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
                    case ".bmp":
                    case ".gif":
                    case ".tiff":
                        return CreateImageThumbnail(filePath);
                    case ".pdf":
                        return CreatePdfThumbnail(filePath);
                    default:
                        return CreateDefaultThumbnail(extension.TrimStart('.'), Colors.LightBlue);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating thumbnail for {filePath}: {ex.Message}");
                return CreateDefaultThumbnail("Error", Colors.LightPink);
            }
        }

        private BitmapImage CreateImageThumbnail(string imagePath)
        {
            try
            {
                var bitmapImage = new BitmapImage();

                using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.PreservePixelFormat;
                    bitmapImage.StreamSource = fileStream;
                    bitmapImage.DecodePixelWidth = 120;
                    bitmapImage.DecodePixelHeight = 90;
                    bitmapImage.Rotation = Rotation.Rotate0;

                    // Handle different image formats and color spaces
                    if (Path.GetExtension(imagePath).ToLower() == ".tiff")
                    {
                        bitmapImage.CreateOptions |= BitmapCreateOptions.IgnoreColorProfile;
                    }

                    bitmapImage.EndInit();
                }

                // Ensure image is fully loaded and frozen for cross-thread access
                if (bitmapImage.IsDownloading)
                {
                    bitmapImage.DownloadCompleted += (s, e) => bitmapImage.Freeze();
                }
                else
                {
                    bitmapImage.Freeze();
                }

                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image {imagePath}: {ex.Message}");

                // Try alternative approach for problematic images
                return CreateImageThumbnailAlternative(imagePath);
            }
        }

        private BitmapImage CreateImageThumbnailAlternative(string imagePath)
        {
            try
            {
                // Alternative approach using BitmapFrame
                using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bitmapFrame = BitmapFrame.Create(
                        fileStream,
                        BitmapCreateOptions.None,
                        BitmapCacheOption.OnLoad);

                    var resizedBitmap = new TransformedBitmap(
                        bitmapFrame,
                        new ScaleTransform(
                            120.0 / bitmapFrame.PixelWidth,
                            90.0 / bitmapFrame.PixelHeight));

                    var bitmapImage = new BitmapImage();
                    using (var memoryStream = new MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(resizedBitmap));
                        encoder.Save(memoryStream);
                        memoryStream.Position = 0;

                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = memoryStream;
                        bitmapImage.EndInit();
                    }

                    bitmapImage.Freeze();
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Alternative image loading also failed for {imagePath}: {ex.Message}");
                return CreateDefaultThumbnail("Image", Colors.LightYellow);
            }
        }

        private BitmapImage CreatePdfThumbnail(string pdfPath)
        {
            try
            {
                // For PDF files, you'll need a PDF rendering library
                // For now, return a nice PDF placeholder
                return CreateDefaultThumbnail("PDF", Colors.LightCoral);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating PDF thumbnail for {pdfPath}: {ex.Message}");
                return CreateDefaultThumbnail("PDF", Colors.LightCoral);
            }
        }

        private BitmapImage CreateDefaultThumbnail(string text, Color backgroundColor)
        {
            try
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Draw background with subtle gradient
                    var gradient = new LinearGradientBrush(
                        Color.FromArgb(255, backgroundColor.R, backgroundColor.G, backgroundColor.B),
                        Color.FromArgb(255,
                            (byte)(backgroundColor.R * 0.8),
                            (byte)(backgroundColor.G * 0.8),
                            (byte)(backgroundColor.B * 0.8)),
                        45);

                    drawingContext.DrawRectangle(gradient, null, new Rect(0, 0, 100, 70));

                    // Draw border
                    drawingContext.DrawRectangle(null, new Pen(Brushes.DarkGray, 1), new Rect(0, 0, 100, 70));

                    // Draw text with better formatting
                    var formattedText = new FormattedText(
                        text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        10,
                        Brushes.Black,
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);

                    // Center the text
                    double x = (100 - formattedText.Width) / 2;
                    double y = (70 - formattedText.Height) / 2;
                    drawingContext.DrawText(formattedText, new Point(x, y));
                }

                var renderTarget = new RenderTargetBitmap(100, 70, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);

                // Convert to BitmapImage
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
                return CreateSimpleColorThumbnail(backgroundColor);
            }
        }

        private BitmapImage CreateSimpleColorThumbnail(Color color)
        {
            try
            {
                var renderTarget = new RenderTargetBitmap(100, 70, 96, 96, PixelFormats.Pbgra32);
                var drawingVisual = new DrawingVisual();

                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 100, 70));
                }

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
            catch
            {
                // Ultimate fallback - return null, UI should handle this
                return null;
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
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            fileItems.Clear();
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

            // Create cancellation token source
            _processingCancellationTokenSource = new CancellationTokenSource();

            // Get the ordered file paths
            string[] orderedFiles = SelectedFiles;

            try
            {
                // Show loading overlay
                ShowLoadingOverlay("Preparing files...", showCancelButton: true);

                // Disable UI during processing
                SetProcessingUI(true);

                // Create progress reporter for Python processing
                IProgress<string> progress = new Progress<string>(message =>
                {
                    // Parse progress messages from Python script
                    if (message.Contains("progress:"))
                    {
                        UpdateLoadingProgress("Processing files...", message);
                    }
                    else if (message.Contains("Converting"))
                    {
                        UpdateLoadingProgress(message, "");
                    }
                    else if (message.Contains("Merging"))
                    {
                        UpdateLoadingProgress(message, "");
                    }
                    else if (message.Contains("Finalizing"))
                    {
                        UpdateLoadingProgress(message, "");
                    }
                });

                // Process files with Python (with cancellation support)
                string pythonResult = await Task.Run(async () =>
                {
                    try
                    {
                        return await App.SubProcessService.ProcessFilesWithPythonAsync(
                            orderedFiles,
                            progress,
                            _processingCancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return "{\"status\":\"cancelled\",\"error\":\"Operation was cancelled\"}";
                    }
                });

                // Check if operation was cancelled
                if (_processingCancellationTokenSource.Token.IsCancellationRequested)
                {
                    UpdateLoadingProgress("Operation cancelled", "Cleaning up...");
                    await Task.Delay(1000); // Brief delay for clean up message
                    HideLoadingOverlay();
                    SetProcessingUI(false);
                    return;
                }

                Console.WriteLine(pythonResult);

                // Clean the Python output
                string cleanJson = ExtractJsonFromOutput(pythonResult);
                Console.WriteLine(cleanJson);

                // Update loading status
                UpdateLoadingProgress("Finalizing output...");

                dynamic results = JsonConvert.DeserializeObject(cleanJson);
                Console.WriteLine(results);

                // Access properties safely
                string status = results.status;
                if (status == "success")
                {
                    string filePath = results.filepath;
                    Console.WriteLine($"Status: {status}, FilePath: {filePath}");

                    // Update loading status before navigation
                    UpdateLoadingProgress("Success! Redirecting...");
                    await Task.Delay(500); // Brief delay for user to see success message

                    NavigationService.Navigate(new MusicEditorPage(filePath));
                }
                else
                {
                    string error = results.error;
                    Console.WriteLine($"Status: {status}, Error: {error}");

                    // Show error message
                    HideLoadingOverlay();
                    MessageBox.Show($"Error processing files: {error}",
                        "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();

                string errorMessage = ex.Message;
                if (ex is JsonReaderException)
                {
                    errorMessage = "Invalid response from Python script. Please check the script configuration.";
                }
                else if (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    errorMessage = "Operation was cancelled.";
                }

                MessageBox.Show($"Error processing files: {errorMessage}\n\nPlease check that Python is installed and the script path is correct.",
                    "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide loading overlay and re-enable UI
                HideLoadingOverlay();
                SetProcessingUI(false);

                // Clean up cancellation token
                _processingCancellationTokenSource?.Dispose();
                _processingCancellationTokenSource = null;
            }
        }

        private string ExtractJsonFromOutput(string pythonOutput)
        {
            // If the Python script might output other text before/after JSON
            // Look for JSON array or object patterns
            int startIndex = pythonOutput.IndexOf('[');
            int endIndex = pythonOutput.LastIndexOf(']') + 1;

            if (startIndex >= 0 && endIndex > startIndex)
            {
                return pythonOutput.Substring(startIndex, endIndex - startIndex);
            }

            // If no array, look for object
            startIndex = pythonOutput.IndexOf('{');
            endIndex = pythonOutput.LastIndexOf('}') + 1;

            if (startIndex >= 0 && endIndex > startIndex)
            {
                return pythonOutput.Substring(startIndex, endIndex - startIndex);
            }

            // If no clear JSON structure, return as-is
            return pythonOutput;
        }


        private void SetProcessingUI(bool isProcessing)
        {
            // Disable main UI buttons
            btnProcess.IsEnabled = !isProcessing;
            btnSelectFiles.IsEnabled = !isProcessing;
            btnImportMusicXml.IsEnabled = !isProcessing;
            btnMoveUp.IsEnabled = !isProcessing;
            btnMoveDown.IsEnabled = !isProcessing;
            btnRemove.IsEnabled = !isProcessing;
            btnClearAll.IsEnabled = !isProcessing;

            // Disable the cancel button on the main UI during processing
            btnCancel.IsEnabled = !isProcessing;

            // Update button text
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
                    App.OpenFileService.TestData(musicXmlFilePath);
                    if (App.OpenFileService.ValidateMusicXmlWithXsd(musicXmlFilePath))
                    {
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


        

        private async void ConnectEurydice()
        {
            if (_server is null)
            {
                _server = new CertAndServer();

                _server.OnImageUploaded += Server_OnImageUploaded;
            }

            var json = await _server.StartServerAsync();

            var win = new QrConnectPage(json, _server);
            QR_Frame.Navigate(win);
        }

        private async void FileArrangerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_server != null)
            {
                await _server.StopServerAsync();
                _server = null;
            }
        }

        private void Server_OnImageUploaded(string path)
        {
            Dispatcher.Invoke(() =>
            {
                AddFiles(new[] { path });
            });
        }

        private CancellationTokenSource _processingCancellationTokenSource;

        private void ShowLoadingOverlay(string message = "Processing Files...", bool showCancelButton = false)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = message;
                ProgressText.Text = "";
                btnCancelProcessing.Visibility = showCancelButton ? Visibility.Visible : Visibility.Collapsed;

                // Start fade-in animation
                LoadingOverlay.Visibility = Visibility.Visible;

                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 0.9, // 90% opacity as requested
                    Duration = TimeSpan.FromSeconds(0.3),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                LoadingOverlay.BeginAnimation(OpacityProperty, fadeInAnimation);
            });
        }

        private void HideLoadingOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                // Start fade-out animation
                var fadeOutAnimation = new DoubleAnimation
                {
                    From = LoadingOverlay.Opacity,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.3),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                fadeOutAnimation.Completed += (s, e) =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                };

                LoadingOverlay.BeginAnimation(OpacityProperty, fadeOutAnimation);
            });
        }

        private void UpdateLoadingProgress(string status, string progress = "")
        {
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = status;
                if (!string.IsNullOrEmpty(progress))
                {
                    ProgressText.Text = progress;
                }
            });
        }

        private void BtnCancelProcessing_Click(object sender, RoutedEventArgs e)
        {
            _processingCancellationTokenSource?.Cancel();
            UpdateLoadingProgress("Cancelling...");
        }

        private void BtnIncrease_Click(object sender, RoutedEventArgs e)
        {
            viewModel.NumberOfParts++;
        }

        private void BtnDecrease_Click(object sender, RoutedEventArgs e)
        {
            viewModel.NumberOfParts--;
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