using MtToolkit;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ScanBot.Services
{
    class MtScanService : ScanServiceBase, IScanService
    {
        readonly Settings.DigitizerSettigns m_Settings;

        public MtScanService(Settings settings)
        {
            m_Settings = settings.Digitizer;
        }

        MtDigitizer m_Digitizer;

        public void Start()
        {
            try
            {
                m_Digitizer = new("NDT");
                m_Digitizer.BitsPerPixel = m_Digitizer.SupportedBitsPerPixelValues.Max();
                m_Digitizer.Resolution = m_Settings.Resolution;
                m_Digitizer.FrameArea = new(0, 0, 14, m_Settings.MaxImageHeightInInches);
                m_Digitizer.Density = m_Settings.Density;
                m_Digitizer.MultiChannelCrop = true;
                m_Digitizer.FilmScanned += Digitizer_FilmScanned;
                Log.Information("Digitizer connected");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
            }
        }

        public void Stop()
        {
            if (m_Digitizer != null)
            {
                m_Digitizer.FilmScanned -= Digitizer_FilmScanned;
                m_Digitizer.Dispose();
                m_Digitizer = null;
            }
            Log.Information("Digitizer disconnected");
        }

        public bool IsDigitizerConnected => m_Digitizer?.SerialNumber != null;

        public ushort Resolution
        {
            get => (ushort?)m_Digitizer?.Resolution ?? 0;
            set
            {
                if (m_Digitizer != null)
                {
                    m_Digitizer.Resolution = value;
                }
            }
        }

        public ushort[] SupportedResolutionValues => new ushort[] { 75, 150, 300, 600 };

        public void AddDigitizerInfo(Dictionary<string, string> tags)
        {
            tags[ModelNameKey] = "Microtek NDT-2000";
            tags[SerialNumberKey] = m_Digitizer?.SerialNumber ?? "";

            if (m_Digitizer != null)
            {
                tags[MinDensityKey] = m_Digitizer.DensityRange[0].ToString(CultureInfo.InvariantCulture);
                tags[MaxDensityKey] = m_Digitizer.DensityRange[1].ToString(CultureInfo.InvariantCulture);
            }
        }

        Guid m_ScanId;
        bool m_ErrorOnFilmScanned;

        public bool ScanFilm()
        {
            try
            {
                m_ScanId = Guid.NewGuid();
                m_ErrorOnFilmScanned = false;
                m_Digitizer.ScanFilm();
                return !m_ErrorOnFilmScanned;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
                return false;
            }
        }

        private async void Digitizer_FilmScanned(object sender, ImageEventArgs e)
        {
            try
            {
                var bytesPerRow = (e.Width * m_Digitizer.BytesPerPixel + 3) / 4 * 4;
                using var image = CreateImage(e.Data, e.Width, e.Data.Length / bytesPerRow, bytesPerRow);
                await HandleImage(image, Resolution, m_ScanId, m_Settings.Rotate);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
                m_ErrorOnFilmScanned = true;
            }
        }

        public void AbortFilm()
        {
        }

        public void EjectFilm()
        {
        }
    }
}
