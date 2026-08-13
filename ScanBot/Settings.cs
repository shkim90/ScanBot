using MtToolkit;
using Newtonsoft.Json;
using System;
using System.IO;

namespace ScanBot
{
    class Settings
    {
        public StoreSettings Store { get; set; } = new();

        public ControlSettings Control { get; set; } = new();

        public DigitizerSettigns Digitizer { get; set; } = new();

        public OcrSettings Ocr { get; set; } = new();

        public static Settings Load()
        {
            try
            {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FilePath));
            }
            catch
            {
                var settings = new Settings();
                settings.Save();
                return settings;
            }
        }

        public void Save() => File.WriteAllText(FilePath, JsonConvert.SerializeObject(this));

        private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(Settings) + ".json");

        public class StoreSettings
        {
            public string RootFolderPath { get; set; } = "";

            public string UploadFolderPath { get; set; } = "";

            public bool RotateOnUpload { get; set; }

            public int BitsPerPixelOnUpload { get; set; } = 16;

            public float[] DensityRangeOnUpload { get; set; } = { 0, 4.0f };

            public int AutoUploadDelay { get; set; } = 5;

            public string ExportFolderPath { get; set; } = "";

            public string ExportPathPattern { get; set; } = "yy-MM/dd";

            public bool SendToServer { get; set; }

            public string ServerHost { get; set; } = "localhost";

            public int ServerPort { get; set; } = 104;

            public string ServerAeTitle { get; set; } = "SERVER";

            public string ClientAeTitle { get; set; } = "CLIENT";

            [JsonIgnore]
            public DateTime? ScanDate { get; set; }

            public string ProtocolName { get; set; } = "";
        }

        public class ControlSettings
        {
            public string SerialPort { get; set; } = "COM1";

            public bool NewAutoFeeder { get; set; }
        }

        public class DigitizerSettigns
        {
            public int Device { get; set; }

            public ushort Resolution { get; set; } = 300;

            public MtDensity Density { get; set; } = MtDensity.D3_50;

            public int MaxImageHeightInInches { get; set; } = 17;

            public bool Rotate { get; set; } = true;

            public ushort GapThreshold { get; set; } = 63900;
        }

        public class OcrSettings
        {
            public double Confidence { get; set; } = 0.9;

            public bool RecognizeOrientation { get; set; }

            // Separate because same-line vertical jitter and same-field horizontal gaps are different
            // physical quantities - see the comment on Label.Merge for why one shared distance can't fit both.
            public double MergeXDistanceInMm { get; set; } = 8;

            public double MergeYDistanceInMm { get; set; } = 2;

            public int Engine { get; set; } = 1;

            public string Host { get; set; } = "magicndt.zotech.com.tw";
        }
    }
}
