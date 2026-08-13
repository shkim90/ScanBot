using MtToolkit;

namespace ScanBot.Shared
{
    partial class ScanConfig
    {
        ushort resolution;
        MtDensity density;
        int maxImageHeightInInches;
        bool rotate;

        protected override void OnInitialized()
        {
            resolution = Settings.Digitizer.Resolution;
            density = Settings.Digitizer.Density;
            maxImageHeightInInches = Settings.Digitizer.MaxImageHeightInInches;
            rotate = Settings.Digitizer.Rotate;
        }

        public void UpdateSettings()
        {
            Settings.Digitizer.Resolution = resolution;
            Settings.Digitizer.Density = density;
            Settings.Digitizer.MaxImageHeightInInches = maxImageHeightInInches;
            Settings.Digitizer.Rotate = rotate;
        }

        public void UpdateState() => StateHasChanged();
    }
}
