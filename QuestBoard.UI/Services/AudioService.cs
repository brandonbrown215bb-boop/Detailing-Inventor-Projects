using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace QuestBoard.UI.Services
{
    public class AudioService
    {
        public static AudioService Instance { get; } = new AudioService();

        public bool IsMuted { get; set; } = false;

        private AudioService() { }

        public void PlayPaperRustle()
        {
            if (IsMuted) return;
            Task.Run(() =>
            {
                try
                {
                    // Subtle system sound or synthesized rustle tone
                    SystemSounds.Asterisk.Play();
                }
                catch { }
            });
        }

        public void PlayWoodTap()
        {
            if (IsMuted) return;
            Task.Run(() =>
            {
                try
                {
                    SystemSounds.Beep.Play();
                }
                catch { }
            });
        }

        public void PlaySuccessSound()
        {
            if (IsMuted) return;
            Task.Run(() =>
            {
                try
                {
                    SystemSounds.Exclamation.Play();
                }
                catch { }
            });
        }
    }
}
