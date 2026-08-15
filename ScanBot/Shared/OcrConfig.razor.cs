using Blazorise;
using System.Threading.Tasks;

namespace ScanBot.Shared
{
    partial class OcrConfig
    {
        string host;
        double confidence;
        bool recognizeOrientation;
        double mergeXDistance;
        double mergeYDistance;
        Validations validations;

        protected override void OnInitialized()
        {
            host = Settings.Ocr.Host;
            confidence = Settings.Ocr.Confidence;
            recognizeOrientation = Settings.Ocr.RecognizeOrientation;
            mergeXDistance = Settings.Ocr.MergeXDistanceInMm;
            mergeYDistance = Settings.Ocr.MergeYDistanceInMm;
        }

        public async Task<bool> ValidateSettings() => await validations.ValidateAll();

        public void UpdateSettings()
        {
            Settings.Ocr.Host = host.Trim();
            Settings.Ocr.Confidence = confidence;
            Settings.Ocr.RecognizeOrientation = recognizeOrientation;
            Settings.Ocr.MergeXDistanceInMm = mergeXDistance;
            Settings.Ocr.MergeYDistanceInMm = mergeYDistance;
        }
    }
}
