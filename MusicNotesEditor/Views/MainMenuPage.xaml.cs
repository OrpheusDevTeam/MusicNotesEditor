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


        public void SelectMusicXMLFile(object sender, RoutedEventArgs e)
        {
            App.OpenFileService.SelectMusicXMLFile(NavigationService.GetNavigationService(this));
        }


        private void SelectImageFiles(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new FileArrangerPage());
        }

        private void OpenCredits(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new CreditsPage());
        }
    }

}
