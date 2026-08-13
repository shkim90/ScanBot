namespace ScanBot.Shared
{
    partial class ScanControl
    {
        protected override void OnInitialized()
        {
            ControlService.IsReadyChanged += ControlService_IsReadyChanged;
        }

        public void Dispose()
        {
            ControlService.IsReadyChanged -= ControlService_IsReadyChanged;
        }

        private async void ControlService_IsReadyChanged()
        {
            await InvokeAsync(StateHasChanged);
        }

        private void ScanFilm()
        {
            ControlService.ScanFilm();
        }

        private void AbortFilm()
        {
            ControlService.AbortFilm();
        }

        private void EjectFilm()
        {
            ControlService.EjectFilm();
        }
    }
}
