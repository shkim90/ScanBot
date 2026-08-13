using Emgu.CV;
using Emgu.CV.Structure;
using Ionic.Zip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ScanBot.Data;
using ScanBot.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ScanBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        [HttpGet]
        [Route("{id}")]
        public ActionResult GetImage(int id)
        {
            var storeService = HttpContext.RequestServices.GetService<StoreService>();
            var imageRef = storeService.GetImageRef(id);
            if (imageRef != null)
            {
                var imagePath = storeService.GetImagePath(imageRef);
                using var image = new Image<Gray, ushort>(imagePath);
                using var byteImage = image.ToByteImage();
                return File(byteImage.ToJpegData(), "image/jpeg");
            }

            return NotFound();
        }

        [HttpGet]
        [Route("zip")]
        public async Task<ActionResult> GetZipFile(string id)
        {
            var storeService = HttpContext.RequestServices.GetService<StoreService>();
            var ids = id.Split(',').Select(id => int.Parse(id)).ToList();
            var imageRefs = await storeService.GetImageRefs(ids);

            var stream = new MemoryStream();
            CreateZipFile(imageRefs.Select(imageRef => storeService.GetImagePath(imageRef)).ToList(), stream);
            stream.Position = 0;
            return File(stream, "application/zip", "images.zip");
        }

        private static void CreateZipFile(List<string> imagePaths, Stream stream)
        {
            using var zipFile = new ZipFile();
            zipFile.AddFiles(imagePaths, "");
            zipFile.Save(stream);
        }

        [HttpGet]
        [Route("ref")]
        public async Task<ActionResult> GetImageRefs(DateTime startDate, DateTime endDate)
        {
            var stream = new MemoryStream();
            await WriteImageRefs(startDate, endDate, stream);
            stream.Position = 0;
            return File(stream, "text/csv", $"{nameof(ScanBot)}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv");
        }

        private async Task WriteImageRefs(DateTime startDate, DateTime endDate, Stream stream)
        {
            var writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };
            writer.WriteLine(string.Join(',', FieldNames));
            var storeService = HttpContext.RequestServices.GetService<StoreService>();
            foreach (var imageRef in await storeService.GetImageRefs(startDate, endDate))
            {
                writer.WriteLine(string.Join(',', GetFieldValues(imageRef).Select(value => ExportFieldValue(value))));
            }
        }

        private static string ExportFieldValue(string value) => value != null && (value.Contains(',') || value.Contains('"')) ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

        private static IEnumerable<string> FieldNames
        {
            get
            {
                yield return "Date";
                yield return "Time";

                foreach (var tagTemplate in ImageTemplate.Default.Tags)
                {
                    yield return tagTemplate.Key;
                }
            }
        }

        private static IEnumerable<string> GetFieldValues(ImageRef imageRef)
        {
            yield return imageRef.Timestamp.ToString("d", CultureInfo.InvariantCulture);
            yield return imageRef.Timestamp.ToString("t", CultureInfo.InvariantCulture);

            var tags = imageRef.DeserializeTags();
            foreach (var tagTemplate in ImageTemplate.Default.Tags)
            {
                yield return tags.TryGetValue(tagTemplate.Key, out var value) ? value : null;
            }
        }
    }
}
