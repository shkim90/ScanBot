using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidarToolkit;

namespace ScanBot.Services
{
    class VidarScanService : ScanServiceBase, IScanService
    {
        readonly Settings.DigitizerSettigns m_Settings;

        public VidarScanService(Settings settings)
        {
            m_Settings = settings.Digitizer;
        }

        IDigitizer m_Digitizer;

        public void Start()
        {
            try
            {
                m_Digitizer = m_Settings.Device == 0 ? new DigitizerEmulator() : new Digitizer();
                m_Digitizer.BitsPerPixel = m_Digitizer.SupportedBitsPerPixelValues.Max();
                m_Digitizer.Resolution = m_Settings.Resolution;
                Log.Information("Digitizer connected");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
            }
        }

        public void Stop()
        {
            m_Digitizer?.Dispose();
            m_Digitizer = null;
            Log.Information("Digitizer disconnected");
        }

        public bool IsDigitizerConnected => m_Digitizer?.IsConnected() == true;

        public ushort Resolution
        {
            get => m_Digitizer?.Resolution ?? 0;
            set
            {
                if (m_Digitizer != null)
                {
                    m_Digitizer.Resolution = value;
                }
            }
        }

        public ushort[] SupportedResolutionValues => m_Digitizer?.SupportedResolutionValues;

        public void AddDigitizerInfo(Dictionary<string, string> tags)
        {
            tags[ModelNameKey] = "VIDAR NDTPRO";
            tags[SerialNumberKey] = m_Digitizer?.SerialNumber ?? "";

            tags[MinDensityKey] = "0.5";
            tags[MaxDensityKey] = "4.595";
        }

        public bool ScanFilm()
        {
            if (m_Digitizer?.StageFilm() == true)
            {
                using var stream = new MemoryStream();
                var scanned = m_Digitizer.ScanFilm(stream, 14, m_Settings.MaxImageHeightInInches, out var width, out var height);
                m_Digitizer.EjectFilm();
                if (scanned)
                {
                    Task.Run(async () => await OnFilmScanned(stream.ToArray(), width, height));
                    return true;
                }
            }
            return false;
        }

        private async Task OnFilmScanned(byte[] data, short width, int height)
        {
            try
            {
                var scanId = Guid.NewGuid();
                var bytesPerRow = width * m_Digitizer.BytesPerPixel;
                using var image = CreateImage(data, width, height, bytesPerRow);
                foreach (var stripImage in image.SplitFilmIntoStrips(m_Settings.GapThreshold, Resolution))
                {
                    using (stripImage)
                    {
                        await HandleImage(stripImage, Resolution, scanId, m_Settings.Rotate);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
            }
        }

        public void AbortFilm() => m_Digitizer?.AbortFilm();

        public void EjectFilm() => m_Digitizer?.EjectFilm();
    }
}
