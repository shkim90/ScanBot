using Emgu.CV.CvEnum;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ScanBot.Data;
using ScanBot.Services;
using System.Collections.Generic;

namespace ScanBot.Pages
{
    partial class Edit
    {
        [Parameter]
        public int ImageRefId { get; set; }

        StoreService storeService;
        ImageRef imageRef;
        Dictionary<string, string> imageTags;
        string imageUri;

        protected override void OnInitialized()
        {
            storeService = ScopedServices.GetService<StoreService>();

            imageRef = storeService.GetImageRef(ImageRefId);
            imageTags = imageRef.DeserializeTags();
            imageUri = $"api/image/{ImageRefId}";
        }

        private string this[string key]
        {
            get => imageTags.TryGetValue(key, out var value) ? value : null;
            set
            {
                value = value.Trim();
                if (value != "")
                {
                    imageTags[key] = value;
                }
                else
                {
                    imageTags.Remove(key);
                }
            }
        }

        private void SubmitChanges()
        {
            storeService.UpdateImageRef(imageRef, imageTags);

            NavigationManager.NavigateTo("/", true);
        }

        private void FlipImage(FlipType flip)
        {
            storeService.ProcessImage(imageRef, image => image.Flip(flip));

            NavigationManager.NavigateTo($"/edit/{ImageRefId}", true);
        }

        private void RotateImage(RotateFlags rotateCode)
        {
            storeService.ProcessImage(imageRef, image => image.Rotate(rotateCode));

            NavigationManager.NavigateTo($"/edit/{ImageRefId}", true);
        }
    }
}
