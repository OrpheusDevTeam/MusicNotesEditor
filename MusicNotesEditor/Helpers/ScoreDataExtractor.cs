using Manufaktura.Controls.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Helpers
{
    public class ScoreDataExtractor
    {
        private const int NUMBER_OF_LINES_IN_STAFF = 5;

        public static int GetStaffLineIndex(Score score, double yPosition, out List<double> linePositions)
        {
            foreach (StaffSystem system in score.Systems)
            {
                if (system.LinePositions == null || system.LinePositions.Count == 0)
                    continue;

                int additionalStaffLines = App.Settings.AdditionalStaffLines.Value;

                var systemLines = system.LinePositions.Values.SelectMany(v => v).ToList();
                for(int index=0; index < systemLines.Count; index += NUMBER_OF_LINES_IN_STAFF)
                {
                    var staffLinesInSystem = systemLines.GetRange(index, NUMBER_OF_LINES_IN_STAFF);
                    var lines = AddValuesInBetween(
                        ExpandList(
                            staffLinesInSystem, additionalStaffLines
                        )
                    );

                    if (lines.Count == 0)
                        continue;

                    int threshold = App.Settings.SnappingThreshold.Value;

                    double minY = lines.Min() - threshold;
                    double maxY = lines.Max() + threshold;

                    if (yPosition < minY || yPosition > maxY)
                        continue;

                    int closestIndex = 0;
                    double smallestDiff = double.MaxValue;

                    for (int i = 0; i < lines.Count; i++)
                    {
                        double diff = Math.Abs(lines[i] - yPosition);
                        if (diff < smallestDiff)
                        {
                            smallestDiff = diff;
                            closestIndex = i;
                        }
                    }
                    linePositions = lines;
                    return closestIndex;
                }
            }
            linePositions = [];
            return -1;
        }


        private static List<double> AddValuesInBetween(List<double> values)
        {
            return values.SelectMany((v, i) => i < values.Count - 1
                    ? new List<double> { v, (v + values[i + 1]) / 2.0 }
                    : new List<double> { v })
                .ToList();
        }


        private static List<double> ExpandList(List<double> values, int n)
        {
            if (values == null || values.Count < 2)
                throw new ArgumentException("List must contain at least two elements.");
            if (n < 1)
                throw new ArgumentException("n must be at least 1.");

            // Compute average distance between neighbors
            double avgGap = values.Zip(values.Skip(1), (a, b) => b - a).Average();

            var expanded = new List<double>();

            double first = values.First();
            for (int i = n; i >= 1; i--)
                expanded.Add(first - i * avgGap);

            expanded.AddRange(values);

            double last = values.Last();
            for (int i = 1; i <= n; i++)
                expanded.Add(last + i * avgGap);

            return expanded;
        }


        public static Measure? GetMeasure(Score score, double clickXPos, double clickYPos)
        {
            foreach (var staff in score.Staves)
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
                            return staff.Measures[i + 1];
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
                        return staff.Measures[i + 1];
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


        public static Clef FindClefOfElement(NoteOrRest note)
        {
            var staffElements = note.Measure.Staff.Elements;
            var noteIndex = staffElements.IndexOf(note);
            for(int i = noteIndex - 1; i >= 0; i--)
            {
                if (staffElements[i] is Clef clef)
                    return clef;
            }

            return null;
        }



    }
}
