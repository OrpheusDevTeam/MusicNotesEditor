using Manufaktura.Controls.Model;
using Manufaktura.Music.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Helpers
{
    public class PitchHelper
    {
        private const double STEM_DIRECTION_CHANGE_LINE = 3;

        // Ordered step sequence repeating through the staff
        private static readonly List<Pitch> NaturalPitches = new()
        {
            Pitch.C1, Pitch.D1, Pitch.E1, Pitch.F1, Pitch.G1, Pitch.A1, Pitch.B1,
            Pitch.C2, Pitch.D2, Pitch.E2, Pitch.F2, Pitch.G2, Pitch.A2, Pitch.B2,
            Pitch.C3, Pitch.D3, Pitch.E3, Pitch.F3, Pitch.G3, Pitch.A3, Pitch.B3,
            Pitch.C4, Pitch.D4, Pitch.E4, Pitch.F4, Pitch.G4, Pitch.A4, Pitch.B4,
            Pitch.C5, Pitch.D5, Pitch.E5, Pitch.F5, Pitch.G5, Pitch.A5, Pitch.B5,
            Pitch.C6, Pitch.D6, Pitch.E6, Pitch.F6, Pitch.G6, Pitch.A6, Pitch.B6,
            Pitch.C7
        };

        public static Pitch GetPitchFromIndex(int index, Clef clef)
        {
            
            // Map each clef to a *reference pitch* for its top staff line (index = 1)
            Pitch topLinePitch = clef switch
            {
                { TypeOfClef: ClefType.GClef, Line: 2 } => Pitch.F5, // Treble
                { TypeOfClef: ClefType.GClef, Line: 1 } => Pitch.A5, // French Violin
                { TypeOfClef: ClefType.FClef, Line: 4 } => Pitch.A3, // Bass
                { TypeOfClef: ClefType.FClef, Line: 3 } => Pitch.C4, // Baritone F
                { TypeOfClef: ClefType.FClef, Line: 5 } => Pitch.F3, // Subbass
                { TypeOfClef: ClefType.CClef, Line: 1 } => Pitch.E5, // Soprano
                { TypeOfClef: ClefType.CClef, Line: 2 } => Pitch.C5, // Mezzo-soprano
                { TypeOfClef: ClefType.CClef, Line: 3 } => Pitch.A4, // Alto
                { TypeOfClef: ClefType.CClef, Line: 4 } => Pitch.F4, // Tenor
                { TypeOfClef: ClefType.CClef, Line: 5 } => Pitch.D4, // Baritone C
                _ => throw new ArgumentException("Unsupported clef", nameof(clef))
            };

            // Find the index of the reference pitch in the list
            int startIndex = NaturalPitches.FindIndex(p => 
            p.ToStep() == topLinePitch.ToStep() && p.Octave == topLinePitch.Octave);
            if (startIndex == -1)
                throw new InvalidOperationException($"Reference pitch {topLinePitch} not found in NaturalPitches list.");

            // Each index step moves one diatonic note downward (top line → lower positions)
            int targetIndex = startIndex - index;

            // Clamp if we go out of range
            targetIndex = Math.Max(0, Math.Min(NaturalPitches.Count - 1, targetIndex));

            return NaturalPitches[targetIndex];
        }

        public static void ShiftPitch(Note note, int numberOfShifts)
        {
            var oldPitch = note.Pitch;
            var staffLinePosition = note.GetLineInSpecificClef(ScoreDataExtractor.FindClefOfElement(note));

            VerticalDirection noteDirection = VerticalDirection.Up;
            if(staffLinePosition +  numberOfShifts * 0.5 >= STEM_DIRECTION_CHANGE_LINE)
                noteDirection = VerticalDirection.Down;

            note.StemDirection = noteDirection;

            int additionalStaffLines = App.Settings.AdditionalStaffLines.Value;

            int maxShift = (int)Math.Round((5 + additionalStaffLines - staffLinePosition ) * 2);
            int minShift = (int)Math.Round((1 - additionalStaffLines - staffLinePosition) * 2);

            if (numberOfShifts > maxShift || numberOfShifts < minShift || numberOfShifts == 0)
                return;

            Console.WriteLine($"SHIFTING {note} MAX: {maxShift} MIN: {minShift}");

            int startIndex = NaturalPitches.FindIndex(p =>
            p.StepName == oldPitch.StepName && p.Octave == oldPitch.Octave);
            if (startIndex == -1)
                throw new InvalidOperationException($"Reference pitch {oldPitch} not found in NaturalPitches list.");
            int targetIndex = startIndex + numberOfShifts;

            // Clamp if we go out of range
            targetIndex = Math.Max(0, Math.Min(NaturalPitches.Count - 1, targetIndex));

            note.Pitch = NaturalPitches[targetIndex];
        }


        private static Step GetStep(Pitch pitch) => pitch.ToStep();
        private static int GetOctave(Pitch pitch) => pitch.Octave;
    }
}
