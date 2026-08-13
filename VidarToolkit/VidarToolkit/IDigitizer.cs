using System;
using System.IO;

namespace VidarToolkit
{
    public interface IDigitizer : IDisposable
    {
        bool IsConnected();

        ushort[] SupportedBitsPerPixelValues { get; }

        ushort BitsPerPixel { get; set; }

        int BytesPerPixel { get; }

        ushort[] SupportedResolutionValues { get; }

        ushort Resolution { get; set; }

        string SerialNumber { get; }

        bool AutoFeeder { get; set; }

        bool HalfSpeed { get; set; }

        bool StageFilm();

        void EjectFilm();

        void AbortFilm();

        bool ScanFilm(Stream stream, double widthInInches, double lengthInInches, out short detectedWidth, out int detectedLength);

        event EventHandler<ScanEventArgs> ScanningFilm;
    }
}
