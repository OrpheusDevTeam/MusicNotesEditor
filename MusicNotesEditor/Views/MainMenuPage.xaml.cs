using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Manufaktura.Controls.Model;
using Microsoft.Win32;
using MusicNotesEditor.ViewModels;

namespace MusicNotesEditor.Views
{
    /// <summary>
    /// Interaction logic for MainMenuPage.xaml
    /// </summary>
    public partial class MainMenuPage : Page
    {

        private readonly MainMenuViewModel viewModel = new MainMenuViewModel();

        public MainMenuPage()
        {
            InitializeComponent();
        }

        private void GoToMusicEditorPage(object sender, RoutedEventArgs e)
        {
            NavigationService nav = NavigationService.GetNavigationService(this);
            nav.Navigate(new MusicEditorPage());
        }

        private string SelectXMLs(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Configure the dialog
            openFileDialog.Title = "Select a file";
            openFileDialog.Filter = "MusicXML files (*.musicxml)|*.musicxml|XML files (*.xml)|*xml|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Multiselect = false;

            // Show the dialog
            bool? result = openFileDialog.ShowDialog();

            // Process the result
            if (result == true)
            {
                return openFileDialog.FileName;
            }

            return "";
        }

        private void SelectMusicXMLFile(object sender, RoutedEventArgs e)
        {
            string filepath = SelectXMLs(sender, e);
            DataContext = viewModel;
            try
            {
                viewModel.TestData(filepath);
                if (viewModel.ValidateMusicXmlWithXsd(filepath) == true) {
                    NavigationService nav = NavigationService.GetNavigationService(this);
                    nav.Navigate(new MusicEditorPage(filepath));
                } 
                else
                {
                    MessageBox.Show("There was an error loading the chosen file, please ensure the format and content are follow the correct MusicXML standard.", "File import error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was an error loading the chosen file, please ensure the format and content are follow the correct MusicXML standard.", "File import error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectImageFiles(object sender, RoutedEventArgs e)
        {
            var fileArrangerWindow = new FileArrangerWindow();
            fileArrangerWindow.Owner = Application.Current.MainWindow;

            bool? result = fileArrangerWindow.ShowDialog();

            if (result == true && fileArrangerWindow.SelectedFiles?.Any() == true)
            {
                string[] orderedFiles = fileArrangerWindow.SelectedFiles;
                ProcessOrderedFiles(orderedFiles);
            }
        }

        private void ProcessOrderedFiles(string[] orderedFiles)
        {
            DataContext = viewModel;

            // Here you can add your specific processing logic
            // For example, you might want to navigate to a processing page:
            // NavigationService.Navigate(new ImageToMusicXmlPage(orderedFiles));

            MessageBox.Show($"Successfully arranged {orderedFiles.Length} files!\n\n" +
                           "Files will be processed in this order:\n" +
                           string.Join("\n", orderedFiles.Select((f, i) => $"{i + 1}. {System.IO.Path.GetFileName(f)}")),
                           "Files Ready for Processing",
                           MessageBoxButton.OK, MessageBoxImage.Information);

            NavigationService nav = NavigationService.GetNavigationService(this);
            nav.Navigate(new MusicEditorPage());
        }
    }

}
