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

        public static int GetStaffLineIndex(Score score, double yPosition, out List<double> linePositions)
        {
            foreach (StaffSystem system in score.Systems)
            {
                if (system.LinePositions == null || system.LinePositions.Count == 0)
                    continue;

                int additionalStaffLines = int.Parse(App.Configuration["additionalStaffLines"], 0);
                var lines = AddValuesInBetween(
                    ExpandList(
                        system.LinePositions.Values.SelectMany(v => v).ToList(), additionalStaffLines
                    )
                );

                if (lines.Count() == 0)
                    continue;

                int threshold = int.Parse(App.Configuration["snappingThreshold"], 0);

                double minY = lines.Min() - threshold;
                double maxY = lines.Max() + threshold;

                if (yPosition < minY || yPosition > maxY)
                    continue;

                int closestIndex = 0;
                double smallestDiff = double.MaxValue;

                for (int i = 0; i < lines.Count(); i++)
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
            linePositions = [];
            return -1;
        }


        private static List<double> AddValuesInBetween(List<double> values)
        {
            return values.SelectMany((v, i) => i < values.Count() - 1
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


    }
}
