using ScanBot.Shared;
using System.Threading.Tasks;

namespace ScanBot.Pages
{
    partial class Config
    {
        StoreConfig storeConfig;
        ControlConfig controlConfig;
        ScanConfig scanConfig;
        OcrConfig ocrConfig;

        private async Task SubmitChanges()
        {
            if (!await storeConfig.ValidateSettings() | !await ocrConfig.ValidateSettings())
            {
                return;
            }

            storeConfig.UpdateSettings();
            controlConfig.UpdateSettings();
            scanConfig.UpdateSettings();
            ocrConfig.UpdateSettings();
            Settings.Save();

            ControlService.Stop();
            ScanService.Stop();
            UploadService.Stop();
            ScanService.Start();
            ControlService.Start();
            UploadService.Start();

            scanConfig.UpdateState();
            NavigationManager.NavigateTo("/", true);
        }
    }
}
