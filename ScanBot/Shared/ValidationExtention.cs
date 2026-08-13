using Blazorise;

namespace ScanBot.Shared
{
    static class ValidationExtention
    {
        public static void IsTrimmedNotEmpty(ValidatorEventArgs e) => e.Status = ((string)e.Value)?.Trim()?.Length > 0 ? ValidationStatus.Success : ValidationStatus.Error;
    }
}
