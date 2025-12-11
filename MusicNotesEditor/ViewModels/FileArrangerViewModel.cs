using System.Configuration;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Manufaktura.Controls.Audio;
using Manufaktura.Controls.Desktop.Audio;
using Manufaktura.Controls.Model;
using Manufaktura.Controls.Parser;
using Manufaktura.Music.Model;
using Manufaktura.Music.Model.MajorAndMinor;
using MusicNotesEditor.Views;

namespace MusicNotesEditor.ViewModels
{

    class FileArrangerViewModel : ViewModel
    {

        private int _numberOfParts = 1;

        public int NumberOfParts
        {
            get { return _numberOfParts; }
            set
            {
                if (value <= 6 && value >= 1 )
                    _numberOfParts = value;
                OnPropertyChanged(() => NumberOfParts);
            }
        }


    }
}
