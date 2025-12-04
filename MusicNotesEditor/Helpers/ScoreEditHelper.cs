using Manufaktura.Controls.Model;
using Manufaktura.Controls.WPF;
using Manufaktura.Music.Model;
using MusicNotesEditor.Models;
using MusicNotesEditor.Models.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MusicNotesEditor.Helpers
{
    internal class ScoreEditHelper
    {

        private const int TEMP_NOTE_OFFSET = 8;

        public static void InsertNote(Score score, double clickXPos, double clickYPos, double noteViewerContentWidth,
            RhythmicDuration? currentNote, bool isRest, int accidental, NoteViewer noteViewer, int currentPageIndex)
        {
            // Additional Staff lines gives twice as many additional positions for notes to be in
            var additionalPositions = App.Settings.AdditionalStaffLines.Value * 2;
            var staffLineIndex = ScoreDataExtractor.GetStaffLineIndex(score, clickYPos, out var _) - additionalPositions;

            if (currentNote == null || staffLineIndex == -1 - additionalPositions)
            {
                return;
            }

            var currentMeasure = ScoreDataExtractor.GetMeasure(score, clickXPos, clickYPos);
            Console.WriteLine("\nMeasuring: ");
            Console.WriteLine(currentMeasure);
            Console.WriteLine($"\nCLICK: \n\tX: {clickXPos} \n\tY: {clickYPos} LINE INDEX: {staffLineIndex}");
            if (currentMeasure == null || currentMeasure.Elements.Count == 1)
            {
                return;
            }

            var staffElements = currentMeasure.Staff.Elements;
            int currentMeasureStartIndex = -1;

            // Get Start index of measure
            for (int i = 0; i < staffElements.Count; i++)
            {
                if (staffElements[i].Measure == currentMeasure)
                {
                    currentMeasureStartIndex = i;
                    break;
                }
            }
            Console.WriteLine($"START INDEXING!!!!!!!!!!!!!!!: {currentMeasureStartIndex}");
            if (currentMeasure.Elements.Last() is PrintSuggestion)
                currentMeasure.Elements.RemoveAt(currentMeasure.Elements.Count - 1);
            int currentMeasureEndIndex = currentMeasureStartIndex + currentMeasure.Elements.Count - 1;
            Console.WriteLine($"summing INDEXING!!!!!!!!!!!!!!!: {string.Join(", ",currentMeasure.Elements)}");
            if (currentMeasure.Number == currentMeasure.Staff.Measures.Last().Number)
                currentMeasureEndIndex++;
            Console.WriteLine($"REND INDEXING!!!!!!!!!!!!!!!: {currentMeasureEndIndex}");


            // Replace rests with temporary notes
            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var rest = staffElements[i] as CorrectRest;
                if (rest != null)
                {
                    staffElements[i] = new TempNote(rest.Duration);
                }
            }

            // Put empty print suggestion to force rerendering
            Rerender(score);

            int elementOnLeftIndex = -1;

            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var element = staffElements[i];
                Console.WriteLine($"Element: {element}");
                Console.WriteLine($"\tMeasure: {element.Measure}");

                var elementXPosition = ScoreDataExtractor.HorizontalPosition(element);

                if (element is TempNote)
                {
                    elementXPosition += TEMP_NOTE_OFFSET;
                }

                if (elementXPosition <= clickXPos)
                {
                    Console.WriteLine($"ElementXPOS: {elementXPosition}\tclickXPos: {clickXPos} Result:{elementXPosition <= clickXPos}");
                    Console.WriteLine($"Lefty: {element}");
                    elementOnLeftIndex = i;
                }
                else if (elementXPosition > clickXPos)
                {
                    break;
                }
            }

            if (elementOnLeftIndex < 0)
            {
                elementOnLeftIndex = currentMeasureStartIndex - 1;
            }

            bool isRightCloser = false;

            if (elementOnLeftIndex + 1 < staffElements.Count)
            {

                var elementOnLeftXPosition = ScoreDataExtractor.HorizontalPosition(
                    staffElements[elementOnLeftIndex]);

                var elementOnRightXPosition = ScoreDataExtractor.HorizontalPosition(
                staffElements[elementOnLeftIndex + 1]);

                isRightCloser = Math.Abs(elementOnLeftXPosition - clickXPos)
                    > Math.Abs(elementOnRightXPosition - clickXPos);
            }

            Proportion? timeInMetrum = null;
            Clef? lastClef = null;
            Proportion currentTimeInMetrum = new Proportion(0, 1);

            // Get time signature
            for (int i = 0; i < currentMeasureEndIndex; i++)
            {
                var element = staffElements[i];
                Console.WriteLine(element.Measure);
                if (element is TimeSignature metrum)
                {
                    timeInMetrum = metrum.NumberValue;
                }
                if (element is Clef clef)
                {
                    lastClef = clef;
                }
                
            }

            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var element = staffElements[i];;
                
                if (element is NoteOrRest noteOrRest)
                {
                    currentTimeInMetrum += noteOrRest.Duration.ToProportion();

                    Console.WriteLine($"CURRENT TIME SO FAR : {currentTimeInMetrum}");
                }
                
            }

            var newNoteProportion = currentNote.Value.ToProportion();

            
            // Replace temporary notes with rests
            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var tempNote = staffElements[i] as TempNote;
                if (tempNote != null)
                {
                    staffElements[i] = new CorrectRest(tempNote.Duration);
                }
            }

            // Stop if chosen note takes more space than measure
            if (newNoteProportion > timeInMetrum || currentTimeInMetrum > timeInMetrum)
            {
                Console.WriteLine($"LEAVING EARLY: {currentTimeInMetrum}");
                ScoreAdjustHelper.FixMeasures(currentMeasure.Staff);

                // Add notification
                return;
            }


            Pitch pitch = PitchHelper.GetPitchFromIndex(staffLineIndex, lastClef);
            var newTempNote = new TempNote(pitch, currentNote.Value);
            staffElements.Insert(elementOnLeftIndex + 1,
                 newTempNote);

            // Remove stuff over metrum
            Proportion excessProportion = newNoteProportion;

            Console.WriteLine($"OLD PROPORTION: {excessProportion} CURRENT: {currentTimeInMetrum}");
            excessProportion += currentTimeInMetrum - timeInMetrum ?? new Proportion(0,1);

            Console.WriteLine($"NEW PROPORTION: {excessProportion}");
            Proportion zeroProportion = new Proportion(0, 1);
            int startingIndex = elementOnLeftIndex + 1;
            int direction = Direction(isRightCloser);
            int cursor = Math.Min(startingIndex + direction, staffElements.Count);
            bool startOverridingNotes = false;
            int notCorrectRestNeighboursCount = 0;

            while (excessProportion > zeroProportion)
            {
                if (cursor >= staffElements.Count)
                {
                    direction = -1;
                    cursor = staffElements.Count - 1;
                    notCorrectRestNeighboursCount++;
                    continue;
                }
                else if (cursor < currentMeasureStartIndex)
                {
                    direction = 1;
                    cursor = currentMeasureStartIndex;
                    notCorrectRestNeighboursCount++;
                    continue;
                }
                var element = staffElements[cursor];

                if (element is CorrectRest rest)
                {
                    if( (startOverridingNotes && isRest) || !isRest)
                    {
                        if (rest.Duration.ToProportion() > excessProportion)
                        {
                            var proportionToFill = rest.Duration.ToProportion() - excessProportion;

                            var currentDuration = DurationHelper.HalfDuration(rest.Duration);
                            while (currentDuration.ToProportion() > proportionToFill)
                            {
                                currentDuration = DurationHelper.HalfDuration(currentDuration);
                            }
                            rest.Duration = currentDuration;
                            proportionToFill -= currentDuration.ToProportion();

                            while (proportionToFill != zeroProportion)
                            {
                                currentDuration = DurationHelper.HalfDuration(currentDuration);
                                Console.WriteLine($"\tDuration: {currentDuration}");
                                Console.WriteLine($"\tProportion: {proportionToFill}");

                                if (currentDuration.ToProportion() > proportionToFill)
                                {
                                    continue;
                                }
                                staffElements.Insert(cursor, new CorrectRest(currentDuration));
                                proportionToFill -= currentDuration.ToProportion();
                            }
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"Removed Element: {staffElements[cursor]}");
                            staffElements.RemoveAt(cursor);
                            excessProportion -= rest.Duration.ToProportion();
                            cursor--;
                        }
                    }
                    else
                    {
                        direction *= -1;
                        notCorrectRestNeighboursCount++;
                    }
                }
                else if (element is TempNote tempNote)
                {
                }
                else if (element is Note note)
                {
                    if ( (startOverridingNotes && !isRest) || isRest )
                    {
                        if (note.Duration.ToProportion() > excessProportion)
                        {
                            var proportionToFill = note.Duration.ToProportion() - excessProportion;

                            var currentDuration = DurationHelper.HalfDuration(note.Duration);
                            while (currentDuration.ToProportion() > proportionToFill)
                            {
                                currentDuration = DurationHelper.HalfDuration(currentDuration);
                            }
                            note.Duration = currentDuration;
                            proportionToFill -= currentDuration.ToProportion();

                            while (proportionToFill != zeroProportion)
                            {
                                currentDuration = DurationHelper.HalfDuration(currentDuration);

                                if (currentDuration.ToProportion() > proportionToFill)
                                {
                                    Console.WriteLine($"\tSkipping Duration: {currentDuration.ToProportion()}");
                                    continue;
                                }

                                Console.WriteLine($"\tDuration: {currentDuration}");
                                Console.WriteLine($"\tProportion: {proportionToFill}");
                                staffElements.Insert(cursor, new Note(note.Pitch, currentDuration));
                                proportionToFill -= currentDuration.ToProportion();
                            }
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"Removed Element: {staffElements[cursor]}");
                            staffElements.RemoveAt(cursor);
                            excessProportion -= note.Duration.ToProportion();
                            cursor--;
                        }
                    }
                    else
                    {
                        direction *= -1;
                        notCorrectRestNeighboursCount++;
                    }
                }
                else if (element is Barline)
                {
                    direction *= -1;
                    notCorrectRestNeighboursCount++;
                }
                else
                {
                }
                cursor += direction;
                startOverridingNotes = notCorrectRestNeighboursCount > 1;
            }

            
            // Replace new note with note
            for (int i = currentMeasureStartIndex; i < staffElements.Count; i++)
            {
                if (staffElements[i] is Note note)
                    PitchHelper.ShiftPitch(note, 0);

                var tempNote = staffElements[i] as TempNote;
                if (tempNote != null)
                {
                    if(isRest)
                    {
                        staffElements[i] = new CorrectRest(tempNote.Duration);
                    }
                    else
                    {
                        var newPitch = AccidentalsData.AlterPitch(tempNote.Pitch, accidental);
                        staffElements[i] = new Note(newPitch, tempNote.Duration, tempNote.StemDirection);
                    }
                }
            }

            ScoreAdjustHelper.AdjustWidth(score, noteViewerContentWidth, currentPageIndex);
            
        }


        public static bool DeleteElements(List<MusicalSymbol> selectedSymbols)
        {
            var result = true;
            for (int i = 0; i < selectedSymbols.Count; i++)
            {
                Proportion? timeInMetrum = null;
                Proportion takenDuration = new Proportion(0, 1);
                var symbolStaff = selectedSymbols[i].Measure.Staff;
                // Get time signature
                for (int j = 0; j < symbolStaff.Elements.Count; j++)
                {
                    var element = symbolStaff.Elements[j];
                    if (element == selectedSymbols[i])
                        break;
                    if (element is TimeSignature metrum)
                    {
                        timeInMetrum = metrum.NumberValue;
                    }
                }

                if (timeInMetrum != null)
                {
                    foreach (var element in selectedSymbols[i].Measure.Elements)
                    {
                        if(element is NoteOrRest noteOrRest)
                        {
                            takenDuration += noteOrRest.Duration.ToProportion();
                        }
                    }
                    if (takenDuration > timeInMetrum && selectedSymbols[i] is NoteOrRest elementWithDuration)
                    {
                        int selectedElementIndex = symbolStaff.Elements.IndexOf(selectedSymbols[i]);
                        symbolStaff.Elements.RemoveAt(selectedElementIndex);

                        Console.WriteLine($"SELETING DELETING AND NOW FIXING BEFORE: {takenDuration}");
                        takenDuration -= elementWithDuration.Duration.ToProportion();
                        var fillingDuration = elementWithDuration.Duration;

                        Console.WriteLine($"SELETING DELETING AND NOW FIXING: {takenDuration}");
                        while (takenDuration < timeInMetrum)
                        {

                            Console.WriteLine($"SELETING DELETING AND NOW FIXING IN LOPP: {timeInMetrum - takenDuration} > {fillingDuration} ");
                            if (timeInMetrum - takenDuration < fillingDuration.ToProportion())
                            {
                                fillingDuration = DurationHelper.HalfDuration(fillingDuration);
                                if (timeInMetrum - takenDuration < fillingDuration.ToProportion())
                                    continue;
                            }
                            if(elementWithDuration is Note note1)
                            {
                                symbolStaff.Elements.Insert(selectedElementIndex, new Note(note1.Pitch, fillingDuration));
                            }
                            if (elementWithDuration is CorrectRest rest)
                            {
                                symbolStaff.Elements.Insert(selectedElementIndex, new CorrectRest(fillingDuration));
                            }

                            takenDuration += fillingDuration.ToProportion();
                        }
                        result = false;
                        continue;
                    }
                }

                if (selectedSymbols[i] is Note note)
                {
                    var newRest = new CorrectRest(note.Duration);
                    symbolStaff.Elements[symbolStaff.Elements.IndexOf(selectedSymbols[i])] = newRest;
                    selectedSymbols[i] = newRest;
                }
            }

            return result;
        }



        public static void Rerender(Score score, NoteViewer? noteViewer = null,  List<MusicalSymbol>? selectedElements = null)
        {
            score.FirstStaff.Elements.Add(new PrintSuggestion()
            {
                IsSystemBreak = false,
                IsPageBreak = false,
                IsBreakpointSet = false,
                IsVisible = false,
            });
            score.FirstStaff.Elements.RemoveAt(score.FirstStaff.Elements.Count - 1);

            MeasureHelper.ValidateMeasures(score, noteViewer);

            if(noteViewer != null && selectedElements != null)
                SelectionHelper.ColorSelectedElements(noteViewer, selectedElements);
        }

        private static int Direction(bool value)
        {
            return value ? 1 : -1;
        }


        public static void AddNewLine(Staff staff, int index)
        {

            var lineBreak = new PrintSuggestion() { IsSystemBreak = true };
            staff.Elements.Insert(index, lineBreak);
        }
    }
}
