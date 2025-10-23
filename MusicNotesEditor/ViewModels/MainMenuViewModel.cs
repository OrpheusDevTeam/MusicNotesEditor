using System.Windows.Navigation;
using System.Xml.Linq;
using Manufaktura.Controls.Audio;
using Manufaktura.Controls.Desktop.Audio;
using Manufaktura.Controls.Model;
using Manufaktura.Controls.Parser;
using Manufaktura.Music.Model;
using Manufaktura.Music.Model.MajorAndMinor;
using MusicNotesEditor.Views;
using static System.Formats.Asn1.AsnWriter;


using System.Xml.Linq;
namespace MusicNotesEditor.ViewModels
{
    class MainMenuViewModel : ViewModel
    {
        public void TestData(string filepath)
        {
            var parser = new MusicXmlParser();
            var score = parser.Parse(XDocument.Load(filepath));
        }

        /*public bool ValidateMusicXmlWithXsd(string filepath)
        {
            try
            {
                // Configure validation settings
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessSchemaLocation;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        
                // Add the MusicXML schema
                settings.Schemas.Add("http://www.musicxml.org/xsd/partwise.dtd", "path/to/partwise.xsd");
                settings.Schemas.Add("http://www.musicxml.org/xsd/scorepartwise.xsd", "path/to/scorepartwise.xsd");
        
                // Attach validation event handler
                settings.ValidationEventHandler += new ValidationEventHandler(ValidationCallback);

                // Create and read the XML file
                using (XmlReader reader = XmlReader.Create(filepath, settings))
                {
                    while (reader.Read()) { }
                }
        
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Validation error: {ex.Message}");
                return false;
            }
        }*/
    }
}
