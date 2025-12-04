using Manufaktura.Controls.Audio;
using Manufaktura.Controls.Desktop.Audio;
using Manufaktura.Controls.Model;
using Manufaktura.Controls.Model.Collections;
using Manufaktura.Controls.Model.Events;
using Manufaktura.Controls.Model.Rules;
using Manufaktura.Controls.Parser;
using Manufaktura.Controls.WPF;
using Manufaktura.Music.Model;
using Manufaktura.Music.Model.MajorAndMinor;
using MusicNotesEditor.Helpers;
using MusicNotesEditor.Models;
using MusicNotesEditor.Models.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace MusicNotesEditor.ViewModels
{
    class MusicEditorViewModel : ViewModel
    {
        private const int MAX_NUMBER_OF_STAVES = 5;
        private const string LYRICS_PLACEHOLDER = " |";

        private readonly List<Type> selectableSymbols = new List<Type>() { 
            typeof(NoteOrRest),
            typeof(Clef),
            typeof(TimeSignature),
        };

        public List<MusicalSymbol> SelectedSymbols = new List<MusicalSymbol>();
        public RhythmicDuration? CurrentNote = null;
        public int CurrentAccidental = 0;
        public double? DraggingStartPosition = null;
        public double NoteViewerContentWidth;
        public double NoteViewerContentHeight;
        public string XmlPath = "";
        public Lyrics? CurrentLyrics = null;

        public bool IsNoteOrRestSelected => SelectedSymbols.Any(symbol => symbol is NoteOrRest);
        public bool IsClefSelected => SelectedSymbols.Any(symbol => symbol is Clef);
        public bool IsTimeSignatureSelected => SelectedSymbols.Any(symbol => symbol is TimeSignature);
        public bool IsNothingSelected => !SelectedSymbols.Any();
        public bool IsRest => CurrentAccidental == 2;

        private string _scoreFileName;
        private ScorePlayer? player = null;
        private Score data;
        private int _currentPageIndex = 1;
        public Score Data
        {
            get { return data; }
            set { data = value; OnPropertyChanged(() => Data); }
        }

        public string ScoreFileName 
        {   
            get { return _scoreFileName; }
            set { _scoreFileName = value; OnPropertyChanged(() => ScoreFileName); }
        }

        public int CurrentPageIndex
        {
            get { return _currentPageIndex; }
            set { _currentPageIndex = value; OnPropertyChanged(() => CurrentPageIndex); }
        }

        public NoteViewer noteViewer;
        

        public void LoadInitialData()
        {
            LoadInitialData(1);
        }

        public void LoadInitialData(int numberOfParts)
        {
            if(numberOfParts < 1)
            {
                throw new InvalidOperationException("Can't create new score with less than 1 staff.");
            }
            else if (numberOfParts > MAX_NUMBER_OF_STAVES)
            {
                throw new InvalidOperationException($"Can't create new score with more than {MAX_NUMBER_OF_STAVES} staff.");
            }

            var score = new Score();
            score.DefaultPageSettings.DefaultStaffDistance = App.Settings.StaffDistance;
            score.DefaultPageSettings.DefaultSystemDistance = App.Settings.AdditionalStaffLines;

            for (int i = 0; i < numberOfParts; i++)
            {
                score.AddStaff(Clef.Treble, TimeSignature.CommonTime, Step.C, MajorAndMinorScaleFlags.MajorSharp);
                score.Staves[i].MeasureAddingRule = Staff.MeasureAddingRuleEnum.AddMeasuresManually;
                score.Staves[i].AddTimeSignature(TimeSignatureType.Numbers, 4, 4);
                score.Staves[i].AddBarline(BarlineStyle.None);
                var part = new Part(score.Staves[i]) { PartId = $"P{i+1}" };
                score.Parts.Add(part);
            }
        
            Data = score;
        }

        public void LoadInitialTemplate()
        {
            LoadInitialTemplate(1);
        }

        public void LoadInitialTemplate(int numberOfParts)
        {
            if (!string.IsNullOrEmpty(XmlPath))
            {
                //ScoreAdjustHelper.AdjustWidth(Data, NoteViewerContentWidth, CurrentPageIndex);
                ScoreFileName = Path.GetFileName(XmlPath);
                return;
            };
            ScoreFileName = "Untitled Score";


            int measuresInLine = Math.Max(
                App.Settings.DefaultInitialMeasures.Value / numberOfParts,
                App.Settings.MinimalInitialMeasurePerStaff.Value);

            for (int i = 0; i < numberOfParts; i++)
            {
                // Fill up stave line 
                for (int j = 0; j < measuresInLine; j++)
                {
                    Data.Staves[i].Add(new CorrectRest(RhythmicDuration.Whole));
                    Data.Staves[i].AddBarline(BarlineStyle.Regular);                    
                }

                RemoveLastN(Data.Staves[i].Elements, 1);
                Data.Staves[i].AddBarline(BarlineStyle.LightHeavy);
            }
        }


        public void LoadData(string filepath)
        {
            var parser = new MusicXmlParser();
            var score = parser.Parse(XDocument.Load(filepath));

            score.DefaultPageSettings.DefaultStaffDistance = App.Settings.StaffDistance;
            score.DefaultPageSettings.DefaultSystemDistance = App.Settings.AdditionalStaffLines;

            foreach (var staff in score.Staves)
            {
                staff.MeasureAddingRule = Staff.MeasureAddingRuleEnum.AddMeasuresManually;
                staff.Elements.Insert(3, new Barline(BarlineStyle.None));
                if(staff.Elements.Last() is Barline barline)
                {
                    barline.Style = BarlineStyle.LightHeavy;
                }
                for (int i = staff.Elements.Count - 1; i >= 0; i--)
                {
                    if (staff.Elements[i] is Rest rest)
                    {
                        staff.Elements[i] = new CorrectRest(rest.Duration);
                    }
                    //else if (staff.Elements[i] is TimeSignature)
                    //{
                    //    staff.Elements.Insert(i + 1, );
                    //}
                }
                ScoreAdjustHelper.FixMeasures(staff);
            }
            Data = score;
        }

        public void PlayScore()
        {

            //player.Tempo = Tempo.Andante;
            if(player != null)
                Console.WriteLine($"STATING IN MUSIC BEFORE {player.State}");
            if (player == null)
            {
                player = new MidiTaskScorePlayer(Data);
                Console.WriteLine(string.Join(", ", MidiTaskScorePlayer.AvailableDevices));
                player.PlayCueNotes = true;
                player.Play();
            }
            Console.WriteLine($"STATING IN MUSIC {player.State}");
            Console.WriteLine($"TEMPOING IN MUSIC {player.Tempo.BeatsPerMinute}");
            Console.WriteLine($"STATING IN MUSIC {player.PlayCueNotes}");
            Console.WriteLine($"CURRENT SYMBOL IN MUSIC {player.CurrentElement}");
            Console.WriteLine($"CURRENT POSITION IN MUSIC {player.CurrentPosition.PositionX}");
        }


        public void AddNote(double clickXPos, double clickYPos)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ScoreEditHelper.InsertNote(Data, clickXPos, clickYPos, NoteViewerContentWidth, CurrentNote, IsRest, CurrentAccidental, noteViewer, CurrentPageIndex);
            MeasureHelper.ValidateMeasures(Data, noteViewer);
            stopwatch.Stop();
            Console.WriteLine($"Execution Time: {stopwatch.ElapsedMilliseconds} ms");
        }


        public static void RemoveLastN<T>(ItemManagingCollection<T> collection, int n)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (n <= 0) return;

            int count = collection.Count;
            int removeCount = Math.Min(n, count);

            // remove from the end
            for (int i = 0; i < removeCount; i++)
            {
                collection.RemoveAt(collection.Count - 1);
            }
        }


        public static double HorizontalPosition(MusicalSymbol element)
        {
            var elementMostLeftPosition = element.ActualRenderedBounds.SW.X;
            var elementMostRightPosition = element.ActualRenderedBounds.SE.X;

            return (elementMostLeftPosition + elementMostRightPosition) / 2;
        }


        public void SelectElement(MusicalSymbol musicalSymbol, bool multiSelect)
        {
            Console.WriteLine($"SELECTING: {musicalSymbol}");

            if (CurrentNote != null || musicalSymbol == null || !IsSymbolSelectable(musicalSymbol)) 
                return;

            if( !IsNoteOrRestSelected )
            {
                multiSelect = false;
            }

            if(!multiSelect || !typeof(NoteOrRest).IsAssignableFrom(musicalSymbol.GetType()) )
            {
                UnSelectElements();
            }

            Console.WriteLine($"SELECTED ELEMENT!!!!!!!!!!!!!!!!!: {musicalSymbol}");     
            

            SelectionHelper.ColorSelectedElement(noteViewer, musicalSymbol);

            if(!SelectedSymbols.Contains(musicalSymbol))
                SelectedSymbols.Add(musicalSymbol);
            Console.WriteLine($"SELECTED ELEMENTS!!!!!!!!!!!!!!!!!: B: {string.Join(",", SelectedSymbols)}");
            NotifySelectionPropertiesChanged();
        }

        public void UnSelectElements(Func<MusicalSymbol, bool>? filter = null)
        {
            if(CurrentLyrics != null)
            {
                Console.WriteLine($"UNSELECTING LYRICS---------------: {CurrentLyrics.Text}");
                if (CurrentLyrics.Text == LYRICS_PLACEHOLDER)
                {
                    CurrentLyrics.Note.Lyrics.Clear();
                    ScoreEditHelper.Rerender(Data);
                }
                SelectionHelper.ColorElement(noteViewer, CurrentLyrics);
            }
            CurrentLyrics = null;
            if (filter == null)
            {
                Console.WriteLine($"UNSELECTING ALL!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                SelectionHelper.ColorElements(noteViewer, SelectedSymbols);
                SelectedSymbols.Clear();
            }
            else
            {
                var elementsToUnselect = SelectedSymbols.Where(filter).ToList();
                Console.WriteLine($"Unselecting {elementsToUnselect.Count} filtered elements");

                SelectionHelper.ColorElements(noteViewer, SelectedSymbols);

                foreach (var element in elementsToUnselect)
                {
                    SelectedSymbols.Remove(element);
                }
                 
            }
            NotifySelectionPropertiesChanged();
        }


        public void DeleteSelectedElements()
        {

            Console.WriteLine("DELETING!!!!!!!!!!!!!!");
            if (!IsNoteOrRestSelected)
                return;

            Console.WriteLine("DELETING AND PASSED!!!");

            bool shouldStillBeSelected = ScoreEditHelper.DeleteElements(SelectedSymbols);

            if(!shouldStillBeSelected)
                UnSelectElements();

            ScoreEditHelper.Rerender(Data);

            ScoreAdjustHelper.AdjustWidth(Data, NoteViewerContentWidth, CurrentPageIndex);
            SelectionHelper.ColorSelectedElements(noteViewer, SelectedSymbols);
            MeasureHelper.ValidateMeasures(Data, noteViewer);
        }


        public void ApplyAccidentals(int accidental)
        {
            foreach (var symbol in SelectedSymbols)
            {
                Console.WriteLine($"FOUND SYMBOL: {symbol}");
                if(symbol is Note note && accidental != 2)
                {
                    Console.WriteLine($"FOUND NOTE: {note}");
                    note.Pitch = AccidentalsData.AlterPitch(note.Pitch, accidental);
                    Console.WriteLine($"NOTE AFTER CHANGE: {note}");
                }
            }
            ScoreEditHelper.Rerender(Data, noteViewer, SelectedSymbols);
        }


        public void DragElements(double mouseYPosition)
        {
            if (!IsNoteOrRestSelected || DraggingStartPosition == null)
                return;

            var shift = (int)Math.Round(( (DraggingStartPosition ?? 0) - mouseYPosition) / DistanceBetweenLines());
            Console.WriteLine($"Dragging Element: {DraggingStartPosition} Mouse: {mouseYPosition} Shift: {shift}");

            if(shift == 0)
            {
                return;
            }

            foreach (var symbol in SelectedSymbols)
            {
                if(symbol is Note note)
                {
                    PitchHelper.ShiftPitch(note, shift);
                }
            }
            DraggingStartPosition = mouseYPosition;
            ScoreEditHelper.Rerender(Data, noteViewer, SelectedSymbols);
        }


        public void AddNewMeasure()
        {
            if(SelectedSymbols.Count == 0)
                MeasureHelper.AddMeasure(Data, NoteViewerContentWidth, CurrentPageIndex);
            else if (SelectedSymbols.Count == 1)
                MeasureHelper.AddMeasure(Data, NoteViewerContentWidth, CurrentPageIndex, SelectedSymbols[0]);

            SelectionHelper.ColorSelectedElements(noteViewer, SelectedSymbols);
            MeasureHelper.ValidateMeasures(Data, noteViewer);
        }


        public void DeleteLastMeasure()
        {
            if (Data.FirstStaff.Measures.Count < 3 || (!IsNoteOrRestSelected && !IsNothingSelected))
                return;

            if (SelectedSymbols.Count == 0)
                MeasureHelper.DeleteMeasure(Data, NoteViewerContentWidth, CurrentPageIndex);
            else if (SelectedSymbols.Count == 1)
                MeasureHelper.DeleteMeasure(Data, NoteViewerContentWidth, CurrentPageIndex, SelectedSymbols[0]);

            UnSelectElements();
            MeasureHelper.ValidateMeasures(Data, noteViewer);
        }


        public void StartTypingLyrics(SyllableType syllableType = SyllableType.Single)
        {
            if(SelectedSymbols.Count != 1)
                return;

            if(CurrentLyrics != null)
            {
                if(CurrentLyrics.Text == LYRICS_PLACEHOLDER || string.IsNullOrWhiteSpace(CurrentLyrics.Text))
                    CurrentLyrics.Note.Lyrics.Clear();
                UnSelectElements();
                ScoreEditHelper.Rerender(Data);
                CurrentLyrics = null;
            }

            if (SelectedSymbols[0] is Note note)
            {
                if(note.Lyrics.Count > 0)
                {
                    CurrentLyrics = note.Lyrics[0];
                    SelectionHelper.ColorSelectedElement(noteViewer, CurrentLyrics);
                }

                else
                {
                    var newLyrics = new Lyrics(syllableType, LYRICS_PLACEHOLDER);

                    note.Lyrics.Add(newLyrics);
                    note.Lyrics[0].DefaultYPosition = CalculateLyricsYPosition(note);
                    ScoreEditHelper.Rerender(Data, noteViewer, SelectedSymbols);

                    CurrentLyrics = newLyrics;
                    SelectionHelper.ColorSelectedElement(noteViewer, newLyrics);
                }
            }
        }


        public void StopTypingLyrics()
        {
            if(CurrentLyrics == null) return;

            var syllableType = CurrentLyrics.Syllables[0].Type;


            if (syllableType == SyllableType.Begin)
                CurrentLyrics.Syllables[0].Type = SyllableType.Single;
            else if (syllableType != SyllableType.Single)
                CurrentLyrics.Syllables[0].Type = SyllableType.End;

            if (CurrentLyrics != null)
            {
                SelectionHelper.ColorElement(noteViewer, CurrentLyrics);
                if (CurrentLyrics.Text == LYRICS_PLACEHOLDER || string.IsNullOrWhiteSpace(CurrentLyrics.Text))
                {
                    CurrentLyrics.Note.Lyrics.Clear();
                }
                CurrentLyrics = null;
            }

            UnSelectElements();
            ScoreEditHelper.Rerender(Data);
        }


        public void AddCharacterToLyrics(string newChar)
        {
            if(CurrentLyrics == null || CurrentLyrics.Text.Length == App.Settings.MaxCharactersInSyllable) 
                return;

            var previousText = CurrentLyrics.Text;
            if (previousText == LYRICS_PLACEHOLDER)
                previousText = "";
            var newLyrics = new Lyrics(CurrentLyrics.Syllables[0].Type, $"{previousText}{newChar}");
            Console.WriteLine($"ADDING NEW LYRICS: {newLyrics} to {CurrentLyrics} of note: {CurrentLyrics?.Note ?? null}");
            newLyrics.DefaultYPosition = CalculateLyricsYPosition(CurrentLyrics.Note);
            CurrentLyrics.Note.Lyrics.Clear();
            CurrentLyrics.Note.Lyrics.Add(newLyrics);
            CurrentLyrics = newLyrics;
            ScoreEditHelper.Rerender(Data, noteViewer, SelectedSymbols);
            SelectionHelper.ColorSelectedElement(noteViewer, CurrentLyrics);
        }


        public double CalculateLyricsYPosition(Note note)
        {
            var yPos = (note.ActualRenderedBounds.NW.Y + note.ActualRenderedBounds.SW.Y) / 2;
            int _ = ScoreDataExtractor.GetStaffLineIndex(Data, yPos, out var linePositions);
            return 5 + ((linePositions.Min() - linePositions.Max()) / 5) * (5 + (App.Settings.AdditionalStaffLines.Value * 2)); 
        }


        public void RemoveCharacterFromLyrics(bool removeAll = false)
        {
            if (CurrentLyrics == null || CurrentLyrics.Text.Length == 0 || CurrentLyrics.Text == LYRICS_PLACEHOLDER)
                return;

            var newText = LYRICS_PLACEHOLDER;
            if (CurrentLyrics.Text.Length > 1 && !removeAll)
                newText = CurrentLyrics.Text[..^1];
            var newLyrics = new Lyrics(CurrentLyrics.Syllables[0].Type, newText);
            newLyrics.DefaultYPosition = CalculateLyricsYPosition(CurrentLyrics.Note);
            CurrentLyrics.Note.Lyrics.Clear();
            CurrentLyrics.Note.Lyrics.Add(newLyrics);
            CurrentLyrics = newLyrics;
            ScoreEditHelper.Rerender(Data, noteViewer, SelectedSymbols);
            SelectionHelper.ColorSelectedElement(noteViewer, CurrentLyrics);
        }

        public void JumpToNextSyllable(bool isNewWord = false, bool jumpToPrevious = false, bool changeSyllablesType = true)
        {
            if (CurrentLyrics == null || (CurrentLyrics.Note == CurrentLyrics.Note.Measure.Staff.Elements.OfType<Note>().Last() && !jumpToPrevious))
                return;

            var syllableType = CurrentLyrics.Syllables[0].Type;
            
            if(!changeSyllablesType) {}
            else if (isNewWord)
            {
                if(syllableType == SyllableType.Begin)
                    CurrentLyrics.Syllables[0].Type = SyllableType.Single;
                else if(syllableType != SyllableType.Single)
                    CurrentLyrics.Syllables[0].Type = SyllableType.End;
            }
            else
            {
                if (syllableType == SyllableType.End)
                    CurrentLyrics.Syllables[0].Type = SyllableType.Middle;
                else if (syllableType == SyllableType.Single)
                    CurrentLyrics.Syllables[0].Type = SyllableType.Begin;
            }
            CurrentLyrics.Note.Measure.Staff.Elements.OfType<Note>();

            var currentType = CurrentLyrics.Syllables[0].Type;
            var notes = CurrentLyrics.Note.Measure.Staff.Elements.OfType<Note>();
    
            var nextNote = jumpToPrevious
            ? notes
                .TakeWhile(n => n != CurrentLyrics.Note)
                .LastOrDefault()
            : notes
                .SkipWhile(n => n != CurrentLyrics.Note)
                .Skip(1)
                .FirstOrDefault();

            if (nextNote == null)
                return;
            
            if (CurrentLyrics.Text == LYRICS_PLACEHOLDER || string.IsNullOrWhiteSpace(CurrentLyrics.Text))
            {
                CurrentLyrics.Note.Lyrics.Clear();
            }

            UnSelectElements();
            ScoreEditHelper.Rerender(Data);
            SelectElement(nextNote, false);

            var newType = SyllableType.Single;

            if((currentType == SyllableType.Begin || currentType == SyllableType.Middle) && !jumpToPrevious)
                newType = SyllableType.Middle;

            StartTypingLyrics(newType);
        }



        private void NotifySelectionPropertiesChanged()
        {
            OnPropertyChanged(nameof(IsNoteOrRestSelected));
            OnPropertyChanged(nameof(IsClefSelected));
            OnPropertyChanged(nameof(IsTimeSignatureSelected));
            OnPropertyChanged(nameof(IsNothingSelected));
        }


        private bool IsSymbolSelectable(MusicalSymbol symbol)
        {
            return selectableSymbols.Any(allowedType =>
                allowedType.IsAssignableFrom(symbol.GetType()));
        }


        private double DistanceBetweenLines()
        {
            var staffLines = Data.Systems[0].LinePositions.Values.First();
            return Math.Abs(staffLines[0] - staffLines[1]);
        }


    }
}
