using Manufaktura.Music.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MusicNotesEditor.Models
{
    public class AccidentalsData
    {
        public string SmuflChar { get; set; }
        public int Alter { get; set; } = 0;
        public string AccidentalName { get; set; } = "";
        public string Description { get; set; } = "";
        public KeyGesture Shortcut { get; set; }

        public AccidentalsData(string smuflChar, int alter, string accidentalName, string description, KeyGesture shortcut)
        {
            SmuflChar = smuflChar;
            Alter = alter;
            AccidentalName = accidentalName;
            Description = description;
            Shortcut = shortcut;
        }


        public static readonly List<AccidentalsData> AvailableAccidentals = new List<AccidentalsData>()
        {
            new AccidentalsData("Q", 2, "Rest", "Ctrl + R", new KeyGesture(Key.R, ModifierKeys.Control)),
            new AccidentalsData("b", -1, "Flat", "Ctrl + -", new KeyGesture(Key.OemMinus, ModifierKeys.Control)),
            new AccidentalsData("k", 0, "Natural", "Ctrl + =", new KeyGesture(Key.OemPlus, ModifierKeys.Control)),
            new AccidentalsData("X", 1, "Sharp", "Ctrl + +", new KeyGesture(Key.Add, ModifierKeys.Control))
        };

        public static Pitch AlterPitch(Pitch pitch, int accidental)
        {
            var step = pitch.ToStep();
            step.Alter = accidental;

            return Pitch.FromStep(step, pitch.Octave);
        }

    }

}
