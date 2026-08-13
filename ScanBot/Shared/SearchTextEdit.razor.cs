using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace ScanBot.Shared
{
    partial class SearchTextEdit
    {
        [Parameter]
        public string Text { get; set; }

        [Parameter]
        public EventCallback<string> TextChanged { get; set; }

        private async Task ChangeText(string value)
        {
            Text = value;
            await TextChanged.InvokeAsync(value);
        }
    }
}
