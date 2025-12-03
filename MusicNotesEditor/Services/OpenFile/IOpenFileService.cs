using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace MusicNotesEditor.Services.OpenFile
{
    public interface IOpenFileService
    {
        /// <summary>
        /// Validates a MusicXML file against XSD schemas
        /// </summary>
        /// <param name="filepath">Path to the MusicXML file</param>
        /// <returns>True if validation passes, false otherwise</returns>
        bool ValidateMusicXmlWithXsd(string filepath);

        /// <summary>
        /// Opens a file dialog to select a MusicXML file, validates it, and navigates to the editor if successful
        /// </summary>
        /// <param name="nav">Navigation service for page navigation</param>
        void SelectMusicXMLFile(NavigationService nav);

        /// <summary>
        /// Tests parsing of a MusicXML file (for internal use)
        /// </summary>
        /// <param name="filepath">Path to the MusicXML file</param>
        void TestData(string filepath);
    }
}