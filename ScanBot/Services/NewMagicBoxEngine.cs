using Emgu.CV;
using Emgu.CV.Structure;
using ScanBot.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    class NewMagicBoxEngine : IOcrEngine
    {
        readonly Settings.OcrSettings m_Settings;

        public NewMagicBoxEngine(Settings settings)
        {
            m_Settings = settings.Ocr;
        }

        public async Task<List<Label>> FindLabels(Image<Gray, byte> byteImage)
        {
            using var fileContent = new ByteArrayContent(byteImage.ToJpegData(100));
            fileContent.Headers.ContentType = new("image/jpeg");
            using var content = new MultipartFormDataContent
            {
                { fileContent, "file", "image.jpg" }
            };

            using var client = new HttpClient();
            for (var stopwatch = Stopwatch.StartNew(); stopwatch.Elapsed.TotalMinutes < 1; )
            {
                try
                {
                    using var response = await client.PostAsync($"http://{m_Settings.Host}/ocr/upload", content);
                    var labels = (await response.Content.ReadFromJsonAsync<ResponseData>()).Message
                        .Where(textBox => textBox.GetConfidence() >= m_Settings.Confidence * 100)
                        .Select(textBox => new Label
                        {
                            Text = textBox.Label,
                            Rect = textBox.GetRect(byteImage.Width, byteImage.Height)
                        })
                        .ToList();
                    return labels;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred");
                }
            }
            throw new TimeoutException("Cannot connect to MagicBox");
        }

        class ResponseData
        {
            public List<TextBox> Message { get; set; }
        }

        class TextBox
        {
            public string Confidence { get; set; }

            public double GetConfidence() => double.Parse(Confidence.TrimEnd('%'));

            public string Label { get; set; }

            public double X { get; set; }

            public double Y { get; set; }

            public double W { get; set; }

            public double H { get; set; }

            public Rectangle GetRect(int width, int height) => new(
                (int)Math.Round(X * width),
                (int)Math.Round(Y * height),
                (int)Math.Round(W * width),
                (int)Math.Round(H * height));
        }
    }
}
