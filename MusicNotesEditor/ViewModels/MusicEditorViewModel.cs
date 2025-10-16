using Manufaktura.Controls.Audio;
using Manufaktura.Controls.Desktop.Audio;
using Manufaktura.Controls.Model;
using Manufaktura.Music.Model;
using Manufaktura.Music.Model.MajorAndMinor;
using static System.Formats.Asn1.AsnWriter;

namespace MusicNotesEditor.ViewModels
{
    class MusicEditorViewModel : ViewModel
    {
        private ScorePlayer player;
        private Score data;
        public Score Data
        {
            get { return data; }
            set { data = value; OnPropertyChanged(() => Data); }
        }
        public void LoadTestData()
        {
            var score = Score.CreateOneStaffScore(Clef.Treble, new MajorScale(Step.C, false));
            score.FirstStaff.Elements.Add(new Note(Pitch.C5, RhythmicDuration.Quarter));
            score.FirstStaff.Elements.Add(new Note(Pitch.B4, RhythmicDuration.Quarter));
            score.FirstStaff.Elements.Add(new Note(Pitch.C5, RhythmicDuration.Half));
            score.FirstStaff.Elements.Add(new Note(Pitch.C5, RhythmicDuration.Half));
            score.FirstStaff.Elements.Add(new Barline()); 
            //xml parsing testing
            //var parser = new MusicXmlParser();
            //var score = parser.Parse(XDocument.Load(@"C:\Users\Dreamer\Documents\MuseScore4\Scores\testscore2.musicxml"));
            Data = score;
        }

        public void PlayScore()
        {
            player = player = new MidiTaskScorePlayer(Data);
            player.Play();
        }
    }
}
