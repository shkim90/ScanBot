using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    interface IScanService
    {
        void Start();

        void Stop();

        bool IsDigitizerConnected { get; }

        ushort Resolution { get; set; }

        ushort[] SupportedResolutionValues { get; }

        public void AddDigitizerInfo(Dictionary<string, string> tags);

        bool ScanFilm();

        void AbortFilm();

        void EjectFilm();

        Task ImportImageFile(byte[] data, ushort resolution, bool rotate = false);

        event Func<ScanEventArgs, Task> FilmScanned;
    }
}
