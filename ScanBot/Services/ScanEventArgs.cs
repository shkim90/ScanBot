using Emgu.CV;
using Emgu.CV.Structure;
using System;

namespace ScanBot.Services
{
    public class ScanEventArgs
    {
        public Image<Gray, ushort> Image { get; init; }

        public ushort Resolution { get; init; }

        public Guid ScanId { get; init; }

        public bool Imported { get; init; }
    }
}
