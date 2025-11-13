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
using MusicNotesEditor.Helpers;
using MusicNotesEditor.Models;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Xml;
using System.Xml.Linq;

namespace MusicNotesEditor.ViewModels
{
    class MusicEditorViewModel : ViewModel
    {
        private const int MAX_NUMBER_OF_STAVES = 5;
        private const int TEMP_NOTE_OFFSET = 6;
        private const int NUMBER_OF_LINES_IN_STAFF = 5;


        public MusicalSymbol? SelectedSymbol = null;
        public RhythmicDuration? CurrentNote = null;
        public double NoteViewerContentWidth;
        public double NoteViewerContentHeight;
        public string XmlPath = "";

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
            Console.WriteLine("initinidgafsasfas");
            if (!string.IsNullOrEmpty(XmlPath))
            {
                Console.WriteLine("Untitling");

                ScoreFileName = Path.GetFileName(XmlPath);
                return;
            };
            ScoreFileName = "Untitled Score";

            Console.WriteLine($"initinidgafsasfasXDSADASSXXACSADSA: {ScoreFileName}");


            int measuresInLine = Math.Max(
                App.Settings.DefaultInitialMeasures.Value / numberOfParts,
                App.Settings.MinimalInitialMeasurePerStaff.Value);

            for (int i = 0; i < numberOfParts; i++)
            {
                // Fill up stave line 
                for (int j = 0; j < measuresInLine; j++)
                {
                    Data.Staves[i].Add(new CorrectRest(RhythmicDuration.Whole) );
                    Data.Staves[i].AddBarline(BarlineStyle.Regular);                    
                }

                RemoveLastN(Data.Staves[i].Elements, 1);
                Data.Staves[i].AddBarline(BarlineStyle.LightHeavy);
                AdjustWidth();
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

            // Additional Staff lines gives twice as many additional positions for notes to be in
            var additionalPositions = App.Settings.AdditionalStaffLines.Value * 2;
            var staffLineIndex = ScoreDataExtractor.GetStaffLineIndex(Data, clickYPos, out var _) - additionalPositions;

            if (CurrentNote == null || staffLineIndex == -1 - additionalPositions)
            {
                return;
            }

            var currentMeasure = GetMeasure(clickXPos, clickYPos);
            Console.WriteLine("\nMeasuring: ");
            Console.WriteLine(currentMeasure);
            Console.WriteLine($"\nCLICK: \n\tX: {clickXPos} \n\tY: {clickYPos} LINE INDEX: {staffLineIndex}");
            if (currentMeasure == null || currentMeasure.Elements.Count() == 1)
            {
                return;
            }

            var staffElements = currentMeasure.Staff.Elements;
            int currentMeasureStartIndex = -1;

            // Get Start index of measure
            for(int i = 0; i < staffElements.Count; i++)
            {   
                if(staffElements[i].Measure == currentMeasure)
                {
                    currentMeasureStartIndex = i;
                    break;
                }
            }

            int currentMeasureEndIndex = currentMeasureStartIndex + currentMeasure.Elements.Count() - 1;
            if (currentMeasure.Number == currentMeasure.Staff.Measures.Last().Number)
                currentMeasureEndIndex++;

            // Replace rests with temporary notes
            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var rest = staffElements[i] as CorrectRest;
                if (rest != null)
                {
                    staffElements[i] = new TempNote(rest.Duration);
                }
            }

            // Put empty print suggestion to force rerendering
            staffElements.Add(new PrintSuggestion()
            {
                IsSystemBreak = false,
                IsPageBreak = false,
                IsBreakpointSet = false,
                IsVisible = false,
            });
            staffElements.RemoveAt(staffElements.Count - 1);
            
            int elementOnLeftIndex = -1;
            
            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var element = staffElements[i];
                Console.WriteLine($"Element: {element}");
                Console.WriteLine($"\tMeasure: {element.Measure}");

                var elementXPosition = HorizontalPosition(element);

                if(element is TempNote)
                {
                    elementXPosition += TEMP_NOTE_OFFSET;
                }

                if (elementXPosition <= clickXPos)
                {
                    Console.WriteLine($"ElementXPOS: {elementXPosition}\tclickXPos: {clickXPos} Result:{elementXPosition <= clickXPos}");
                    Console.WriteLine($"Lefty: {element}");
                    elementOnLeftIndex = i;
                }
                else if (elementXPosition > clickXPos)
                {
                    break;
                }
            }

            if(elementOnLeftIndex < 0)
            {
                elementOnLeftIndex = currentMeasureStartIndex - 1;
            }

            bool isRightCloser = false;

            if(elementOnLeftIndex + 1 < staffElements.Count)
            {

                var elementOnLeftXPosition = HorizontalPosition(
                    staffElements[elementOnLeftIndex]);

                var elementOnRightXPosition = HorizontalPosition(
                staffElements[elementOnLeftIndex + 1]);

                isRightCloser = Math.Abs(elementOnLeftXPosition - clickXPos)
                    > Math.Abs(elementOnRightXPosition - clickXPos);
            }

            Proportion? timeInMetrum = null;
            Clef? lastClef = null; 

            // Get time signature
            for (int i = 0; i < currentMeasureEndIndex; i++)
            {
                var element = staffElements[i];
                if (element is TimeSignature metrum)
                {
                    timeInMetrum = metrum.NumberValue;
                }
                if(element is Clef clef)
                {
                    lastClef = clef;
                }
            }

            var newNoteProportion = CurrentNote.Value.ToProportion();

            // Stop if chosen note takes more space than measure
            if(newNoteProportion > timeInMetrum)
            {
                // Add notification
                return;
            }

            // Replace temporary notes with rests
            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var tempNote = staffElements[i] as TempNote;
                if (tempNote != null)
                {
                    staffElements[i] = new CorrectRest(tempNote.Duration);
                }
            }

            Pitch pitch = PitchHelper.GetPitchFromIndex(staffLineIndex, lastClef);
            staffElements.Insert(elementOnLeftIndex + 1, 
                new TempNote(pitch, CurrentNote.Value));
            
            // Remove stuff over metrum
            Proportion excessProportion = newNoteProportion;
            Proportion zeroProportion = new Proportion(0, 1);
            int startingIndex = elementOnLeftIndex + 1;
            int direction = Direction(isRightCloser);
            int cursor = Math.Min(startingIndex + direction, staffElements.Count());
            bool startOverridingNotes = false;
            int notCorrectRestNeighboursCount = 0;

            while (excessProportion > zeroProportion)
            {
                if(cursor >= staffElements.Count())
                {
                    direction = -1;
                    cursor = staffElements.Count() - 1;
                    notCorrectRestNeighboursCount++;
                    continue;
                }
                else if (cursor < currentMeasureStartIndex)
                {
                    direction = 1;
                    cursor = currentMeasureStartIndex;
                    notCorrectRestNeighboursCount++;
                    continue;
                }
                var element = staffElements[cursor];
                
                if (element is CorrectRest rest)
                {
                    if (rest.Duration.ToProportion() > excessProportion)
                    {
                        var proportionToFill = rest.Duration.ToProportion() - excessProportion;
                        
                        var currentDuration = DurationHelper.HalfDuration(rest.Duration);
                        rest.Duration = currentDuration;
                        proportionToFill -= currentDuration.ToProportion();

                        while (proportionToFill != zeroProportion)
                        {
                            currentDuration = DurationHelper.HalfDuration(currentDuration);
                            Console.WriteLine($"\tDuration: {currentDuration}");
                            Console.WriteLine($"\tProportion: {proportionToFill}");

                            if (currentDuration.ToProportion() > proportionToFill)
                            {
                                continue;
                            }
                            staffElements.Insert(cursor, new CorrectRest(currentDuration));
                            proportionToFill -= currentDuration.ToProportion();
                        }
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Removed Element: {staffElements[cursor]}");
                        staffElements.RemoveAt(cursor);
                        excessProportion -= rest.Duration.ToProportion();
                        cursor--;
                    }
                }
                else if (element is TempNote tempNote)
                {
                }
                else if (element is Note note)
                {
                    if (startOverridingNotes)
                    {
                        if (note.Duration.ToProportion() > excessProportion)
                        {
                            var proportionToFill = note.Duration.ToProportion() - excessProportion;
                            
                            var currentDuration = DurationHelper.HalfDuration(note.Duration);
                            while(currentDuration.ToProportion() > proportionToFill)
                            {
                                currentDuration = DurationHelper.HalfDuration(currentDuration);
                            }
                            note.Duration = currentDuration;
                            proportionToFill -= currentDuration.ToProportion();
                
                            while (proportionToFill != zeroProportion)
                            {
                                currentDuration = DurationHelper.HalfDuration(currentDuration);

                                if (currentDuration.ToProportion() > proportionToFill)
                                {
                                    Console.WriteLine($"\tSkipping Duration: {currentDuration.ToProportion()}");
                                    continue;
                                }

                                Console.WriteLine($"\tDuration: {currentDuration}");
                                Console.WriteLine($"\tProportion: {proportionToFill}");
                                staffElements.Insert(cursor, new Note(note.Pitch, currentDuration));
                                proportionToFill -= currentDuration.ToProportion();
                            }
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"Removed Element: {staffElements[cursor]}");
                            staffElements.RemoveAt(cursor);
                            excessProportion -= note.Duration.ToProportion();
                            cursor--;
                        }
                    }
                    else
                    {
                        direction *= -1;
                        notCorrectRestNeighboursCount++;
                    }
                }
                else if (element is Barline)
                {
                    direction *= -1;
                    notCorrectRestNeighboursCount++;
                }
                else
                {
                    direction *= -1;
                }
                cursor += direction;
                startOverridingNotes = notCorrectRestNeighboursCount > 1;
            }


            // Replace new note with note
            for (int i = currentMeasureStartIndex; i < staffElements.Count(); i++)
            {
                var tempNote = staffElements[i] as TempNote;
                if (tempNote != null)
                {
                    staffElements[i] = new Note(tempNote.Pitch, tempNote.Duration);
                }
            }

            AdjustWidth();

            stopwatch.Stop();
            Console.WriteLine($"Execution Time: {stopwatch.ElapsedMilliseconds} ms");
        }

        public int Direction(bool value)
        {
            return value ? 1 : -1;
        }


        public void AdjustWidth()
        {
            foreach (var staff in Data.Staves)
            {
                // Remove all existing system breaks and replace rests with tempNotes
                for (int i = staff.Elements.Count - 1; i >= 0; i--)
                {
                    if (staff.Elements[i] is PrintSuggestion ps && ps.IsSystemBreak)
                        staff.Elements.RemoveAt(i);
            
                    var tempNote = staff.Elements[i] as CorrectRest;
                    if (tempNote != null)
                    {
                        staff.Elements[i] = new TempNote(tempNote.Duration);
                    }
                }

                int lastBarlineIndex = -1;

                for (int i = 0; i < staff.Elements.Count; i++)
                {
                    var element = staff.Elements[i];
                    var previousBarlineIndex = lastBarlineIndex;
                    if(element is Barline)
                    {
                        lastBarlineIndex = i;
                    }

                    var elementXPosition = element.ActualRenderedBounds.SE.X;
                    //Console.WriteLine($"Barline[{i}] X={lastBarlineXPosition}");
                    if (elementXPosition > NoteViewerContentWidth && i > 0)
                    {
                        Console.WriteLine($"I: {i} \tcURENT: {previousBarlineIndex} at {staff.Elements[previousBarlineIndex]}");
                        AddNewLine(staff, previousBarlineIndex+1);
                        i++;
                    }
                }

                // Replace back TempNotes with rests
                for (int i = 0; i < staff.Elements.Count; i++)
                {
                    var tempNote = staff.Elements[i] as TempNote;
                    if (tempNote != null)
                    {
                        staff.Elements[i] = new CorrectRest(tempNote.Duration);
                    }
                }

                FixMeasures(staff);
            }

            //int systemCount = 0;
            //foreach(var staff in Data.Staves)
            //{
            //    var lastMeasure = staff.Measures.Last();
            //    var lastValidSystem = lastMeasure.Staff.Score.Systems.IndexOf(lastMeasure.System);
            //    if(lastValidSystem > systemCount)
            //    {
            //        systemCount = lastValidSystem;
            //    }
            //}

            //for(int i = Data.Systems.Count - 1; i > systemCount; i--)
            //{
            //    Data.Systems.RemoveAt(i);
            //}
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


        private Measure? GetMeasure(double clickXPos, double clickYPos)
        {
            foreach (var staff in Data.Staves)
            {
                var barlines = staff.Elements.OfType<Barline>().ToList();

                for (int i = 0; i < barlines.Count - 1; i++)
                {
                    var firstBarline = barlines[i];
                    var secondBarline = (i + 1 < barlines.Count) ? barlines[i + 1] : null;

                    var threshold = App.Settings.SnappingThreshold.Value;

                    var leftX = firstBarline.ActualRenderedBounds.SE.X;

                    var additionalStaffLines = App.Settings.AdditionalStaffLines.Value;
                    
                    var bottomY = firstBarline.ActualRenderedBounds.NE.Y - threshold;
                    var topY = firstBarline.ActualRenderedBounds.SE.Y + threshold;
                    var height = topY - bottomY;

                    
                    double additionalStaffHeight = height * ((double)additionalStaffLines / NUMBER_OF_LINES_IN_STAFF);

                    topY += additionalStaffHeight;
                    bottomY -= additionalStaffHeight;


                    if (secondBarline == null)
                    {
                        Console.WriteLine($"Before: bottomY={bottomY}, topY={topY}, leftX={leftX}");

                        if (staff.Measures[i + 1].System != staff.Measures[i].System)
                        {
                            Console.WriteLine("Condition TRUE: staff.Measures[i+1].System != staff.Measures[i].System");

                            bottomY = staff.Measures[i + 1].System.LinePositions[1].First() - threshold - additionalStaffHeight;
                            topY = staff.Measures[i + 1].System.LinePositions[1].Last() + threshold + additionalStaffHeight;
                            leftX = 0;

                            Console.WriteLine($"After: bottomY={bottomY}, topY={topY}, leftX={leftX}");
                        }
                        else
                        {
                            Console.WriteLine("Condition FALSE: staff.Measures[i+1].System == staff.Measures[i].System");
                        }
                        if (clickYPos >= bottomY && clickYPos <= topY && clickXPos >= leftX)
                        {
                            return staff.Measures[i+1];
                        }

                        continue;
                    }

                    var rightX = secondBarline.ActualRenderedBounds.SE.X;

                    var secondBottomY = secondBarline.ActualRenderedBounds.NE.Y - threshold - additionalStaffHeight;

                    if (secondBottomY != bottomY)
                    {
                        bottomY = secondBottomY;
                        topY = secondBarline.ActualRenderedBounds.SE.Y + threshold + additionalStaffHeight;
                        leftX = 0;
                    }

                    if (clickYPos >= bottomY && clickYPos <= topY &&
                        clickXPos >= leftX && clickXPos <= rightX)
                    {
                        return staff.Measures[i+1];
                    }
                }
            }

            Console.WriteLine("❌ Click did not hit any measure");
            return null;
        }

        public static double HorizontalPosition(MusicalSymbol element)
        {
            var elementMostLeftPosition = element.ActualRenderedBounds.SW.X;
            var elementMostRightPosition = element.ActualRenderedBounds.SE.X;

            return (elementMostLeftPosition + elementMostRightPosition) / 2;
        }


        private static void AddNewLine(Staff staff)
        {
            
            var lineBreak = new PrintSuggestion() { IsSystemBreak = true };
            staff.Add(lineBreak);
        }


        private static void AddNewLine(Staff staff, int index)
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

                // Ugly reflection trick but needed to work
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

        private static StaffSystem GetSystemForElement(Staff staff, MusicalSymbol element)
        {
            if (staff.Score == null || staff.Score.Systems == null || staff.Score.Systems.Count == 0)
                return null;

            int breakCount = staff.Elements
                .TakeWhile(e => e != element)
                .Count(e => e is PrintSuggestion ps && ps.IsSystemBreak);

            // If we have more breaks than systems, clamp
            breakCount = Math.Min(breakCount, staff.Score.Systems.Count - 1);

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
