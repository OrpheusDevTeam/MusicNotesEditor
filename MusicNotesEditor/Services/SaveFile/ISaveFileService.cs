using Manufaktura.Controls.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Services.SaveFile
{
    public interface IFileSaveService
    {
        public bool SaveMusicXMLInternal(Score score, string filePath);
    }
}
