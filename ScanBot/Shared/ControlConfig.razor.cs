namespace ScanBot.Shared
{
    partial class ControlConfig
    {
        string serialPort;

        protected override void OnInitialized()
        {
            serialPort = Settings.Control.SerialPort;
        }

        public void UpdateSettings()
        {
            Settings.Control.SerialPort = serialPort;
        }
    }
}
