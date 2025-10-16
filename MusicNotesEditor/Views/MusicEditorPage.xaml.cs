using Manufaktura.Controls.Model;
using Manufaktura.Music.Model;
using MusicNotesEditor.Models;
using MusicNotesEditor.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace MusicNotesEditor.Views
{
    /// <summary>
    /// Interaction logic for MusicEditorPage.xaml
    /// </summary>
    public partial class MusicEditorPage : Page
    {
        private readonly MusicEditorViewModel viewModel = new MusicEditorViewModel();
        public MusicEditorPage()
        {
            InitializeComponent();
            GenerateNoteButtons();

            DataContext = viewModel;
            viewModel.LoadTestData();

            mainGrid.SizeChanged += MainGrid_SizeChanged;
            noteViewer.MouseLeftButtonDown += NoteViewer_Debug;
        }


        private void GenerateNoteButtons()
        {
            foreach (var note in NoteDuration.AvailableNotes)
            {
                var tooltip = new ToolTip
                {
                    Content = new TextBlock
                    {
                        Inlines =
                        {
                            new Run(note.NoteName) { FontWeight = FontWeights.Bold },
                            new Run($"\n{note.Description}")
                        }
                    }
                };

                var btn = new ToggleButton
                {
                    Content = note.SmuflChar,
                    ToolTip = tooltip,
                    Style = NoteToolbar.Resources["ToolBarButtonStyle"] as Style,
                    Tag = note.Duration,
                    Margin = new Thickness(2),
                };

                btn.Click += (s, e) => ToggleNote(note.Duration);

                var gesture = note.Shortcut;
                var command = new RoutedCommand();
                command.InputGestures.Add(gesture);

                NoteToolbar.Items.Add(btn);

                var keyBinding = new KeyBinding(
                    new RelayCommand(() => {
                        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                    ),
                    note.Shortcut);

                this.InputBindings.Add(keyBinding);
            }
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

        private void ToggleNote(RhythmicDuration note)
        {
            var notesButtons = NoteToolbar.Items;

            if (viewModel.CurrentNote == note)
            {
                viewModel.CurrentNote = null;
            }
            else
            {
                viewModel.CurrentNote = note;
            }

            foreach (ToggleButton noteButton in notesButtons)
            {
                if (noteButton == null)
                    continue;
                RhythmicDuration buttonDuration = (RhythmicDuration)noteButton.Tag;

                noteButton.IsChecked = buttonDuration == viewModel.CurrentNote;
            }
            
        }

    }
}
