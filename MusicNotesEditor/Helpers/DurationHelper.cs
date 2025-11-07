using Manufaktura.Music.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Helpers
{
    public class DurationHelper
    {

        public static RhythmicDuration HalfDuration(RhythmicDuration duration)
        {
            Proportion proportion = duration.ToProportion() / 2;
            int powerOfTwoDenominator = (int)Math.Log2(proportion.Denominator);
            return new RhythmicDuration(powerOfTwoDenominator);
        }

    }
}
