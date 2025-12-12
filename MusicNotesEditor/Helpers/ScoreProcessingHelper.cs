using Manufaktura.Controls.Model;
using Manufaktura.Controls.WPF;
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
            //int count = 0;
            //foreach(var systemBreak in breaks)
            //{
            //    if (systemBreak.IsSystemBreak)
            //        count++;
            //}


            //var staves2 = score.Staves;
            //for (int i = 0; i < staves2.Count; i++)
            //{
            //    var elements = staves2[i].Elements;

            //    for (int j = 0; j < elements.Count; j++)
            //    {
            //        Console.WriteLine($"\tStave: {i + 1} Measure: Element: {j + 1}. {elements[j]} ");
            //        //if (elements[j] is Note note)
            //        //    Console.WriteLine($"Lyrics: {string.Join(" | ", note.Lyrics)}");

            //    }
            //}


            //Console.WriteLine($"PREPROCESSING DATA COUNT PARTS: {count}");
            //if (count % numberOfParts != 0)
            //    throw new InvalidCastException("The number of parts is invalid!");

            //int partsToFindLeft = numberOfParts - 1;

            //int index = 0;


            //foreach (var element in score.FirstStaff.Elements)
            //{
            //    if (element is PrintSuggestion printSuggestion && partsToFindLeft > 0)
            //    {
            //        partsToFindLeft--;
            //    }
            //    else
            //    {
            //        continue;
            //    }
            //    var clef = score.FirstStaff.Elements[index + 1] as Clef;
            //    var key = score.FirstStaff.Elements[index + 2] as Key;
            //    TimeSignature? timeSignature = null;
            //    if (key is null)
            //    {
            //        timeSignature = score.FirstStaff.Elements[index + 2] as TimeSignature;
            //    }
            //    if (timeSignature is null)
            //    {
            //        timeSignature = score.FirstStaff.Elements[index + 3] as TimeSignature;
            //    }

            //    if (clef != null && timeSignature != null)
            //    {
            //        Console.WriteLine($"PREPROCESSING ADDING NORMALLY {index}");
            //        score.AddStaff(clef, timeSignature, Step.C, MajorAndMinorScaleFlags.MajorSharp);
            //        score.Staves.Last().Elements.Add(timeSignature);
            //    }
            //    else
            //    {
            //        Console.WriteLine($"PREPROCESSING ADDING FALLBACK {index}");
            //        score.AddStaff(Clef.Treble, TimeSignature.CommonTime, Step.C, MajorAndMinorScaleFlags.MajorSharp);
            //        score.Staves.Last().Elements.Add(TimeSignature.CommonTime);
            //    }
            //}

            //Dictionary<int, List<MusicalSymbol>> symbolsPerStaff = new Dictionary<int, List<MusicalSymbol>>();
            //int currentStaff = -1;

            //foreach (var staff in score.Staves)
            //{
            //    for (int i = 0; i < staff.Elements.Count; i++)
            //    {
            //        var element = staff.Elements[i];
            //        Console.WriteLine($"xdxdxdQWERTY: {element}");
            //        if (element is PrintSuggestion systemBreak && systemBreak.IsSystemBreak)
            //        {
            //            Console.WriteLine($"xdxdxdQWERTY: {i} currentStaff : {currentStaff}");
            //            currentStaff++;
            //            currentStaff = currentStaff % numberOfParts; 
            //        }
            //        int currentStaffIndex = Math.Max( currentStaff, 0 );

            //        if (!symbolsPerStaff.ContainsKey(currentStaffIndex))
            //        {
            //            symbolsPerStaff[currentStaffIndex] = new List<MusicalSymbol>();
            //        }

            //        symbolsPerStaff[currentStaffIndex].Add(element);
            //    }
            //}

            //foreach (var kvp in symbolsPerStaff)
            //{
            //    score.Staves[kvp.Key].Elements.Clear();
            //    score.Staves[kvp.Key].Elements.AddRange(kvp.Value);
            //}


            foreach (var staff in score.Staves)
            {
                var isLastElementBarline = false;
                var singleNoteOrRestDetected = false;
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
                            isLastElementBarline = false;
                            break;

                        case TimeSignature timeSignature:
                            if(lastTimeSignature == null || !singleNoteOrRestDetected)
                                lastTimeSignature = timeSignature;
                            else
                            {
                                if (timeSignature.NumberValue.Numerator == lastTimeSignature.NumberValue.Numerator
                                    && timeSignature.NumberValue.Denominator == lastTimeSignature.NumberValue.Denominator)
                                    symbolsToRemove.Add(timeSignature);
                                else
                                    lastTimeSignature = timeSignature;
                            }
                            isLastElementBarline = false;
                            break;

                        case Barline barline:
                            if (isLastElementBarline || !singleNoteOrRestDetected)
                                symbolsToRemove.Add(barline);
                            isLastElementBarline = true;
                            break;

                        case NoteOrRest noteOrRest:
                            singleNoteOrRestDetected = true;
                            isLastElementBarline = false;
                            break;

                        case PrintSuggestion print:
                            isLastElementBarline = false;
                            singleNoteOrRestDetected = false;
                            break;
                        default:
                            isLastElementBarline = false;
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
                for(int i = 0; i < staff.Elements.Count; i++)
                {
                    if(staff.Elements[i] is Clef clef && clef.TypeOfClef == ClefType.FClef)
                    {
                        staff.Elements[i] = Clef.Bass;
                    }
                }
            }

            foreach( var staff in score.Staves)
            {
                bool bassClef = false;
                foreach(var element in staff.Elements)
                {
                    if(element is Clef clef)
                    {
                        bassClef = clef.TypeOfClef == ClefType.FClef && clef.Line == 4;
                    }
                    if (element is Note note && bassClef)
                    {
                        PitchHelper.ShiftPitch(note, -12);
                    }

                }
            }

            return score;
        }
    }
}
