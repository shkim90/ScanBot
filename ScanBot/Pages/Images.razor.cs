using Blazorise;
using Blazorise.DataGrid;
using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ScanBot.Data;
using ScanBot.Models;
using ScanBot.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ScanBot.Pages
{
    partial class Images
    {
        StoreService storeService;
        DateTime startDate;
        DateTime endDate;
        string keywordFilter;
        int pageSize;
        int currentPage;
        List<ImageRefEx> imageRefs;
        List<ImageRefEx> selectedImageRefs = new();
        FileEdit fileEdit;

        protected override async Task OnInitializedAsync()
        {
            storeService = ScopedServices.GetService<StoreService>();

            startDate = await SessionStorage.GetItemAsync<DateTime?>(nameof(startDate)) ?? DateTime.Today;
            endDate = await SessionStorage.GetItemAsync<DateTime?>(nameof(endDate)) ?? DateTime.Today;
            keywordFilter = await SessionStorage.GetItemAsStringAsync(nameof(keywordFilter));
            pageSize = await LocalStorage.GetItemAsync<int?>(nameof(pageSize)) ?? 10;
            currentPage = await SessionStorage.GetItemAsync<int?>(nameof(currentPage)) ?? 1;
            await UpdateImageRefs();
            ScanService.FilmScanned += ScanService_FilmScanned;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ScanService.FilmScanned -= ScanService_FilmScanned;
            }
        }

        private async Task UpdateImageRefs()
        {
            var imageRefs2 = (await storeService.GetImageRefs(startDate, endDate))
                .Select(imageRef => new ImageRefEx(imageRef, !File.Exists(storeService.GetImagePath(imageRef))));
            if (!string.IsNullOrWhiteSpace(keywordFilter))
            {
                foreach (var keyword in keywordFilter.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    imageRefs2 = imageRefs2.Where(imageRef => imageRef.Tags.Values.Any(tagValue => tagValue.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
                }
            }
            imageRefs = imageRefs2.ToList();
            selectedImageRefs.Clear();
        }

        private async Task ScanService_FilmScanned(ScanEventArgs e)
        {
            await UpdateImageRefs();
            await InvokeAsync(StateHasChanged);
        }

        private async Task ChangeStartDate(DateTime value)
        {
            startDate = value;
            await SessionStorage.SetItemAsync(nameof(startDate), startDate);
            await UpdateImageRefs();
        }

        private async Task ChangeEndDate(DateTime value)
        {
            endDate = value;
            await SessionStorage.SetItemAsync(nameof(endDate), endDate);
            await UpdateImageRefs();
        }

        private async Task ChangeKeywordFilter(string value)
        {
            keywordFilter = value;
            await SessionStorage.SetItemAsStringAsync(nameof(keywordFilter), keywordFilter);
            await UpdateImageRefs();
        }

        private async Task ChangePageSize(int value)
        {
            pageSize = value;
            await LocalStorage.SetItemAsync(nameof(pageSize), pageSize);
        }

        private async Task ChangePage(DataGridPageChangedEventArgs e)
        {
            currentPage = e.Page;
            await SessionStorage.SetItemAsync(nameof(currentPage), currentPage);
        }

        private void ExportImageRefs()
        {
            var uri = "api/image/ref";
            uri = QueryHelpers.AddQueryString(uri, "startDate", startDate.ToString(CultureInfo.InvariantCulture));
            uri = QueryHelpers.AddQueryString(uri, "endDate", endDate.ToString(CultureInfo.InvariantCulture));
            NavigationManager.NavigateTo(uri, true);
        }

        private async Task ImportImageFiles(FileChangedEventArgs e)
        {
            foreach (var file in e.Files)
            {
                await ImportImageFile(file);
            }
        }

        private async Task ImportImageFile(IFileEntry file)
        {
            try
            {
                using var stream = new MemoryStream();
                await file.WriteToStreamAsync(stream);
                var data = stream.ToArray();
                await ScanService.ImportImageFile(data, StoreService.GetImageResolution(data));
            }
            catch
            {
            }
        }

        private void EditImage()
        {
            var imageRef = selectedImageRefs[0].Source;
            NavigationManager.NavigateTo($"edit/{imageRef.Id}", true);
        }

        private void DownloadImages()
        {
            var uri = "api/image/zip";
            var imageRefsToDownload = selectedImageRefs.Where(imageRef => !imageRef.Sent).ToList();
            uri = QueryHelpers.AddQueryString(uri, "id", string.Join(',', imageRefsToDownload.Select(imageRef => imageRef.Source.Id.ToString())));
            NavigationManager.NavigateTo(uri, true);
        }

        private async Task RecognizeImages()
        {
            if (!await MessageService.Confirm("Recognize images?", "Images"))
            {
                return;
            }

            await LoadingIndicatorService.Show();
            await ResetImageProgress();

            var i = 0;
            var imageRefsToRecognize = selectedImageRefs.Where(imageRef => !imageRef.Sent).ToList();
            foreach (var imageRef in imageRefsToRecognize)
            {
                await RecognizeImage(imageRef.Source);
                await SetImageProgress(i++, imageRefsToRecognize.Count);
            }

            await LoadingIndicatorService.Hide();
            await UpdateImageRefs();
        }

        private async Task RecognizeImage(ImageRef imageRef)
        {
            var imagePath = storeService.GetImagePath(imageRef);
            using var image = new Image<Gray, ushort>(imagePath);
            var tags = imageRef.DeserializeTags();
            var resolution = StoreService.GetImageResolution(imagePath);
            var (ocrTags, imageModified) = await OcrService.FindTags(image, resolution);
            foreach (var ocrTag in ocrTags)
            {
                tags[ocrTag.Key] = ocrTag.Value;
            }
            storeService.UpdateImageRef(imageRef, tags);

            if (imageModified)
            {
                image.Save(imagePath);
                StoreService.SetImageResolution(imagePath, resolution);
            }
        }

        private async Task SendImages()
        {
            if (!await MessageService.Confirm("Send images?", "Images"))
            {
                return;
            }

            await LoadingIndicatorService.Show();
            await ResetImageProgress();

            var i = 0;
            var imageRefsToSend = selectedImageRefs.Where(imageRef => !imageRef.Sent).ToList();
            foreach (var imageRef in imageRefsToSend)
            {
                if (await SendImage(imageRef.Source))
                {
                    await SetImageProgress(i++, imageRefsToSend.Count);
                }
                else
                {
                    break;
                }
            }

            await LoadingIndicatorService.Hide();
            await UpdateImageRefs();
        }

        private async Task<bool> SendImage(ImageRef imageRef)
        {
            var imagePath = storeService.GetImagePath(imageRef);
            var tags = imageRef.DeserializeTags();
            var filmTypeTemplate = ImageTemplate.Default.FilmTypes.FirstOrDefault(filmTypeTemplate => filmTypeTemplate.ContainsTags(tags));
            if (filmTypeTemplate != null)
            {
                using var image = new Image<Gray, ushort>(imagePath);
                var dicomFile = DicomService.CreateDicomFile(image, StoreService.GetImageResolution(imagePath), imageRef.Timestamp, tags, filmTypeTemplate);
                if (await storeService.SendFile(dicomFile))
                {
                    storeService.PurgeData(imagePath);
                    return true;
                }
                else
                {
                    await NotificationService.Warning("Image cannot send");
                }
            }
            else
            {
                await NotificationService.Warning("Film type not recognized");
            }
            return false;
        }

        private async Task DeleteImages()
        {
            if (!await MessageService.Confirm("Delete images?", "Images"))
            {
                return;
            }

            foreach (var imageRef in selectedImageRefs)
            {
                storeService.DeleteImage(imageRef.Source);
            }
            await UpdateImageRefs();
        }

        private async Task SetImageProgress(int imageIndex, int imageCount) => await PageProgressService.Go((imageIndex + 1) * 100 / imageCount, options => options.Color = Color.Warning);

        private async Task ResetImageProgress() => await PageProgressService.Go(-1);
    }
}
