using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Services.MusicPlayback
{
    public interface IMusicPlaybackService
    {
        void Play(string path);
        void Stop();
        void Pause();
        void Resume();
    }
}
