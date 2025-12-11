using Manufaktura.Controls.Model;
using Manufaktura.Controls.Rendering;
using Manufaktura.Controls.WPF;
using Manufaktura.Music.Model;
using MusicNotesEditor.Models.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MusicNotesEditor.Helpers
{
    public class MeasureHelper
    {

        public static void AddMeasure(Score score, double noteViewerContentWidth, double noteViewerContentHeight, int currentPageIndex, MusicalSymbol? selectedSymbol = null)
        {

            foreach (var staff in score.Staves)
            {
                if (selectedSymbol != null)
                {
                    if (selectedSymbol is not NoteOrRest)
                        continue;
                    var measureStartIndex = MeasureStartPositionFromElement(selectedSymbol, staff);
                    int measureEndIndex = -1;
                    for (int i = measureStartIndex; i < staff.Elements.Count; i++)
                    {
                        if (staff.Elements[i] is Barline barline )
                        {
                            measureEndIndex = i;
                            //if (barline.Style == BarlineStyle.LightHeavy)
                            //    measureEndIndex++; 
                            break;
                        }
                    }
                    if (measureEndIndex == -1)
                        throw new InvalidOperationException("The score structure corrupted.");

                    staff.Elements.Insert(measureEndIndex, new CorrectRest(RhythmicDuration.Whole));
                    staff.Elements.Insert(measureEndIndex, new Barline(BarlineStyle.Regular));
                }
                else
                {
                    staff.Elements.Insert(staff.Elements.Count - 1, new Barline(BarlineStyle.Regular));
                    staff.Elements.Insert(staff.Elements.Count - 1, new CorrectRest(RhythmicDuration.Whole));
                }
                
            }
            
            ScoreAdjustHelper.AdjustWidth(score, noteViewerContentWidth, noteViewerContentHeight, currentPageIndex);
        }


        public static void DeleteMeasure(Score score, double noteViewerContentWidth, double noteViewerContentHeight, int currectPageIndex, MusicalSymbol? selectedSymbol = null)
        {

            foreach (var staff in score.Staves)
            {

                if (selectedSymbol != null)
                {
                    var measureStartIndex = MeasureStartPositionFromElement(selectedSymbol, staff);
                    while(staff.Elements[measureStartIndex] is not Barline)
                    {
                            staff.Elements.RemoveAt(measureStartIndex);
                    }

                    if (staff.Elements[measureStartIndex] is Barline barline && barline.Style == BarlineStyle.LightHeavy)
                        staff.Elements.RemoveAt(measureStartIndex - 1);
                    else
                        staff.Elements.RemoveAt(measureStartIndex);
                }
                else
                {
                    Console.WriteLine($"deleting last measure !!!!!!!_________________!!!!!!!!!!!!!!!!!!");

                    var measureStartIndex = MeasureStartPositionFromElement(staff.Elements[staff.Elements.Count - 2], staff);
                    while (staff.Elements[measureStartIndex] is not Barline)
                    {
                        staff.Elements.RemoveAt(measureStartIndex);
                    }
                    if (staff.Elements[measureStartIndex] is Barline barline && barline.Style == BarlineStyle.LightHeavy)
                        staff.Elements.RemoveAt(measureStartIndex - 1);
                    else
                        staff.Elements.RemoveAt(measureStartIndex);
                }

            }

            ScoreAdjustHelper.AdjustWidth(score, noteViewerContentWidth, noteViewerContentHeight, currectPageIndex);
        }


        public static int MeasureStartPositionFromElement(MusicalSymbol symbol, Staff staff)
        {

            var measure = staff.Measures.Where(m => m.Number == symbol.Measure.Number).First();
            return MeasureStartPosition(measure);
        }

        public static int MeasureStartPosition(Measure measure)
        {
            return measure.Staff.Elements.IndexOf(measure.Elements[0]);
        }

        public static void ValidateMeasures(Score score, NoteViewer? noteViewer)
        {
            if (noteViewer == null)
                return;

            List<Measure> invalidMeasures = new List<Measure>();

            foreach(var staff in score.Staves)
            {
                Proportion? timeInMetrum = null;
                Proportion takenDuration = new Proportion(0, 1);

                for(int i = 0; i < staff.Elements.Count; i++)
                {
                    var element = staff.Elements[i];
                    if (element is TimeSignature metrum)
                    {
                        timeInMetrum = metrum.NumberValue;
                    }
                    else if (element is Barline barline)
                    {
                        if (timeInMetrum != null && barline.Style != BarlineStyle.None && timeInMetrum != takenDuration)
                        {
                            invalidMeasures.Add(element.Measure);
                        }
                        takenDuration = new Proportion(0, 1);
                    }
                    else if (element is NoteOrRest noteOrRest)
                    {
                        takenDuration += noteOrRest.Duration.ToProportion();
                    }
                }
            }

            Console.WriteLine($"invalid MEASURES!!!: {string.Join(", ", invalidMeasures)}");

            List<MusicalSymbol> invalidMeasureLines = new List<MusicalSymbol>();
            foreach(var invalidMeasure in invalidMeasures)
            {
                foreach(var staffFragment in invalidMeasure.System.Staves)
                {
                    Console.WriteLine($"INVALID STAFF FRAGMENT: {staffFragment} at {staffFragment.ActualRenderedBounds} of {staffFragment.RenderedWidth}");
                    invalidMeasureLines.Add(staffFragment);
                }
                
            }
            SelectionHelper.ColorElements(noteViewer, invalidMeasureLines, Color.FromRgb(250, 0, 0));

        }
    }
}
