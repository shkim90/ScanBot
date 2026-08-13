using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using ScanBot.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;

namespace MagicBoxTest
{
    static class Program
    {
        static string m_StoreFolderPath;
        static bool m_CleanLabels;

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                System.Windows.Forms.MessageBox.Show($"Usage: {nameof(MagicBoxTest)} <Host>");
                return;
            }

            m_StoreFolderPath = Path.Combine(Path.GetTempPath(), nameof(MagicBoxTest));
            Directory.CreateDirectory(m_StoreFolderPath);
            m_CleanLabels = args.Length > 1 && args[1] == "-clean";

            while (true)
            {
                var dialog = new System.Windows.Forms.OpenFileDialog
                {
                    Filter = "*.tif|*.tif|*.jpg|*.jpg",
                    Multiselect = true
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        foreach (var filePath in dialog.FileNames)
                        {
                            ProcessImage(args[0], filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Forms.MessageBox.Show(ex.Message);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private static void ProcessImage(string host, string imageFilePath)
        {
            using var image = new Image<Gray, ushort>(imageFilePath);
            using var byteImage = ToByteImage(image);

            var filePath = Path.Combine(Path.GetTempPath(), nameof(MagicBoxTest) + ".jpg");
            byteImage.Save(filePath);
            using var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
            fileContent.Headers.ContentType = new("image/jpeg");
            using var content = new MultipartFormDataContent
            {
                { fileContent, "file", Path.GetFileName(filePath) }
            };

            var stopwatch = Stopwatch.StartNew();

            using var client = new HttpClient();
            using var response = client.PostAsync($"http://{host}/ocr/upload", content).Result;
            var labels = response.Content.ReadFromJsonAsync<ResponseData>().Result.Message
                .Select(textBox => new Label
                {
                    Text = textBox.Label,
                    Rect = textBox.GetRect(image.Width, image.Height)
                })
                .ToList();

            stopwatch.Stop();
            Console.WriteLine($"Processing {imageFilePath}: {stopwatch.ElapsedMilliseconds} ms");

            labels = Label.Merge(labels, 30);
            if (m_CleanLabels)
            {
                CleanLabels(image, imageFilePath, labels);
            }
            else
            {
                labels.Add(new() { Text = Path.GetFileName(imageFilePath), Rect = new(0, 100, 0, 0) });
                ShowLabels(filePath, labels);
            }

            foreach (var label in labels)
            {
                Console.WriteLine($"{label.Text}: {label.Rect}");
            }
        }

        private static Image<Gray, byte> ToByteImage(Image<Gray, ushort> image)
        {
            image.MinMax(out var _, out var maxValues, out var _, out var _);
            var slope = 255 / maxValues[0];
            return image.Convert(value => (byte)(value * slope));
        }

        private static void ShowLabels(string imageFilePath, List<Label> labels)
        {
            using var image = new Image<Bgr, byte>(imageFilePath);
            DrawLabels(image, labels);
            var outputImageFilePath = Path.Combine(m_StoreFolderPath, Guid.NewGuid() + ".png");
            image.Save(outputImageFilePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = outputImageFilePath,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private static void DrawLabels(Image<Bgr, byte> image, List<Label> labels)
        {
            foreach (var label in labels)
            {
                image.Draw(label.Rect, new(0, 0, 255), 4);
                var labelLocation = label.Rect.Location;
                image.Draw(label.Text, labelLocation, FontFace.HersheyPlain, 8, new(0, 255, 0), 3);
            }
        }

        private static void CleanLabels<T>(Image<Gray, T> image, string imageFilePath, List<Label> labels)
            where T : new()
        {
            CleanLabels(image, labels);
            var outputImageFilePath = Path.Combine(Path.GetDirectoryName(imageFilePath), "Cleaned", Path.GetFileName(imageFilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(outputImageFilePath));
            image.Save(outputImageFilePath);
        }

        private static void CleanLabels<T>(Image<Gray, T> image, List<Label> labels)
            where T : new()
        {
            foreach (var label in labels)
            {
                image.FillConvexPoly(GetBoundingPolyline(label.Rect), new(0));
            }
        }

        private static Point[] GetBoundingPolyline(Rectangle rect) => new[]
        {
            new Point(rect.Left, rect.Top),
            new Point(rect.Right, rect.Top),
            new Point(rect.Right, rect.Bottom),
            new Point(rect.Left, rect.Bottom)
        };

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
