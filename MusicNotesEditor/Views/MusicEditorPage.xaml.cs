using Manufaktura.Controls.Model;
using Manufaktura.Controls.Parser;
using Manufaktura.Controls.WPF;
using Manufaktura.Controls.WPF.Renderers;
using Manufaktura.Music.Model;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Win32;
using MusicNotesEditor.Helpers;
using MusicNotesEditor.Models;
using MusicNotesEditor.Models.Framework;
using MusicNotesEditor.Services.SaveFile;
using MusicNotesEditor.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Xml.Linq;

namespace MusicNotesEditor.Views
{
    /// <summary>
    /// Interaction logic for MusicEditorPage.xaml
    /// </summary>
    public partial class MusicEditorPage : Page
    {
        private const string SOFTWARE_NAME = "Orpheus v1.0";

        private double noteViewerWidthPercentage = 0.6;
        private readonly MusicEditorViewModel viewModel = new MusicEditorViewModel();
        private readonly Canvas mainCanvas;

        TextBlock noteIndicator;
        List<TextBlock> staffLineIndicators;
        Stopwatch stopwatch = new Stopwatch();

        public MusicEditorPage(string filepath = "")
        {
            stopwatch.Start();
            try
            {
                NavigationCommands.BrowseBack.InputGestures.Clear();
                NavigationCommands.BrowseForward.InputGestures.Clear();

                InitializeComponent();
                GenerateNoteButtons();

                DataContext = viewModel;
                viewModel.XmlPath = filepath;
                viewModel.noteViewer = noteViewer;

                if (string.IsNullOrEmpty(filepath))
                {
                    viewModel.LoadInitialData();
                }
                else
                {
                    viewModel.LoadData(filepath);
                }

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
            catch (Exception ex)
            {
                Console.WriteLine("Loading Error:");
                Console.WriteLine(ex);
                viewModel.LoadInitialData();
            }
            finally
            {

            }
        }

        private void InitializeDataOnFirstRender(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= InitializeDataOnFirstRender;

            viewModel.NoteViewerContentWidth = NoteViewerContentWidth();
            viewModel.NoteViewerContentHeight = NoteViewerContentHeight();
            viewModel.LoadInitialTemplate();
            ScoreAdjustHelper.AdjustWidth(viewModel.Data, NoteViewerContentWidth(), NoteViewerContentHeight(), viewModel.CurrentPageIndex);
            ScoreAdjustHelper.AdjustWidth(viewModel.Data, NoteViewerContentWidth(), NoteViewerContentHeight(), viewModel.CurrentPageIndex);

            MeasureHelper.ValidateMeasures(viewModel.Data, noteViewer);
            stopwatch.Stop();
            var totalTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"\n??????????????\n^^^^^^^^^^^^^^^^\nMusicEditorPage constructor completed in {totalTime}ms\n??????????????\n^^^^^^^^^^^^^^^^\n");
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
                var keyboardNoteCommand = new RelayCommand(
                    execute: () => ToggleNote(note.Duration, true)
                );
                var noteCommand = new RelayCommand(
                    execute: () => ToggleNote(note.Duration, false)
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
                        keyboardNoteCommand,
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

            noteViewer.Height = noteViewer.Width * 1.414 * viewModel.AdditionalPageHeight;

            viewModel.NoteViewerContentWidth = NoteViewerContentWidth();
            viewModel.NoteViewerContentHeight = NoteViewerContentHeight();
        }


        public void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.TogglePlayback();
        }

        public void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.TogglePause();
        }


        public void SaveNewMusicXML(object sender, RoutedEventArgs e)
        {
            string filter = "MusicXML Files (*.musicxml)|*.musicxml";
            SaveFileDialog dialog = new SaveFileDialog { Filter = filter };
            string filePath = dialog.ShowDialog() == true ? dialog.FileName : null;

            if (filePath == null) return;

            bool success = App.FileSaveService.SaveMusicXMLInternal(viewModel.Data, filePath);

            if (success && filePath != viewModel.XmlPath)
            {
                viewModel.XmlPath = filePath;
                viewModel.ScoreFileName = Path.GetFileName(viewModel.XmlPath);
            }
        }

        public void SaveMusicXML(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(viewModel.XmlPath))
            {
                SaveNewMusicXML(sender, e);
                return;
            }

            App.FileSaveService.SaveMusicXMLInternal(viewModel.Data, viewModel.XmlPath);
        }


        public void OpenMusicXML(object sender, RoutedEventArgs e)
        {
            App.OpenFileService.SelectMusicXMLFile(NavigationService.GetNavigationService(this));
        }


        public void ExportToSvg(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }


        public async void ExportToPdf(object sender, RoutedEventArgs e)
        {
            string filter = "PDF Files (*.pdf)|*.pdf";

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = filter
            };

            string filePath = dialog.ShowDialog() == true ? dialog.FileName : null;
            string tempPath = "";

            if (!string.IsNullOrWhiteSpace(viewModel.XmlPath))
            {
                SaveMusicXML(sender, e);
            }
            else
            {
                tempPath = Path.Combine(Path.GetTempPath(), $"temp_music_{Guid.NewGuid()}.musicxml");
                bool success = App.FileSaveService.SaveMusicXMLInternal(viewModel.Data, tempPath);
            }

            try
            {
                var progress = new Progress<string>();
                if (filePath == null)
                    return;

                string musicXMLPath = !string.IsNullOrWhiteSpace(tempPath) ? tempPath : viewModel.XmlPath;

                string arguments = $"{musicXMLPath} {filePath}";

                string results = await App.SubProcessService.ExecuteJavaScriptScriptAsync("script.js", arguments, progress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving XML: {ex.Message}");
            }

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
            if (viewModel.CurrentLyrics != null)
            {
                if (e.Key == System.Windows.Input.Key.Delete)
                {
                    viewModel.RemoveCharacterFromLyrics(true);
                    return;
                }

                if (e.Key == System.Windows.Input.Key.Back)
                {
                    viewModel.RemoveCharacterFromLyrics();
                    return;
                }

                if (e.Key == System.Windows.Input.Key.Left)
                {
                    viewModel.JumpToNextSyllable(isNewWord: true, jumpToPrevious: true, changeSyllablesType: false);
                    return;
                }

                if (e.Key == System.Windows.Input.Key.Right)
                {
                    viewModel.JumpToNextSyllable(isNewWord: true, jumpToPrevious: false, changeSyllablesType: false);
                    return;
                }

                if (e.Key == System.Windows.Input.Key.Space)
                {
                    viewModel.JumpToNextSyllable(isNewWord: true);
                    return;
                }

                if (e.Key == System.Windows.Input.Key.OemMinus)
                {
                    // hyphen means same word, continue
                    viewModel.JumpToNextSyllable(isNewWord: false);
                    return;
                }

                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    viewModel.StopTypingLyrics();
                    return;
                }

                // Normal character input
                string? typed = null;
                char c = (char)KeyInterop.VirtualKeyFromKey(e.Key);
                if (char.IsLetterOrDigit(c))
                    typed = c.ToString().ToLower();

                if (!string.IsNullOrEmpty(typed))
                {
                    viewModel.AddCharacterToLyrics(typed);
                    return;
                }

                return;
            }

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
                case System.Windows.Input.Key.L:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        viewModel.StartTypingLyrics();
                    break;
            }
        }


        private void ToggleNote(RhythmicDuration note, bool inputFromKeyboard = false)
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
            if(inputFromKeyboard)
            {
                Canvas_MouseLeave(null, null);
                Canvas_MouseEnter(null, null);
                SnapToStaffLine(new Point(-1000,-1000));
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
            if(noteIndicator.Parent == null)
                mainCanvas.Children.Add(noteIndicator);
            foreach(var staffLineIndicator in staffLineIndicators)
            {
                if(staffLineIndicator.Parent == null)
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
            if (viewModel.CurrentNote == null)
                return;
            var pos = e.GetPosition(mainCanvas);
            SnapToStaffLine(pos);
        }

        private void SnapToStaffLine(Point pos)
        {

            // Update letter position to follow the mouse
            Canvas.SetLeft(noteIndicator, pos.X - noteIndicator.ActualWidth / 2);
            foreach (var staffLineIndicator in staffLineIndicators)
            {
                Canvas.SetLeft(staffLineIndicator, pos.X - staffLineIndicator.ActualWidth / 2);
            }

            var lineIndex = ScoreDataExtractor.GetStaffLineIndex(
                viewModel.Data,
                pos.Y,
                out List<double> closestLinePositions);

            if (lineIndex == -1 || viewModel.CurrentNote == null)
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
                if (lineIndex / 2 < additionalStaffLines)
                {
                    Canvas.SetTop(
                        staffLineIndicators[i],
                        closestLinePositions[i * 2] - staffLineIndicators[i].ActualHeight / 3.75);
                }
                else if ((closestLinePositions.Count - lineIndex) / 2 <= additionalStaffLines)
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


            var staves = viewModel.Data.Staves;
            var systems = viewModel.Data.Systems;
            var parts = viewModel.Data.Parts;
            var pages = viewModel.Data.Pages;
            
            Console.WriteLine($"Staves: {staves.Count}");
            Console.WriteLine($"Systems: {systems.Count}");
            Console.WriteLine($"Parts: {parts.Count}");
            Console.WriteLine($"Pages: {pages.Count}");

            foreach(var system in systems)
            {
                Console.WriteLine($"STAFF FRAGMENTS OF {system}: {string.Join(" | ", system.Staves)}");
            }

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
                    Console.WriteLine($"\tStave: {i + 1} Measure: Element: {j + 1}. {elements[j]} " +
                            $"Page: {elements[j].Measure.System.Page} and measure: {elements[j].Measure} and bounds: {elements[j].ActualRenderedBounds}");
                    //if (elements[j] is Note note)
                    //    Console.WriteLine($"Lyrics: {string.Join(" | ", note.Lyrics)}");

                }
            }

            //    Console.WriteLine("\n\n");
        }

        private void BackToMenu(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MainMenuPage());
        }
    }
}
