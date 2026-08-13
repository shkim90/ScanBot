using Blazorise;
using System.Threading.Tasks;
using System;

namespace ScanBot.Shared
{
    partial class StoreConfig
    {
        string rootFolderPath;
        string uploadFolderPath;
        bool rotateOnUpload;
        bool sendToServer;
        string serverHost;
        int serverPort;
        string serverAeTitle;
        string clientAeTitle;
        DateTime? scanDate;
        string protocolName;
        Validations validations;

        protected override void OnInitialized()
        {
            rootFolderPath = Settings.Store.RootFolderPath;
            uploadFolderPath = Settings.Store.UploadFolderPath;
            rotateOnUpload = Settings.Store.RotateOnUpload;
            sendToServer = Settings.Store.SendToServer;
            serverHost = Settings.Store.ServerHost;
            serverPort = Settings.Store.ServerPort;
            serverAeTitle = Settings.Store.ServerAeTitle;
            clientAeTitle = Settings.Store.ClientAeTitle;
            scanDate = Settings.Store.ScanDate;
            protocolName = Settings.Store.ProtocolName;
        }

        public async Task<bool> ValidateSettings() => await validations.ValidateAll();

        public void UpdateSettings()
        {
            Settings.Store.RootFolderPath = rootFolderPath.Trim();
            Settings.Store.UploadFolderPath = uploadFolderPath.Trim();
            Settings.Store.RotateOnUpload = rotateOnUpload;
            Settings.Store.SendToServer = sendToServer;
            Settings.Store.ServerHost = serverHost.Trim();
            Settings.Store.ServerPort = serverPort;
            Settings.Store.ServerAeTitle = serverAeTitle.Trim();
            Settings.Store.ClientAeTitle = clientAeTitle.Trim();
            Settings.Store.ScanDate = scanDate;
            Settings.Store.ProtocolName = protocolName.Trim();
        }
    }
}
