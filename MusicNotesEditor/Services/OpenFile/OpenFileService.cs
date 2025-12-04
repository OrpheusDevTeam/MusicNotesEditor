using Manufaktura.Controls.Model;
using Manufaktura.Controls.Parser;
using Microsoft.Win32;
using MusicNotesEditor.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace MusicNotesEditor.Services.OpenFile
{
    public class OpenFileService : IOpenFileService
    {

        public void TestData(string filepath)
        {
            var parser = new MusicXmlParser();
            var score = parser.Parse(XDocument.Load(filepath));
        }

        public bool ValidateMusicXmlWithXsd(string filepath)
        {
            return true;
            //try
            //{
            //    // Track if validation failed
            //    bool validationFailed = false;

            //    // Configure validation settings
            //    XmlReaderSettings settings = new XmlReaderSettings();
            //    settings.ValidationType = ValidationType.Schema;
            //    settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
            //    settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessSchemaLocation;
            //    settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            //    settings.DtdProcessing |= DtdProcessing.Parse;

            //    // CRITICAL: This makes validation fail on undeclared elements
            //    settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessIdentityConstraints;

            //    // Load the schema first to handle imports properly
            //    XmlSchemaSet schemas = new XmlSchemaSet();

            //    // Add the imported schemas that MusicXML depends on
            //    schemas.Add("http://www.w3.org/XML/1998/namespace", "Assets\\xml.xsd");
            //    schemas.Add("http://www.w3.org/1999/xlink", "Assets\\xlink.xsd");

            //    // Add the main MusicXML schema (no namespace)
            //    schemas.Add(null, "Assets\\musicxml.xsd"); // null = no namespace

            //    settings.Schemas = schemas;

            //    // Attach validation event handler
            //    settings.ValidationEventHandler += (sender, args) =>
            //    {
            //        if (args.Severity == XmlSeverityType.Warning)
            //            Console.WriteLine($"\tWarning: {args.Message}");
            //        else
            //        {
            //            Console.WriteLine($"\tValidation error: {args.Message}");
            //            validationFailed = true;  // Mark as failed
            //        }
            //    };

            //    // Create and read the XML file
            //    using (XmlReader reader = XmlReader.Create(filepath, settings))
            //    {
            //        while (reader.Read()) { }
            //    }

            //    return !validationFailed;
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Schema error: {ex.Message}");
            //    return false;
            //}
        }


        private string SelectXMLs()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Configure the dialog
            openFileDialog.Title = "Select a file";
            openFileDialog.Filter = "MusicXML files (*.musicxml)|*.musicxml|XML files (*.xml)|*xml|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Multiselect = false;

            // Show the dialog
            bool? result = openFileDialog.ShowDialog();

            // Process the result
            if (result == true)
            {
                return openFileDialog.FileName;
            }

            return "";
        }

        public void SelectMusicXMLFile(NavigationService nav)
        {
            string filepath = SelectXMLs();
            try
            {
                TestData(filepath);
                if (ValidateMusicXmlWithXsd(filepath) == true)
                {
                    nav.Navigate(new MusicEditorPage(filepath));
                }
                else
                {
                    MessageBox.Show("There was an error loading the chosen file, please ensure the format and content are follow the correct MusicXML standard.", "File import error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("There was an error loading the chosen file, please ensure the format and content are follow the correct MusicXML standard.", "File import error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
