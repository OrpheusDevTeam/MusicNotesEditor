using Manufaktura.Music.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Models
{
    public class NoteDuration
    {
        public string smuflChar;
        public RhythmicDuration duration;
        public string noteName = "";
        public string description = "";

        public NoteDuration(string smuflChar, RhythmicDuration duration, string noteName, string description)
        {
            this.smuflChar = smuflChar;
            this.duration = duration;
            this.noteName = noteName;
            this.description = description;
        }

        public static readonly List<NoteDuration> AvailableNotes = new List<NoteDuration>()
        {
            new NoteDuration("w", RhythmicDuration.Whole, "Whole Note", "Ctrl+1"),
            new NoteDuration("h", RhythmicDuration.Half, "Half Note", ""),
            new NoteDuration("q", RhythmicDuration.Quarter, "Quarter Note", ""),
            new NoteDuration("e", RhythmicDuration.Eighth, "Eighth Note", ""),
            new NoteDuration("s", RhythmicDuration.Sixteenth, "16th Note", ""),
            new NoteDuration("t", RhythmicDuration.D32nd, "32nd Note", ""),
            new NoteDuration("u", RhythmicDuration.D64th, "64th Note", ""),
        };
    }


}
