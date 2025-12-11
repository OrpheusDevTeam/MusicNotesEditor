using Manufaktura.Controls.Model;
using Manufaktura.Controls.Model.Collections;
using Manufaktura.Controls.Model.Events;
using Manufaktura.Controls.WPF;
using Manufaktura.Music.Model;
using MusicNotesEditor.Models.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Helpers
{
    internal class ScoreAdjustHelper
    {   

        public static void AdjustWidth(Score score, double noteViewerContentWidth, double noteViewerContentHeight, int pageIndex)
        {
            Dictionary<Staff, List<Barline>> barlinesInMeasures = new Dictionary<Staff, List<Barline>>();

            foreach (var staff in score.Staves)
            {
                FixMeasures(staff);
                foreach(var m in staff.Measures)
                    Console.WriteLine($"{m} of {m.System}: {m.Width}");
            }

            ScoreEditHelper.Rerender(score);

            foreach (var staff in score.Staves)
            {
                // Remove all existing system breaks
                for (int i = staff.Elements.Count - 1; i >= 0; i--)
                {
                    if (staff.Elements[i] is PrintSuggestion ps && ps.IsSystemBreak)
                        staff.Elements.RemoveAt(i);
                }
                barlinesInMeasures.Add(staff, staff.Elements.OfType<Barline>().ToList());
            }

            int numberOfMeasures = score.FirstStaff.Elements.OfType<Barline>().Count();
            for (int i = 0; i < numberOfMeasures; i++)
            {
                // -1 Nothing, 0 System Break, 1 Page Break
                int actionToTake = -1;
                foreach(var staff in barlinesInMeasures.Keys)
                {
                    var element = barlinesInMeasures[staff][i];
                    var elementXPosition = element.ActualRenderedBounds.SE.X;
                    var elementYPosition = element.ActualRenderedBounds.SE.Y;
                    //Console.WriteLine($"ADJUSTING WIDTH, POSITION BREAK: {elementXPosition}");

                    if (elementXPosition > noteViewerContentWidth && i > 0)
                    {
                        //Console.WriteLine($"ADJUSTING WIDTH, POSITION BREAKING HOORAY");
                        actionToTake = 0;
                        break;
                    }
                }
                if (actionToTake == 0)
                {
                    foreach (var staff in score.Staves)
                    {
                        ScoreEditHelper.AddNewLine(staff, staff.Elements.IndexOf(barlinesInMeasures[staff][i - 1]) + 1);
                    }
                }
                    
            }

            foreach (var staff in score.Staves)
            {
                FixMeasures(staff);
            }

            for (int i = 0; i < numberOfMeasures; i++)
            {
                double measureWidth = 0;
                foreach (var staff in barlinesInMeasures.Keys)
                {
                    var measure = barlinesInMeasures[staff][i].Measure;
                    double measureInSystemWidth = measure?.Width ?? 0;
                    
                    measureWidth = Math.Max(measureWidth, measureInSystemWidth);

                    //Console.WriteLine($"ADJUSTING MEASURE WIDTH, : {measureWidth}");
                    
                }
                if(measureWidth > 0)
                {
                    foreach (var staff in score.Staves)
                    {
                        staff.Measures[i].Width = measureWidth;
                    }
                }
                
            }

            var page = score.Pages.Last();
            var lastCorrectSystem = score.FirstStaff.Elements.Last().Measure.System;
            int lastCorrectSystemIndex = page.Systems.IndexOf(lastCorrectSystem);
            List<StaffSystem> newStaffSystems = new List<StaffSystem>();

            Console.WriteLine($"\nFINDING LAST SYSTEM: \n{lastCorrectSystem} \nindex: {lastCorrectSystemIndex} \nmeasure: {score.FirstStaff.Elements.Last().Measure}\n");

            for (int i = page.Systems.Count - 1; i > lastCorrectSystemIndex; i--)
            {
                Console.WriteLine($"CLEANING UP STAFF SYSTEM AT : {i}");
                page.Systems.RemoveAt(i);
            }

            ScoreEditHelper.Rerender(score);
        }

        private const double MIN_MEASURE_WIDTH = 100;
        private const double BASE_MEASURE_WIDTH = 60;

        private const double REST_WIDTH = 20;
        private const double NOTE_WIDTH = 20;
        private const double CLEF_WIDTH = 20;
        private const double TIME_SIGNATURE_WIDTH = 24;
        private const double ACCIDENTAL_WIDTH = 15;
        private const double LYRIC_WIDTH = 3;

        public static void FixMeasures(Staff staff)
        {
            Console.WriteLine("FIXING");
            if (staff == null)
                throw new ArgumentNullException(nameof(staff));

            staff.Measures.Clear();
            var currentMeasure = new Measure(staff, null) { Number = 1};
            
            staff.Measures.Add(currentMeasure);
            double currentMeasureWidth = BASE_MEASURE_WIDTH;
            
            foreach (var e in staff.Elements)
            {
                // Find which system this element belongs to
                var system = GetSystemForElement(staff, e);
                if (system != null)
                    currentMeasure.System = system;

                currentMeasure.Elements.Add(e);

                switch (e)
                {
                    case Rest rest:
                        currentMeasureWidth += REST_WIDTH;
                        break;

                    case Note note:
                        currentMeasureWidth += NOTE_WIDTH;
                        if (note.Duration != RhythmicDuration.Whole && note.Duration != RhythmicDuration.Half)
                        {
                            currentMeasureWidth += 6; // Slightly wider for longer notes
                        }
                        if(note.Alter != 0)
                        {
                            currentMeasureWidth += ACCIDENTAL_WIDTH;
                        }
                        if(note.Lyrics != null && note.Lyrics.Count > 0)
                            currentMeasureWidth += LYRIC_WIDTH * note.Lyrics[0].Text.Length;
                        break;

                    case Clef clef:
                        currentMeasureWidth += CLEF_WIDTH;    
                        break;

                    case TimeSignature timeSig:
                        currentMeasureWidth += TIME_SIGNATURE_WIDTH;
                        break;

                    case PrintSuggestion printSuggestion:
                        currentMeasureWidth += CLEF_WIDTH;
                        break;

                    default:
                        break;
                }

                // Ugly reflection trick but needed to work
                var property = typeof(MusicalSymbol).GetProperty("Measure",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                property.SetValue(e, currentMeasure);

                if (e is Barline)
                {

                    currentMeasure.Width = Math.Max(currentMeasureWidth, MIN_MEASURE_WIDTH);

                    int measureNumber = staff.Measures.Count + 1;


                    currentMeasureWidth = BASE_MEASURE_WIDTH;
                    currentMeasure = new Measure(staff, GetSystemForElement(staff, e))
                    {
                        Number = measureNumber
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

        public static ScorePage CreateScorePage(Score score)
        {
            var scorePageType = typeof(ScorePage);
            var constructor = scorePageType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(Score) },
                null);

            if (constructor != null)
            {
                return (ScorePage)constructor.Invoke(new object[] { score });
            }

            throw new InvalidOperationException("Cannot create ScorePage instance");
        }
        private static double CalculateDistance(MusicalSymbol symbol1, MusicalSymbol symbol2)
        {
            var coord1 = symbol1.ActualRenderedBounds;
            var coord2 = symbol2.ActualRenderedBounds;

            return Math.Abs(coord1.NE.X - coord2.NE.X);
        }

    }
}
