using Emgu.CV;
using Emgu.CV.Structure;
using ScanBot.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    class OcrEngine : IOcrEngine
    {
        readonly Settings.OcrSettings m_Settings;

        public OcrEngine(Settings settings)
        {
            m_Settings = settings.Ocr;
        }

        public async Task<List<Label>> FindLabels(Image<Gray, byte> byteImage)
        {
            using var fileContent = new ByteArrayContent(byteImage.ToJpegData(100));
            fileContent.Headers.ContentType = new("image/jpeg");
            using var content = new MultipartFormDataContent
            {
                { fileContent, "image", "image.jpg" }
            };

            using var client = new HttpClient();
            using var response = await client.PostAsync($"http://{m_Settings.Host}/api/recognition/", content);
            var labels = (await response.Content.ReadFromJsonAsync<OcrResult>()).Regions_Of_Interest
                .Where(roi => roi.Confidence >= m_Settings.Confidence)
                .Select(roi => new Label
                {
                    Text = roi.Text,
                    Rect = roi.Rect
                })
                .ToList();
            return labels;
        }

        class OcrResult
        {
            public List<Roi> Regions_Of_Interest { get; set; }
        }

        class Roi
        {
            public List<double[]> Coordinates { get; set; }

            public string Text { get; set; }

            public double Confidence { get; set; }

            public Rectangle Rect => Rectangle.FromLTRB(
                (int)Math.Round(Coordinates.Min(point => point[0])),
                (int)Math.Round(Coordinates.Min(point => point[1])),
                (int)Math.Round(Coordinates.Max(point => point[0])),
                (int)Math.Round(Coordinates.Max(point => point[1])));
        }
    }
}
