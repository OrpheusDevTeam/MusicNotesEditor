using Manufaktura.Controls.Model;
using Manufaktura.Music.Model;
using Manufaktura.Music.Model.MajorAndMinor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Helpers
{
    public class ScoreProcessingHelper
    {
        public static Score ProcessScoreFromOMR(Score score, int numberOfParts)
        {
            Clef? lastClef = null;
            TimeSignature? lastTimeSignature = null;
            List<MusicalSymbol> symbolsToRemove = new List<MusicalSymbol>();

            if (numberOfParts < 1 && numberOfParts > 6)
            {
                numberOfParts = 1;
            }

            var breaks = score.FirstStaff.Elements.OfType<PrintSuggestion>();
            int count = 1;
            foreach(var systemBreak in breaks)
            {
                if (systemBreak.IsSystemBreak)
                    count++;
            }

            if (count % numberOfParts != 0)
                throw new InvalidCastException("The number of parts is invalid!");

            int partsToFindLeft = numberOfParts - 1;

            int index = 0;
            foreach (var element in score.FirstStaff.Elements)
            {

                if( element is PrintSuggestion printSuggestion && partsToFindLeft > 0)
                {
                    numberOfParts--;
                }
                var clef = score.FirstStaff.Elements[index + 1] as Clef;
                var key = score.FirstStaff.Elements[index + 2] as Key;
                TimeSignature? timeSignature = null;
                if(key is null)
                {
                    timeSignature = score.FirstStaff.Elements[index + 2] as TimeSignature;
                }
                if (timeSignature is null)
                {
                    timeSignature = score.FirstStaff.Elements[index + 3] as TimeSignature;
                }

                if (clef != null && timeSignature != null)
                {
                    score.AddStaff(clef, timeSignature, Step.C, MajorAndMinorScaleFlags.MajorSharp);
                }
                else
                {
                    score.AddStaff(Clef.Treble, TimeSignature.CommonTime, Step.C, MajorAndMinorScaleFlags.MajorSharp);
                }

                index++;
            }

            foreach (var staff in score.Staves)
            {
                foreach (var element in staff.Elements)
                {
                    switch(element)
                    {
                        case Clef clef:
                            if (lastClef == null)
                                lastClef = clef;
                            else
                            {
                                if (clef.TypeOfClef == lastClef.TypeOfClef && clef.Line == lastClef.Line)
                                    symbolsToRemove.Add(clef);
                                else
                                    lastClef = clef;
                            }
                            break;

                        case TimeSignature timeSignature:
                            if(lastTimeSignature == null)
                                lastTimeSignature = timeSignature;
                            else
                            {
                                if (timeSignature.NumberValue.Numerator == lastTimeSignature.NumberValue.Numerator
                                    && timeSignature.NumberValue.Denominator == lastTimeSignature.NumberValue.Denominator)
                                    symbolsToRemove.Add(timeSignature);
                                else
                                    lastTimeSignature = timeSignature;
                            }
                            break;
                    }
                }
            }

            foreach(var staff in score.Staves)
            {
                foreach(var elementToRemove in symbolsToRemove)
                {
                    staff.Elements.Remove(elementToRemove);
                }
            }

            return score;
        }
    }
}
