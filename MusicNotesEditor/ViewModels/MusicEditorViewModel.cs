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
        public bool IsRest = false;
        public bool IsDragging = false;
        public double NoteViewerContentWidth;
        public double NoteViewerContentHeight;
        public string XmlPath = "";

        private string _scoreFileName;
        private Color selectionColor = Color.FromRgb(200, 0, 200);
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


        public void AddNote(NoteViewer noteViewer, double clickXPos, double clickYPos)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ScoreEditHelper.InsertNote(Data, clickXPos, clickYPos, NoteViewerContentWidth, CurrentNote, IsRest);        

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


        public void SelectElement(NoteViewer noteViewer, MusicalSymbol musicalSymbol, bool multiSelect)
        {
            Console.WriteLine($"SELECTING: {musicalSymbol}");

            if (CurrentNote != null || musicalSymbol == null || !IsSymbolSelectable(musicalSymbol)) 
                return;

            if( !IsNoteOrRestSelected() )
            {
                multiSelect = false;
            }

            if(!multiSelect || !typeof(NoteOrRest).IsAssignableFrom(musicalSymbol.GetType()) )
            {
                UnSelectElements(noteViewer);
            }

            Console.WriteLine($"SELECTED ELEMENT!!!!!!!!!!!!!!!!!: {musicalSymbol}");     
            

            SelectionHelper.ColorElement(noteViewer, musicalSymbol, selectionColor);

            if(!SelectedSymbols.Contains(musicalSymbol))
                SelectedSymbols.Add(musicalSymbol);
            Console.WriteLine($"SELECTED ELEMENTS!!!!!!!!!!!!!!!!!: B: {string.Join(",", SelectedSymbols)}");
        }

        public void UnSelectElements(NoteViewer viewer, Func<MusicalSymbol, bool>? filter = null)
        {
            if (filter == null)
            {
                Console.WriteLine($"UNSELECTING ALL!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                SelectionHelper.ColorElements(viewer, SelectedSymbols);
                SelectedSymbols.Clear();
            }
            else
            {
                var elementsToUnselect = SelectedSymbols.Where(filter).ToList();
                Console.WriteLine($"Unselecting {elementsToUnselect.Count} filtered elements");

                SelectionHelper.ColorElements(viewer, SelectedSymbols);

                foreach (var element in elementsToUnselect)
                {
                    SelectedSymbols.Remove(element);
                }

            }
        }


        public void DeleteSelectedElements(NoteViewer noteViewer)
        {

            Console.WriteLine("DELETING!!!!!!!!!!!!!!");
            if (!IsNoteOrRestSelected())
                return;

            Console.WriteLine("DELETING AND PASSED!!!");

            ScoreEditHelper.DeleteElements(SelectedSymbols);

            ScoreEditHelper.Rerender(Data);
            
            SelectionHelper.ColorElements(noteViewer, SelectedSymbols, selectionColor);

        }


        private bool IsSymbolSelectable(MusicalSymbol symbol)
        {
            return selectableSymbols.Any(allowedType =>
                allowedType.IsAssignableFrom(symbol.GetType()));
        }

        private bool IsNoteOrRestSelected()
        {
            return SelectedSymbols.Any(symbol => symbol is NoteOrRest);
        }

    }
}
