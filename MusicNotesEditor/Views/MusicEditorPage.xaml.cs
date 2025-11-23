using Manufaktura.Controls.Model;
using Manufaktura.Controls.WPF;
using Manufaktura.Controls.WPF.Renderers;
using Manufaktura.Music.Model;
using MusicNotesEditor.Helpers;
using MusicNotesEditor.Models;
using MusicNotesEditor.ViewModels;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
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

        private double noteViewerWidthPercentage = 0.6;
        private readonly MusicEditorViewModel viewModel = new MusicEditorViewModel();
        private readonly Canvas mainCanvas;

        TextBlock noteIndicator;
        List<TextBlock> staffLineIndicators;
        public MusicEditorPage(string filepath = "")
        {
            InitializeComponent();
            GenerateNoteButtons();

            DataContext = viewModel;
            viewModel.XmlPath = filepath;
            viewModel.noteViewer = noteViewer;

            try
            {
                if (string.IsNullOrEmpty(filepath))
                {
                    viewModel.LoadInitialData();
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

            /* 
             * Part of initializing data has to be moved to first render
             * as it needs rendering bounds  of barlines to put system breaks correctly
            */
            CompositionTarget.Rendering += InitializeDataOnFirstRender;

            mainGrid.SizeChanged += MainGrid_SizeChanged;
            noteViewer.MouseLeftButtonDown += NoteViewer_Debug;
            noteViewer.MouseLeftButtonDown += AddNote;

            mainCanvas = (Canvas)noteViewer.FindName("MainCanvas");

            noteViewer.MouseEnter += Canvas_MouseEnter;
            noteViewer.MouseLeave += Canvas_MouseLeave;
            noteViewer.MouseMove += Canvas_MouseMove;
            noteViewer.MouseMove += ElementDragging;

            noteViewer.MouseLeftButtonDown += Canvas_Click;
            noteViewer.MouseLeftButtonUp += Canvas_Release;

            noteIndicator = new TextBlock
            {
                FontSize = 26,
                Foreground = Brushes.Blue,
                FontFamily = (FontFamily)FindResource("Polihymnia")
            };

            int additionalStaffLines = App.Settings.AdditionalStaffLines.Value;
            staffLineIndicators = new List<TextBlock>(additionalStaffLines);

            for (int i = 0; i < additionalStaffLines; i++)
            {
                var staffLineIndicatorTemplate = new TextBlock
                {
                    Text = "\u00E0",
                    FontSize = 26,
                    Foreground = Brushes.Blue,
                    FontFamily = (FontFamily)FindResource("Polihymnia")
                };
                staffLineIndicators.Add(staffLineIndicatorTemplate);
            }

        }

        private void InitializeDataOnFirstRender(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= InitializeDataOnFirstRender;

            viewModel.NoteViewerContentWidth = NoteViewerContentWidth();
            viewModel.NoteViewerContentHeight = NoteViewerContentHeight();
            viewModel.LoadInitialTemplate();
            ScoreAdjustHelper.AdjustWidth(viewModel.Data, NoteViewerContentWidth());
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
            foreach (var note in NoteDurationData.AvailableNotes)
            {
                var tooltip = new ToolTip
                {
                    Content = new TextBlock
                    {
                        Inlines =
                {
                    new Run(note.NoteName) { FontWeight = FontWeights.Bold },
                    new Run($"\n{note.Description}"),
                }
                    }
                };

                // Create a command for this note
                var noteCommand = new RelayCommand(
                    execute: () => ToggleNote(note.Duration)
                );

                var btn = new ToggleButton
                {
                    Content = note.SmuflChar,
                    ToolTip = tooltip,
                    Style = NoteToolbar.Resources["ToolBarButtonStyle"] as Style,
                    Tag = note.Duration,
                    Margin = new Thickness(2),
                    Command = noteCommand
                };

                if (note.Shortcut != null)
                {
                    var inputBinding = new InputBinding(
                        noteCommand,
                        note.Shortcut
                    );

                    this.InputBindings.Add(inputBinding);
                }

                NoteToolbar.Items.Add(btn);
            }

            foreach (var accidental in AccidentalsData.AvailableAccidentals)
            {
                var tooltip = new ToolTip
                {
                    Content = new TextBlock
                    {
                        Inlines =
                {
                    new Run(accidental.AccidentalName) { FontWeight = FontWeights.Bold },
                    new Run($"\n{accidental.Description}"),
                }
                    }
                };

                // Create a command for this note
                var accidentalCommand = new RelayCommand(
                    execute: () =>
                    {
                        if(viewModel.IsNothingSelected)
                            ToggleAccidental(accidental.Alter);
                        else if(viewModel.IsNoteOrRestSelected)
                            ApplyAccidentals(accidental.Alter);
                    }
                );

                var btn = new ToggleButton
                {
                    Content = accidental.SmuflChar,
                    ToolTip = tooltip,
                    Style = NoteToolbar.Resources["ToolBarButtonStyle"] as Style,
                    Tag = accidental.Alter,
                    Margin = new Thickness(2),
                    Padding = new Thickness(0, -4, 0, 2),
                    FontSize = 28,
                    Command = accidentalCommand
                };

                if (accidental.Shortcut != null)
                {
                    var inputBinding = new InputBinding(
                        accidentalCommand,
                        accidental.Shortcut
                    );

                    this.InputBindings.Add(inputBinding);
                }

                AccidentalsToolbar.Items.Add(btn);

                // Skip toolbar for selected elements for rest
                if (accidental.Alter == 2)
                    continue;

                tooltip = new ToolTip
                {
                    Content = new TextBlock
                    {
                        Inlines =
                {
                    new Run(accidental.AccidentalName) { FontWeight = FontWeights.Bold },
                    new Run($"\n{accidental.Description}"),
                }
                    }
                };

                var btnForSelection = new ToggleButton
                {
                    Content = accidental.SmuflChar,
                    ToolTip = tooltip,
                    Style = NoteToolbar.Resources["ToolBarButtonStyle"] as Style,
                    Tag = accidental.Alter,
                    Margin = new Thickness(2),
                    Padding = new Thickness(0, -4, 0, 2),
                    FontSize = 28,
                    Command = accidentalCommand
                };

                AccidentalsSelectToolbar.Items.Add(btnForSelection);

            }

        }


        private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double containerWidth = mainGrid.ActualWidth;

            noteViewer.Width = containerWidth * noteViewerWidthPercentage;

            noteViewer.Height = noteViewer.Width * 1.414;

            viewModel.NoteViewerContentWidth = NoteViewerContentWidth();
            viewModel.NoteViewerContentHeight = NoteViewerContentHeight();
        }

        public void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.PlayScore();
        }

        public void Canvas_Click(object sender, MouseButtonEventArgs e)
        {
            if (viewModel.CurrentNote != null)
                return;

            FrameworkElement? element = e.OriginalSource as FrameworkElement;
            var ownershipDictionary = SelectionHelper.GetOwnershipDictionary(noteViewer);
            // Remember that for it to work non other method run on click can change score

            if (element == null || !ownershipDictionary.ContainsKey(element))
            {
                if (e.ClickCount == 2)
                {
                    viewModel.UnSelectElements();
                }
                return;
            }

            bool shiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            viewModel.SelectElement(ownershipDictionary[element], shiftPressed);
            var pos = e.GetPosition(mainCanvas);
            viewModel.DraggingStartPosition = pos.Y;
        }


        private void Canvas_Release(object sender, MouseButtonEventArgs e)
        {
            viewModel.DraggingStartPosition = null;
        }

        private void ElementDragging(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(mainCanvas);
            viewModel.DragElements(pos.Y);
        }

        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case System.Windows.Input.Key.Delete:
                    viewModel.DeleteSelectedElements();
                    break;
                case System.Windows.Input.Key.Escape:
                    viewModel.UnSelectElements();
                    break;
                case System.Windows.Input.Key.Insert:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        viewModel.DeleteLastMeasure();
                    else
                        viewModel.AddNewMeasure();
                    break;
            }
        }


        private void ToggleNote(RhythmicDuration note)
        {
            viewModel.UnSelectElements();
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

            noteIndicator.Text = NoteDurationData.SmuflCharFromDuration(viewModel.CurrentNote);
            if (viewModel.IsRest)
            {
                noteIndicator.Text = noteIndicator.Text.ToUpper();
            }
        }


        private void ToggleAccidental(int alter)
        {
            if(!viewModel.IsNothingSelected) 
                return;

            viewModel.UnSelectElements();
            var accidentalsButtons = AccidentalsToolbar.Items;

            viewModel.CurrentAccidental = alter;
          
            foreach (ToggleButton accidentalButton in accidentalsButtons)
            {
                if (accidentalButton == null)
                    continue;
                int accidental = (int)accidentalButton.Tag;

                accidentalButton.IsChecked = accidental == viewModel.CurrentAccidental;
            }
        }

        private void ApplyAccidentals(int alter)
        {   
            if (viewModel.IsNothingSelected)
                return;

            Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!Applying Alter: {alter}");
            viewModel.ApplyAccidentals(alter);

            var accidentalsSelectButtons = AccidentalsSelectToolbar.Items;

            foreach (ToggleButton accidentalButton in accidentalsSelectButtons)
            {
                if (accidentalButton == null)
                    continue;
                accidentalButton.IsChecked = false;
            }

        }

        private void AddNote(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(mainCanvas);
            viewModel.AddNote( pos.X, pos.Y);
            Canvas_MouseLeave(null, null);
            Canvas_MouseEnter(null, null);
        }

        private void Canvas_MouseEnter(object sender, MouseEventArgs e)
        {
            noteIndicator.Visibility = Visibility.Visible;
            mainCanvas.Children.Add(noteIndicator);
            foreach(var staffLineIndicator in staffLineIndicators)
            {
                mainCanvas.Children.Add(staffLineIndicator);
            }
            
            noteIndicator.Text = NoteDurationData.SmuflCharFromDuration(viewModel.CurrentNote);
            if(viewModel.IsRest)
            {
                noteIndicator.Text = noteIndicator.Text.ToUpper();
            }
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            noteIndicator.Visibility = Visibility.Collapsed;
            mainCanvas.Children.Remove(noteIndicator);
            foreach (var staffLineIndicator in staffLineIndicators)
            {
                mainCanvas.Children.Remove(staffLineIndicator);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Update letter position to follow the mouse
            var pos = e.GetPosition(mainCanvas); 
            Canvas.SetLeft(noteIndicator, pos.X - noteIndicator.ActualWidth / 2);
            foreach(var staffLineIndicator in staffLineIndicators)
            {
                Canvas.SetLeft(staffLineIndicator, pos.X - staffLineIndicator.ActualWidth / 2);
            }

            var lineIndex = ScoreDataExtractor.GetStaffLineIndex(
                viewModel.Data,
                pos.Y,
                out List<double> closestLinePositions);
            
            if(lineIndex == -1 || viewModel.CurrentNote == null)
            {
                Canvas.SetTop(noteIndicator, pos.Y - noteIndicator.ActualHeight / 2);
                foreach (var staffLineIndicator in staffLineIndicators)
                {
                    staffLineIndicator.Visibility = Visibility.Collapsed;
                }
                return;
            }

            var closest = closestLinePositions[lineIndex];
            double distanceToClosestLine = Math.Abs(closest - pos.Y);
            
            Canvas.SetTop(noteIndicator, closest - noteIndicator.ActualHeight / 2);
            var additionalStaffLines = App.Settings.AdditionalStaffLines.Value;

            for (int i = 0; i < additionalStaffLines; i++)
            {
                staffLineIndicators[i].Visibility = Visibility.Visible;
                if(lineIndex / 2 < additionalStaffLines)
                {
                    Canvas.SetTop(
                        staffLineIndicators[i],
                        closestLinePositions[i*2] - staffLineIndicators[i].ActualHeight / 3.75);
                }
                else if ( (closestLinePositions.Count - lineIndex) / 2 <= additionalStaffLines)
                {
                    var additionalLineIndex = closestLinePositions.Count - (i + 1) * 2;
                    Canvas.SetTop(
                        staffLineIndicators[i],
                        closestLinePositions[additionalLineIndex] - staffLineIndicators[i].ActualHeight / 4.5);
                }
                else
                {
                    staffLineIndicators[i].Visibility = Visibility.Collapsed;
                }
            }
            
        }


        private void NoteViewer_Loaded(object sender, RoutedEventArgs e)
        {

            ToggleAccidental(0);

        }

        // Function for printing debug information about note viewer
        // Runs when it's clicked. It will be messy. Delete after finishing note viewer.
        private void NoteViewer_Debug(object sender, MouseButtonEventArgs e)
        {

            //foreach(var ind in staffLineIndicators)
            //    {
            //        Console.WriteLine($"INDICATORXD: {ind} of text: {ind.Text} and size {ind.ActualWidth} and parent {ind.Parent}");

            //    }

            //    Console.WriteLine($"INDICATORXD: {noteIndicator} of text: {noteIndicator.Text} and size {noteIndicator.ActualWidth} and parent {noteIndicator.Parent}");


            //    var staves = viewModel.Data.Staves;
            //    var systems = viewModel.Data.Systems;
            //    var parts = viewModel.Data.Parts;
            //    var pages = viewModel.Data.Pages;

            //    Console.WriteLine($"Staves: {staves.Count}");
            //    Console.WriteLine($"Systems: {systems.Count}");
            //    Console.WriteLine($"Parts: {parts.Count}");
            //    Console.WriteLine($"Pages: {pages.Count}");

            //    Console.WriteLine("XD");

            //    var barlines = viewModel.Data.FirstStaff.Elements.OfType<Barline>();
            //    double lastBarlineYPosition = barlines.Last().ActualRenderedBounds.SE.X;

            //    Console.WriteLine($"\nLast Barline: {lastBarlineYPosition}");
            //    Console.WriteLine($"Bounds: {mainCanvas.ActualWidth}");

            //    barlines = viewModel.Data.FirstStaff.Elements.OfType<Barline>();
            //    lastBarlineYPosition = barlines.Last().ActualRenderedBounds.SE.X;

            //    Console.WriteLine($"\nLast Barline2: {lastBarlineYPosition}");
            //    Console.WriteLine($"Bounds2: {noteViewer.Width}");
            //    Console.WriteLine($"Bounds3: {NoteViewerContentWidth}");

            //    foreach (var staff in viewModel.Data.Staves)
            //    {
            //        foreach (var measure in staff.Measures)
            //        {
            //            Console.WriteLine(measure.ToString());
            //        }
            //    }

            //    if (noteViewer.SelectedElement != null)
            //    {
            //        Console.WriteLine($"Selected element: {noteViewer.SelectedElement} Location: {noteViewer.SelectedElement.RenderedWidth} Type:{noteViewer.SelectedElement.GetType()}");
            //    }
            //    if (noteViewer.SelectedElement is StaffFragment)
            //    {
            //        StaffFragment fragment = noteViewer.SelectedElement as StaffFragment;
            //        Console.WriteLine($"Fragment: {string.Join(", ", fragment.LinePositions)}");
            //    }
            //    Console.WriteLine("\nAll elements\n:");

            var staves2 = noteViewer.ScoreSource.Staves;
            for (int i = 0; i < staves2.Count; i++)
            {
                var elements = staves2[i].Elements;
                
                for (int j = 0; j < elements.Count; j++)
                {
                    Console.WriteLine($"\tStave: {i + 1} Measure: {elements[j].Measure} Element: {j + 1}. {elements[j]} " +
                            $"Location: {elements[j].ActualRenderedBounds}");
                    if (elements[j] is Note note)
                        Console.WriteLine($"Lyrics: {string.Join(" | ", note.Lyrics)}");

                }
            }

            //    Console.WriteLine("\n\n");
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MainMenuPage());
        }
    }
}
