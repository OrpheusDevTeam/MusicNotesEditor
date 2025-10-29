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
using System.Windows.Navigation;

namespace MusicNotesEditor.Views
{
    /// <summary>
    /// Interaction logic for MusicEditorPage.xaml
    /// </summary>
    public partial class MusicEditorPage : Page
    {
        const int SNAPPING_THRESHOLD = 5;

        private readonly MusicEditorViewModel viewModel = new MusicEditorViewModel();
        private readonly Canvas mainCanvas;

        TextBlock noteIndicator;
        public MusicEditorPage(string filepath = "")
        {
            InitializeComponent();
            GenerateNoteButtons();

            DataContext = viewModel;

            try
            {
                if (string.IsNullOrEmpty(filepath))
                {
                    viewModel.LoadInitialData();
                    /* 
                     * Part of initializing data has to be moved to first render
                     * as it needs rendering bounds of barlines to put system breaks correctly
                    */
                    CompositionTarget.Rendering += InitializeDataOnFirstRender;
                }

                else
                    viewModel.LoadData(filepath);
            }
            catch (Exception ex)
            {
                // Should navigate back to main menu with displaying error, should catch different exceptions
                Console.WriteLine("Loading Error:");
                Console.WriteLine(ex);
                viewModel.LoadInitialData();
            }

            mainGrid.SizeChanged += MainGrid_SizeChanged;
            noteViewer.MouseLeftButtonDown += NoteViewer_Debug;

            mainCanvas = (Canvas)noteViewer.FindName("MainCanvas");
            noteViewer.MouseEnter += Canvas_MouseEnter;
            noteViewer.MouseLeave += Canvas_MouseLeave;
            noteViewer.MouseMove += Canvas_MouseMove;
            
            noteIndicator = new TextBlock
            {
                FontSize = 26,
                Foreground = Brushes.Blue,
                FontFamily = (FontFamily)FindResource("Polihymnia")
            };

        }

        private void InitializeDataOnFirstRender(object? sender, EventArgs e)
        {
            viewModel.NoteViewerContentWidth = NoteViewerContentWidth();
            viewModel.NoteViewerContentHeight = NoteViewerContentHeight();
            CompositionTarget.Rendering -= InitializeDataOnFirstRender;
            viewModel.LoadInitialTemplate();
        }


        private double NoteViewerContentWidth()
        {
            return (noteViewer.Width - 
                (noteViewer.Padding.Right + noteViewer.Padding.Left))
                / noteViewer.ZoomFactor;
        }

        private double NoteViewerContentHeight()
        {
            return (noteViewer.Height -
                (noteViewer.Padding.Top + noteViewer.Padding.Bottom))
                / noteViewer.ZoomFactor;
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
                    new RelayCommand(() =>
                    {
                        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    ),
                    note.Shortcut);
            }
        }


        private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double containerWidth = mainGrid.ActualWidth;

            noteViewer.Width = containerWidth * 0.5f;

            noteViewer.Height = noteViewer.Width * 1.414;
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
            noteIndicator.Visibility = Visibility.Visible;
            mainCanvas.Children.Add(noteIndicator);

            noteIndicator.Text = NoteDuration.SmuflCharFromDuration(viewModel.CurrentNote);
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            noteIndicator.Visibility = Visibility.Collapsed;
            mainCanvas.Children.Remove(noteIndicator);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Update letter position to follow the mouse
            var pos = e.GetPosition(mainCanvas); 
            Canvas.SetLeft(noteIndicator, pos.X - noteIndicator.ActualWidth / 2);
            
            var staffLinesPosition = GetStaffLinesPositions(viewModel.Data);
            
            double closest = staffLinesPosition.OrderBy(v => Math.Abs(v - pos.Y)).First();
            double distanceToClosestLine = Math.Abs(closest - pos.Y);

            
            if (distanceToClosestLine < SNAPPING_THRESHOLD )
            {
                Canvas.SetTop(noteIndicator, closest - noteIndicator.ActualHeight / 2);
            }
            else
            {
                Canvas.SetTop(noteIndicator, pos.Y - noteIndicator.ActualHeight / 2);
            }
        }


        private List<double> GetStaffLinesPositions(Score score)
        {
            var linesPositions = new List<double>();
            var staffSystems = score.Systems;

            foreach (StaffSystem system in  staffSystems)
            {
                foreach(var lines in system.LinePositions.Values)
                {
                    linesPositions.AddRange( AddValuesInBetween(lines) );
                }
            }

            return linesPositions;
        }

        private double[] AddValuesInBetween(double[] values)
        {
            return values.SelectMany((v, i) => i < values.Length - 1
                    ? new[] { v, (v + values[i + 1]) / 2.0 }
                    : new[] { v })
                .ToArray();
        }


        private void NoteViewer_Loaded(object sender, RoutedEventArgs e)
        {

        }

        // Function for printing debug information about note viewer
        // Runs when it's clicked. It will be messy. Delete after finishing note viewer.
        private void NoteViewer_Debug(object sender, MouseButtonEventArgs e)
        {

            var staves = viewModel.Data.Staves;
            var systems = viewModel.Data.Systems;
            var parts = viewModel.Data.Parts;
            var pages = viewModel.Data.Pages;

            Console.WriteLine($"Staves: {staves.Count}");
            Console.WriteLine($"Systems: {systems.Count}");
            Console.WriteLine($"Parts: {parts.Count}");
            Console.WriteLine($"Pages: {pages.Count}");

            Console.WriteLine("XD");

            var barlines = viewModel.Data.FirstStaff.Elements.OfType<Barline>();
            double lastBarlineYPosition = barlines.Last().ActualRenderedBounds.SE.X;

            Console.WriteLine($"\nLast Barline: {lastBarlineYPosition}");
            Console.WriteLine($"Bounds: {mainCanvas.ActualWidth}");

            barlines = viewModel.Data.FirstStaff.Elements.OfType<Barline>();
            lastBarlineYPosition = barlines.Last().ActualRenderedBounds.SE.X;

            Console.WriteLine($"\nLast Barline2: {lastBarlineYPosition}");
            Console.WriteLine($"Bounds2: {noteViewer.Width}");
            Console.WriteLine($"Bounds3: {NoteViewerContentWidth}");


            foreach (var staff in viewModel.Data.Staves)
            {
                foreach (var measure in staff.Measures)
                {
                    Console.WriteLine(measure.ToString());
                }
            }


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

            //var staves = noteViewer.ScoreSource.Staves;

            //for (int i = 0; i < staves.Count; i++)
            //{
            //    var elements = staves[i].Elements;
            //    for (int j = 0; j < elements.Count; j++)
            //    {
            //        Console.WriteLine($"\tStave: {i + 1} Element: {j + 1}. {elements[j]} Location: {elements[j].ActualRenderedBounds}");
            //        var element = elements[j] as Note;
            //        if(element != null)
            //        {
            //            //element.Pitch = Pitch.A4;
            //        }
            //    }
            //}

            Console.WriteLine("\n\n");
        }

    }
}
