using Manufaktura.Controls.Model;
using Manufaktura.Controls.Parser;
using MusicNotesEditor.Helpers;
using MusicNotesEditor.Models.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MusicNotesEditor.Services.SaveFile
{
    public class FileSaveService : IFileSaveService
    {

        public bool SaveMusicXMLInternal(Score score, string filePath)
        {
            var stopwatch = Stopwatch.StartNew();
            if (filePath == null) return false;

            PrepareStaffForSerialization(score);

            try
            {
                var parser = new MusicXmlParser();
                XDocument musicXmlFile = parser.ParseBack(score);

                if (musicXmlFile == null)
                {
                    RestoreStaffAfterSerialization(score);
                    return false;
                }

                SaveXmlToFile(musicXmlFile, filePath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving XML: {ex.Message}");
                return false;
            }
            finally
            {
                RestoreStaffAfterSerialization(score);
                stopwatch.Stop();
                var totalTime = stopwatch.ElapsedMilliseconds;
                Console.WriteLine($"\n??????????????\n^^^^^^^^^^^^^^^^\nMusicEditorPage constructor completed in {totalTime}ms\n??????????????\n^^^^^^^^^^^^^^^^\n");
            }
        }

        private void PrepareStaffForSerialization(Score score)
        {
            foreach (var staff in score.Staves)
            {
                // Remove the barline at index 3
                if (staff.Elements.Count > 3)
                {
                    staff.Elements.RemoveAt(3);
                }

                // Convert CorrectRest to Rest
                for (int i = 0; i < staff.Elements.Count; i++)
                {
                    if (staff.Elements[i] is CorrectRest correctRest)
                    {
                        staff.Elements[i] = new Rest(correctRest.Duration);
                    }
                }
                ScoreAdjustHelper.FixMeasures(staff);
            }

        }

        private void RestoreStaffAfterSerialization(Score score)
        {
            for (int i = 0; i < score.Staves.Count; i++)
            {
                var staff = score.Staves[i];

                // Insert barline at index 3
                if (staff.Elements.Count >= 3)
                {
                    staff.Elements.Insert(3, new Barline(BarlineStyle.None));
                }

                // Convert Rest back to CorrectRest
                for (int j = 0; j < staff.Elements.Count; j++)
                {
                    if (staff.Elements[j] is Rest rest)
                    {
                        staff.Elements[j] = new CorrectRest(rest.Duration);
                    }
                }

                ScoreAdjustHelper.FixMeasures(staff);
            }
        }

        private void SaveXmlToFile(XDocument musicXmlFile, string filePath)
        {
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var writer = new StreamWriter(filePath, false, utf8NoBom))
            {
                musicXmlFile.Save(writer);
            }
        }
    }
}
