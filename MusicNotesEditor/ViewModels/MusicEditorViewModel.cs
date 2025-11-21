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

        public bool IsNoteOrRestSelected => SelectedSymbols.Any(symbol => symbol is NoteOrRest);
        public bool IsClefSelected => SelectedSymbols.Any(symbol => symbol is Clef);
        public bool IsTimeSignatureSelected => SelectedSymbols.Any(symbol => symbol is TimeSignature);
        public bool IsNothingSelected => !SelectedSymbols.Any();
        public bool IsRest => CurrentAccidental == 2;

        private string _scoreFileName;
        private ScorePlayer player;
        private Score data;
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
                ScoreAdjustHelper.AdjustWidth(Data, NoteViewerContentWidth);
            }
        }


        public void LoadData(string filepath)
        {
            var parser = new MusicXmlParser();
            var score = parser.Parse(XDocument.Load(filepath));
            Data = score;
        }

        public void PlayScore()
        {
            player = new MidiTaskScorePlayer(Data);
            player.Tempo = Tempo.Allegro;
            player.Play();
        }


        public void AddNote(double clickXPos, double clickYPos)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ScoreEditHelper.InsertNote(Data, clickXPos, clickYPos, NoteViewerContentWidth, CurrentNote, IsRest, CurrentAccidental);        

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

            ScoreEditHelper.DeleteElements(SelectedSymbols);

            ScoreEditHelper.Rerender(Data);
            
            SelectionHelper.ColorSelectedElements(noteViewer, SelectedSymbols);

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
                MeasureHelper.AddMeasure(Data, NoteViewerContentWidth);
            else if (SelectedSymbols.Count == 1)
                MeasureHelper.AddMeasure(Data, NoteViewerContentWidth, SelectedSymbols[0]);

            SelectionHelper.ColorSelectedElements(noteViewer, SelectedSymbols);
        }


        public void DeleteLastMeasure()
        {
            if (Data.FirstStaff.Measures.Count < 3 || (!IsNoteOrRestSelected && !IsNothingSelected))
                return;

            if (SelectedSymbols.Count == 0)
                MeasureHelper.DeleteMeasure(Data, NoteViewerContentWidth);
            else if (SelectedSymbols.Count == 1)
                MeasureHelper.DeleteMeasure(Data, NoteViewerContentWidth, SelectedSymbols[0]);

            UnSelectElements();
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
