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
using Microsoft.Win32;

namespace MusicNotesEditor.Views
{
    /// <summary>
    /// Interaction logic for MainMenuPage.xaml
    /// </summary>
    public partial class MainMenuPage : Page
    {
        public MainMenuPage()
        {
            InitializeComponent();
        }

        private void GoToMusicEditorPage(object sender, RoutedEventArgs e)
        {
            NavigationService nav = NavigationService.GetNavigationService(this);
            nav.Navigate(new MusicEditorPage());
        }

        private string SelectFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Configure the dialog
            openFileDialog.Title = "Select a file";
            openFileDialog.Filter = "MusicXML files (*.musicxml)|*.musicxml";
            openFileDialog.FilterIndex = 2;
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
            string filepath = SelectFile(sender, e);
            NavigationService nav = NavigationService.GetNavigationService(this);
            nav.Navigate(new MusicEditorPage(filepath));
        }
    }

}
