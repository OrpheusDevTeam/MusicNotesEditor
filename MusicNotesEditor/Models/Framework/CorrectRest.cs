using Manufaktura.Controls.Model;
using Manufaktura.Controls.Model.Fonts;
using Manufaktura.Music.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Models.Framework
{
    internal class CorrectRest : Rest
    {
        public CorrectRest(RhythmicDuration restDuration) : base(restDuration)
        {
        }


        public override char GetCharacter(IMusicFont font)
        {
            if (BaseDuration == RhythmicDuration.Whole)
            {
                return font.RestWhole;
            }

            if (BaseDuration == RhythmicDuration.Half)
            {
                return font.RestHalf;
            }

            if (BaseDuration == RhythmicDuration.Quarter)
            {
                return font.RestQuarter;
            }

            if (BaseDuration == RhythmicDuration.Eighth)
            {
                return font.RestEighth;
            }

            if (BaseDuration == RhythmicDuration.Sixteenth)
            {
                return font.RestSixteenth;
            }

            if (BaseDuration == RhythmicDuration.D32nd)
            {
                return font.Rest32nd;
            }

            if (BaseDuration == RhythmicDuration.D64th)
            {
                return 'V';
            }

            return '\0';
        }

    }
}
