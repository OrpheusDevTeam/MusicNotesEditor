using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MusicNotesEditor.Services.MusicPlayback
{
    public class MusicPlaybackService : IMusicPlaybackService
    {
        private readonly MediaPlayer _player = new();

        public void Play(string path)
        {
            _player.Open(new Uri(path, UriKind.RelativeOrAbsolute));
            //_player.Volume = 1.0;
            Console.WriteLine($"MUSIC  PLAYING: {_player.Volume}");
            _player.Play();
        }

        public void Stop() => _player.Stop();
        public void Pause() => _player.Pause();
        public void Resume() => _player.Play();
    }
}
