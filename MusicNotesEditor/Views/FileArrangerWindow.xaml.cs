using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
            UpdateFileCount();
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
    }

    public class FileItem
    {
        public int Order { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FileSize { get; set; }
    }
}