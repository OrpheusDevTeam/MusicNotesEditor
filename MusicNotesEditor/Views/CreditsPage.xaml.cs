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
    public partial class CreditsPage : Page
    {

        private readonly FileArrangerViewModel viewModel = new FileArrangerViewModel();

        public CreditsPage()
        {
            InitializeComponent();
        }

        private void ReturnToMenu(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MainMenuPage());
        }
    }

}
