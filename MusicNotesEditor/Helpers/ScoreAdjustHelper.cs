using Manufaktura.Controls.Model;
using Manufaktura.Controls.Model.Collections;
using Manufaktura.Controls.Model.Events;
using Manufaktura.Controls.WPF;
using MusicNotesEditor.Models.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Helpers
{
    internal class ScoreAdjustHelper
    {

        public static void AdjustWidth(Score score, double noteViewerContentWidth, int pageIndex)
        {
            Dictionary<Staff, List<Barline>> barlinesInMeasures = new Dictionary<Staff, List<Barline>>();

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
                int breakIndex = -1;
                foreach(var staff in barlinesInMeasures.Keys)
                {
                    var element = barlinesInMeasures[staff][i];
                    var elementXPosition = element.ActualRenderedBounds.SE.X;
                    Console.WriteLine($"ADJUSTING WIDTH, POSITION BREAK: {elementXPosition}");
                    if (elementXPosition > noteViewerContentWidth && i > 0)
                    {

                        Console.WriteLine($"ADJUSTING WIDTH, POSITION BREAKING HOORAY");
                        breakIndex = i;
                        break;
                    }
                }
                if (breakIndex == -1)
                    continue;

                foreach(var staff in score.Staves)
                {
                    ScoreEditHelper.AddNewLine(staff, staff.Elements.IndexOf(barlinesInMeasures[staff][i-1])+1);
                }
            }

            foreach (var staff in score.Staves)
            {
                FixMeasures(staff);
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

        public static void FixMeasures(Staff staff)
        {
            Console.WriteLine("FIXING");
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
