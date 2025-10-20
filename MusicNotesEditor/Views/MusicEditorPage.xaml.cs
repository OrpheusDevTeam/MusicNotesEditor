using Manufaktura.Controls.Model;
using Manufaktura.Controls.WPF;
using Manufaktura.Music.Model;
using MusicNotesEditor.Models;
using MusicNotesEditor.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicNotesEditor.Views
{
    /// <summary>
    /// Interaction logic for MusicEditorPage.xaml
    /// </summary>
    public partial class MusicEditorPage : Page
    {
        private readonly MusicEditorViewModel viewModel = new MusicEditorViewModel();
        private readonly Canvas mainCanvas;

        TextBlock noteCursor;
        public MusicEditorPage()
        {
            InitializeComponent();
            GenerateNoteButtons();

            DataContext = viewModel;
            viewModel.LoadTestData();

            mainGrid.SizeChanged += MainGrid_SizeChanged;
            noteViewer.MouseLeftButtonDown += NoteViewer_Debug;

            mainCanvas = (Canvas)noteViewer.FindName("MainCanvas");
            noteViewer.MouseEnter += Canvas_MouseEnter;
            noteViewer.MouseLeave += Canvas_MouseLeave;
            noteViewer.MouseMove += Canvas_MouseMove;

            noteCursor = new TextBlock
            {
                FontSize = 26,
                Foreground = Brushes.Blue,
                FontFamily = (FontFamily)FindResource("Polihymnia")
            };

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
            if (noteViewer.SelectedElement != null)
            {
                Console.WriteLine($"Selected element: {noteViewer.SelectedElement} Location: {noteViewer.SelectedElement.RenderedWidth} Type:{noteViewer.SelectedElement.GetType()}");
            }
            if (noteViewer.SelectedElement is StaffFragment)
            {
                StaffFragment fragment = noteViewer.SelectedElement as StaffFragment;
                Console.WriteLine($"Fragment: {string.Join(", ", fragment.LinePositions)}");
            }
            Console.WriteLine("\nAll elements\n:");

            var staves = noteViewer.ScoreSource.Staves;

            for (int i=0; i < staves.Count; i++)
            {
                var elements = staves[i].Elements;
                for (int j = 0; j < elements.Count; j++)
                {
                    Console.WriteLine($"\tStave: {i + 1} Element: {j+1}. {elements[j]} Location: {elements[j].ActualRenderedBounds}");
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
                noteViewer.IsSelectable = true;
            }
            else
            {
                viewModel.CurrentNote = note;
                noteViewer.IsSelectable = false;
            }

            foreach (ToggleButton noteButton in notesButtons)
            {
                if (noteButton == null)
                    continue;
                RhythmicDuration buttonDuration = (RhythmicDuration)noteButton.Tag;

                noteButton.IsChecked = buttonDuration == viewModel.CurrentNote;
            }
            
        }

        private void Canvas_MouseEnter(object sender, MouseEventArgs e)
        {
            noteCursor.Visibility = Visibility.Visible;
            mainCanvas.Children.Add(noteCursor);

            noteCursor.Text = NoteDuration.SmuflCharFromDuration(viewModel.CurrentNote);
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            noteCursor.Visibility = Visibility.Collapsed;
            mainCanvas.Children.Remove(noteCursor);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Update letter position to follow the mouse
            var pos = e.GetPosition(mainCanvas);
            Canvas.SetLeft(noteCursor, pos.X - noteCursor.ActualWidth/2);
            Canvas.SetTop(noteCursor, pos.Y - noteCursor.ActualHeight/2);
        }

    }
}
