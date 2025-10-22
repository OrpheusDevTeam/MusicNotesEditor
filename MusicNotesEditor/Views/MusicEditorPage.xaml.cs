using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using MusicNotesEditor.ViewModels;

namespace MusicNotesEditor.Views
{
    /// <summary>
    /// Interaction logic for MusicEditorPage.xaml
    /// </summary>
    public partial class MusicEditorPage : Page
    {
        public MusicEditorPage()
        {
            InitializeComponent();

            var viewModel = new MusicEditorViewModel();
            DataContext = viewModel;
            viewModel.LoadTestData();

            mainGrid.SizeChanged += MainGrid_SizeChanged;
            noteViewer.MouseLeftButtonDown += NoteViewer_Debug;
        }
        public MusicEditorPage(string filepath)
        {
            InitializeComponent();

            var viewModel = new MusicEditorViewModel();
            DataContext = viewModel;
            try
            {
                viewModel.LoadData(filepath);
            }
            catch (Exception e)
            {
                viewModel.LoadTestData();
            }

            mainGrid.SizeChanged += MainGrid_SizeChanged;
            noteViewer.MouseLeftButtonDown += NoteViewer_Debug;
        }
        private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double containerWidth = mainGrid.ActualWidth;

            noteViewer.Width = containerWidth * 0.5f;

            noteViewer.Height = noteViewer.Width * 1.414;
        }

        private void NoteViewer_Debug(object sender, MouseButtonEventArgs e)
        {

            Console.WriteLine($"Selected element: {noteViewer.SelectedElement}");

            Console.WriteLine("\nAll elements\n:");

            var staves = noteViewer.ScoreSource.Staves;

            for (int i=0; i < staves.Count; i++)
            {
                var elements = staves[i].Elements;
                for (int j = 0; j < elements.Count; j++)
                {
                    Console.WriteLine($"\tStave: {i + 1} Element: {j+1}. {elements[j]}");
                }
            }

            Console.WriteLine("\n\n");
        }

        private void noteViewer_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
