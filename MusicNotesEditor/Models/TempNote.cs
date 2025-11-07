using Manufaktura.Controls.Model;
using Manufaktura.Music.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Models
{
    internal class TempNote : Note
    {
        private RhythmicDuration value;

        public TempNote(RhythmicDuration noteDuration)
        : base(noteDuration)
        {
        }

        public TempNote(Pitch pitch, RhythmicDuration noteDuration)
        : base(pitch, noteDuration)
        {
        }
    }
}
