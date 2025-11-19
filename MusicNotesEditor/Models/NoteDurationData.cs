using Manufaktura.Music.Model;
using System.Collections.Generic;
using System.Windows.Input;

namespace MusicNotesEditor.Models
{
    public class NoteDurationData
    {
        public string SmuflChar { get; set; }
        public RhythmicDuration Duration { get; set; }
        public string NoteName { get; set; } = "";
        public string Description { get; set; } = "";
        public KeyGesture Shortcut { get; set; }

        public NoteDurationData(string smuflChar, RhythmicDuration duration, string noteName, string description, KeyGesture shortcut = null)
        {
            SmuflChar = smuflChar;
            Duration = duration;
            NoteName = noteName;
            Description = description;
            Shortcut = shortcut;
        }

        public static readonly List<NoteDurationData> AvailableNotes = new List<NoteDurationData>()
        {
            new NoteDurationData("w", RhythmicDuration.Whole, "Whole Note", "Ctrl+1", new KeyGesture(Key.D1, ModifierKeys.Control)),
            new NoteDurationData("h", RhythmicDuration.Half, "Half Note", "Ctrl+2", new KeyGesture(Key.D2, ModifierKeys.Control)),
            new NoteDurationData("q", RhythmicDuration.Quarter, "Quarter Note", "Ctrl+3", new KeyGesture(Key.D3, ModifierKeys.Control)),
            new NoteDurationData("e", RhythmicDuration.Eighth, "Eighth Note", "Ctrl+4", new KeyGesture(Key.D4, ModifierKeys.Control)),
            new NoteDurationData("s", RhythmicDuration.Sixteenth, "16th Note", "Ctrl+5", new KeyGesture(Key.D5, ModifierKeys.Control)),
            new NoteDurationData("t", RhythmicDuration.D32nd, "32nd Note", "Ctrl+6", new KeyGesture(Key.D6, ModifierKeys.Control)),
            new NoteDurationData("u", RhythmicDuration.D64th, "64th Note", "Ctrl+7", new KeyGesture(Key.D7, ModifierKeys.Control)),
        };

        public static string SmuflCharFromDuration(RhythmicDuration? duration)
        {
            var matchingDuration = AvailableNotes.FirstOrDefault(note => note.Duration == duration);
            return matchingDuration?.SmuflChar ?? "";
        }

    }
}
