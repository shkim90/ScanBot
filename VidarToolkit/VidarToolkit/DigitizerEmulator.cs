using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace VidarToolkit
{
    public class DigitizerEmulator : IDigitizer
    {
        public DigitizerEmulator()
        {
            m_BitsPerPixel = SupportedBitsPerPixelValues[0];
            m_Resolution = SupportedResolutionValues[0];
        }

        public void Dispose()
        {
        }

        public bool IsConnected() => true;

        public ushort[] SupportedBitsPerPixelValues => new ushort[] { 8, 12, 16 };

        ushort m_BitsPerPixel;

        public ushort BitsPerPixel
        {
            get => m_BitsPerPixel;
            set
            {
                if (!SupportedBitsPerPixelValues.Contains(value))
                {
                    throw new ArgumentException("Out of supported values", nameof(value));
                }
                m_BitsPerPixel = value;
            }
        }

        public int BytesPerPixel => (m_BitsPerPixel + 7) / 8;

        public ushort[] SupportedResolutionValues => new ushort[] { 75, 150, 300, 570 };

        ushort m_Resolution;

        public ushort Resolution
        {
            get => m_Resolution;
            set
            {
                if (!SupportedResolutionValues.Contains(value))
                {
                    throw new ArgumentException("Out of supported values", nameof(value));
                }
                m_Resolution = value;
            }
        }

        public string SerialNumber => "000000";

        public bool AutoFeeder { get; set; }

        public bool HalfSpeed { get; set; }

        int m_StageCounter;

        public bool StageFilm() => m_StageCounter++ % 2 == 0;

        public void EjectFilm() => Thread.Sleep(TimeSpan.FromSeconds(2));

        bool m_Abort;

        public void AbortFilm()
        {
            m_Abort = true;
            m_StageCounter = 0;
        }

        public bool ScanFilm(Stream stream, double widthInInches, double lengthInInches, out short detectedWidth, out int detectedLength)
        {
            detectedWidth = (short)(Math.Min(widthInInches, 14) * m_Resolution);
            detectedLength = (int)(Math.Min(lengthInInches, 17) * m_Resolution);
            var stride = detectedWidth * BytesPerPixel;

            m_Abort = false;
            var line = CreateLine(detectedWidth);
            for (var i = 0; i < detectedLength; ++i)
            {
                if (m_Abort)
                {
                    break;
                }
                stream.Write(line, 0, line.Length);
                if ((i + 1) % 10 == 0 || i == detectedLength - 1)
                {
                    ScanningFilm?.Invoke(this, new ScanEventArgs((int)(stream.Position / stride)));
                }
                if (i % 10 == 0)
                {
                    Thread.Sleep(1);
                }
            }
            var scanned = !m_Abort;
            m_Abort = false;
            return scanned;
        }

        readonly Random m_Random = new Random();

        private byte[] CreateLine(int width)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    var offset = m_Random.Next(1 << m_BitsPerPixel);
                    for (var j = 0; j < width; ++j)
                    {
                        var c = ((j << m_BitsPerPixel - 11) + offset) % (1 << m_BitsPerPixel);
                        if (m_BitsPerPixel > 8)
                        {
                            writer.Write((ushort)c);
                        }
                        else
                        {
                            writer.Write((byte)c);
                        }
                    }
                }
                return stream.ToArray();
            }
        }

        public event EventHandler<ScanEventArgs> ScanningFilm;
    }
}
