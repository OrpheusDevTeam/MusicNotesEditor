using Manufaktura.Controls.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Helpers
{
    internal class ScoreDataExtractor
    {
        public static List<double> GetStaffLinesPositions(Score score)
        {
            var linesPositions = new List<double>();
            var staffSystems = score.Systems;

            foreach (StaffSystem system in staffSystems)
            {
                if (system.LinePositions == null)
                    continue;
                foreach (var lines in system.LinePositions.Values)
                {
                    linesPositions.AddRange(AddValuesInBetween(lines));
                }
            }

            return linesPositions;
        }

        public static int GetStaffLineIndex(Score score, double yPosition)
        {
            foreach (StaffSystem system in score.Systems)
            {
                if (system.LinePositions == null || system.LinePositions.Count == 0)
                    continue;

                var lines = AddValuesInBetween(
                    system.LinePositions.Values.SelectMany(v => v).ToArray()
                );

                if (lines.Length == 0)
                    continue;

                int threshold = int.Parse(App.Configuration["snappingThreshold"], 0);

                double minY = lines.Min() - threshold;
                double maxY = lines.Max() + threshold;

                if (yPosition < minY || yPosition > maxY)
                    continue;

                int closestIndex = 0;
                double smallestDiff = double.MaxValue;

                for (int i = 0; i < lines.Length; i++)
                {
                    double diff = Math.Abs(lines[i] - yPosition);
                    if (diff < smallestDiff)
                    {
                        smallestDiff = diff;
                        closestIndex = i;
                    }
                }

                return closestIndex;
            }
            return -1;
        }


        public static double[] AddValuesInBetween(double[] values)
        {
            return values.SelectMany((v, i) => i < values.Length - 1
                    ? new[] { v, (v + values[i + 1]) / 2.0 }
                    : new[] { v })
                .ToArray();
        }


    }
}
