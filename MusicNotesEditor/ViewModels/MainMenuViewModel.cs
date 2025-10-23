using System.Configuration;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using Manufaktura.Controls.Audio;
using Manufaktura.Controls.Desktop.Audio;
using Manufaktura.Controls.Model;
using Manufaktura.Controls.Parser;
using Manufaktura.Music.Model;
using Manufaktura.Music.Model.MajorAndMinor;
using MusicNotesEditor.Views;
using static System.Formats.Asn1.AsnWriter;
namespace MusicNotesEditor.ViewModels
{
    class MainMenuViewModel : ViewModel
    {
        public void TestData(string filepath)
        {
            var parser = new MusicXmlParser();
            var score = parser.Parse(XDocument.Load(filepath));
        }

        public bool ValidateMusicXmlWithXsd(string filepath)
        {
            try
            {
                // Track if validation failed
                bool validationFailed = false;

                // Configure validation settings
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessSchemaLocation;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                settings.DtdProcessing |= DtdProcessing.Parse;

                // CRITICAL: This makes validation fail on undeclared elements
                settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessIdentityConstraints;

                // Load the schema first to handle imports properly
                XmlSchemaSet schemas = new XmlSchemaSet();

                // Add the imported schemas that MusicXML depends on
                schemas.Add("http://www.w3.org/XML/1998/namespace", "xml.xsd");
                schemas.Add("http://www.w3.org/1999/xlink", "xlink.xsd");

                // Add the main MusicXML schema (no namespace)
                schemas.Add(null, "musicxml.xsd"); // null = no namespace

                settings.Schemas = schemas;

                // Attach validation event handler
                settings.ValidationEventHandler += (sender, args) =>
                {
                    if (args.Severity == XmlSeverityType.Warning)
                        Console.WriteLine($"\tWarning: {args.Message}");
                    else
                    {
                        Console.WriteLine($"\tValidation error: {args.Message}");
                        validationFailed = true;  // Mark as failed
                    }
                };

                // Create and read the XML file
                using (XmlReader reader = XmlReader.Create(filepath, settings))
                {
                    while (reader.Read()) { }
                }
        
                return !validationFailed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema error: {ex.Message}");
                return false;
            }
        }
    }
}
