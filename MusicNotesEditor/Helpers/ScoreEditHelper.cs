using Manufaktura.Controls.Model;
using Manufaktura.Music.Model;
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
            RhythmicDuration? currentNote, bool isRest)
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
            if (currentMeasure == null || currentMeasure.Elements.Count() == 1)
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

            int currentMeasureEndIndex = currentMeasureStartIndex + currentMeasure.Elements.Count() - 1;
            if (currentMeasure.Number == currentMeasure.Staff.Measures.Last().Number)
                currentMeasureEndIndex++;

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

            // Get time signature
            for (int i = 0; i < currentMeasureEndIndex; i++)
            {
                var element = staffElements[i];
                if (element is TimeSignature metrum)
                {
                    timeInMetrum = metrum.NumberValue;
                }
                if (element is Clef clef)
                {
                    lastClef = clef;
                }
            }

            var newNoteProportion = currentNote.Value.ToProportion();

            // Stop if chosen note takes more space than measure
            if (newNoteProportion > timeInMetrum)
            {
                // Add notification
                return;
            }

            // Replace temporary notes with rests
            for (int i = currentMeasureStartIndex; i < currentMeasureEndIndex; i++)
            {
                var tempNote = staffElements[i] as TempNote;
                if (tempNote != null)
                {
                    staffElements[i] = new CorrectRest(tempNote.Duration);
                }
            }

            Pitch pitch = PitchHelper.GetPitchFromIndex(staffLineIndex, lastClef);
            staffElements.Insert(elementOnLeftIndex + 1,
                new TempNote(pitch, currentNote.Value));

            // Remove stuff over metrum
            Proportion excessProportion = newNoteProportion;
            Proportion zeroProportion = new Proportion(0, 1);
            int startingIndex = elementOnLeftIndex + 1;
            int direction = Direction(isRightCloser);
            int cursor = Math.Min(startingIndex + direction, staffElements.Count());
            bool startOverridingNotes = false;
            int notCorrectRestNeighboursCount = 0;

            while (excessProportion > zeroProportion)
            {
                if (cursor >= staffElements.Count())
                {
                    direction = -1;
                    cursor = staffElements.Count() - 1;
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
                    if (rest.Duration.ToProportion() > excessProportion)
                    {
                        var proportionToFill = rest.Duration.ToProportion() - excessProportion;

                        var currentDuration = DurationHelper.HalfDuration(rest.Duration);
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
                else if (element is TempNote tempNote)
                {
                }
                else if (element is Note note)
                {
                    if (startOverridingNotes)
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
                    direction *= -1;
                }
                cursor += direction;
                startOverridingNotes = notCorrectRestNeighboursCount > 1;
            }


            // Replace new note with note
            for (int i = currentMeasureStartIndex; i < staffElements.Count(); i++)
            {
                var tempNote = staffElements[i] as TempNote;
                if (tempNote != null)
                {
                    staffElements[i] = new Note(tempNote.Pitch, tempNote.Duration);
                }
            }

            ScoreAdjustHelper.AdjustWidth(score, noteViewerContentWidth);
        }


        public static void DeleteElements(List<MusicalSymbol> selectedSymbols)
        {
            for (int i = 0; i < selectedSymbols.Count; i++)
            {
                if (selectedSymbols[i] is Note note)
                {
                    var symbolStaff = selectedSymbols[i].Measure.Staff;
                    var newRest = new CorrectRest(note.Duration);
                    symbolStaff.Elements[symbolStaff.Elements.IndexOf(selectedSymbols[i])] = newRest;
                    selectedSymbols[i] = newRest;
                }
            }

        }



        public static void Rerender(Score score)
        {
            score.FirstStaff.Elements.Add(new PrintSuggestion()
            {
                IsSystemBreak = false,
                IsPageBreak = false,
                IsBreakpointSet = false,
                IsVisible = false,
            });
            score.FirstStaff.Elements.RemoveAt(score.FirstStaff.Elements.Count - 1);
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
