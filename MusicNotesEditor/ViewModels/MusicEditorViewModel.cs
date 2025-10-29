using Manufaktura.Controls.Audio;
using Manufaktura.Controls.Desktop.Audio;
using Manufaktura.Controls.Model;
using Manufaktura.Controls.Model.Collections;
using Manufaktura.Controls.Model.Events;
using Manufaktura.Controls.Model.Rules;
using Manufaktura.Controls.Parser;
using Manufaktura.Controls.Primitives;
using Manufaktura.Controls.WPF;
using Manufaktura.Music.Model;
using Manufaktura.Music.Model.MajorAndMinor;
using System.Windows;
using System.Xml;
using System.Xml.Linq;

namespace MusicNotesEditor.ViewModels
{
    class MusicEditorViewModel : ViewModel
    {
        private const int MAX_NUMBER_OF_STAVES = 10;
        private const int MAX_INITIAL_STAVE_LINES = 3;

        public RhythmicDuration? CurrentNote = null;
        public double NoteViewerContentWidth;
        public double NoteViewerContentHeight;
        public string XmlPath = "";

        private ScorePlayer player;
        private Score data;
        public Score Data
        {
            get { return data; }
            set { data = value; OnPropertyChanged(() => Data); }
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
                return;

            for (int i = 0; i < numberOfParts; i++)
            {

                int staffLinesCount = 1;
                // Fill up stave line 
                while (true)
                {
                    Data.Staves[i].Add(new Rest(RhythmicDuration.Whole));
                    Data.Staves[i].AddBarline(BarlineStyle.Regular);
                    var barlines = Data.Staves[i].Elements.OfType<Barline>();
                    double lastBarlineXPosition = barlines.Last().ActualRenderedBounds.SE.X;
                    
                    // until it reaches width
                    if (lastBarlineXPosition > NoteViewerContentWidth)
                    {
                        RemoveLastN(Data.Staves[i].Elements, 2);
                        if(staffLinesCount < MAX_INITIAL_STAVE_LINES)
                            AddNewLine(Data.Staves[i]);

                     
                        staffLinesCount++;
                    }

                    if (staffLinesCount > MAX_INITIAL_STAVE_LINES)
                    {
                        RemoveLastN(Data.Staves[i].Elements, 1);
                        FixMeasures(Data.Staves[i]);
                        break;
                    }
                }

            }
        }


        public void LoadData(string filepath)
        {
            var parser = new MusicXmlParser();
            var score = parser.Parse(XDocument.Load(filepath));
            
            Data = score;
            FixWidth();

        }

        public void PlayScore()
        {
            player = player = new MidiTaskScorePlayer(Data);
            player.Play();
        }

        public void FixWidth()
        {
            foreach (var staff in Data.Staves)
            {
                // Remove all existing system breaks
                for (int i = staff.Elements.Count - 1; i >= 0; i--)
                {
                    if (staff.Elements[i] is PrintSuggestion ps && ps.IsSystemBreak)
                        staff.Elements.RemoveAt(i);
                }

                for (int i = 0; i < staff.Elements.Count; i++)
                {
                    if (staff.Elements[i] is not Barline barline)
                        continue;

                    var lastBarlineXPosition = barline.ActualRenderedBounds.SE.X;
                    Console.WriteLine($"Barline[{i}] X={lastBarlineXPosition}");
                    if (lastBarlineXPosition > NoteViewerContentWidth && i > 0)
                    {
                        AddNewLine(staff, i - 1);
                        i++;
                    }
                }

                FixMeasures(staff);
            }

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

        public static void AddNewLine(Staff staff)
        {
            
            var lineBreak = new PrintSuggestion() { IsSystemBreak = true };
            staff.Add(lineBreak);
        }


        public static void AddNewLine(Staff staff, int index)
        {
            
            var lineBreak = new PrintSuggestion() { IsSystemBreak = true };
            staff.Elements.Insert(index, lineBreak);
        }


        public static void FixMeasures(Staff staff)
        {
            if (staff == null)
                throw new ArgumentNullException(nameof(staff));

            staff.Measures.Clear();

            var currentMeasure = new Measure(staff, null) { Number = 1 };
            staff.Measures.Add(currentMeasure);

            foreach (var e in staff.Elements)
            {
                // Find which system this element belongs to
                var system = GetSystemForElement(staff, e);
                if (system != null)
                    currentMeasure.System = system;

                currentMeasure.Elements.Add(e);
                var property = typeof(MusicalSymbol).GetProperty("Measure",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                property.SetValue(e, currentMeasure);


                if (e is Barline)
                {
                    currentMeasure = new Measure(staff, GetSystemForElement(staff, e))
                    {
                        Number = staff.Measures.Count + 1
                    };
                    staff.Measures.Add(currentMeasure);
                }
            }

            // Remove trailing empty measure
            if (staff.Measures.Last().Elements.Count == 0 && staff.Measures.Count > 1)
                staff.Measures.RemoveAt(staff.Measures.Count - 1);

            RaiseStaffInvalidated(staff);
        }

        protected static StaffSystem GetSystemForElement(Staff staff, MusicalSymbol element)
        {
            if (staff.Score == null || staff.Score.Systems == null || staff.Score.Systems.Count == 0)
                return null;

            // Count all system breaks that happened before this element
            int breakCount = staff.Elements
                .TakeWhile(e => e != element)
                .Count(e => e is PrintSuggestion ps && ps.IsSystemBreak);

            // If we have more breaks than systems, clamp
            breakCount = Math.Min(breakCount, staff.Score.Systems.Count - 1);

            // Return the corresponding system
            return staff.Score.Systems[breakCount];
        }


        private static void RaiseStaffInvalidated(Staff staff)
        {
            var evtField = typeof(Staff).GetField("StaffInvalidated",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            var del = (MulticastDelegate)evtField?.GetValue(staff);
            if (del == null) return;

            del.DynamicInvoke(staff, new InvalidateEventArgs<Staff>(staff));
        }


    }
}
