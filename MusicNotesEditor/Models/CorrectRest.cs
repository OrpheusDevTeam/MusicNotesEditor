using Manufaktura.Controls.Model;
using Manufaktura.Controls.Model.Fonts;
using Manufaktura.Music.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Models
{
    internal class CorrectRest : Rest
    {
        public CorrectRest(RhythmicDuration restDuration) : base(restDuration)
        {
        }


        public override char GetCharacter(IMusicFont font)
        {
            if (base.BaseDuration == RhythmicDuration.Whole)
            {
                return font.RestWhole;
            }

            if (base.BaseDuration == RhythmicDuration.Half)
            {
                return font.RestHalf;
            }

            if (base.BaseDuration == RhythmicDuration.Quarter)
            {
                return font.RestQuarter;
            }

            if (base.BaseDuration == RhythmicDuration.Eighth)
            {
                return font.RestEighth;
            }

            if (base.BaseDuration == RhythmicDuration.Sixteenth)
            {
                return font.RestSixteenth;
            }

            if (base.BaseDuration == RhythmicDuration.D32nd)
            {
                return font.Rest32nd;
            }

            if (base.BaseDuration == RhythmicDuration.D64th)
            {
                return 'V';
            }

            return '\0';
        }

    }
}
