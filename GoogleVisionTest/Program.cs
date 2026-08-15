using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Google.Cloud.Vision.V1;
using ScanBot.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GoogleVisionTest
{
    static class Program
    {
        static string m_StoreFolderPath;

        [STAThread]
        static void Main()
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CloudKey.json"));

            m_StoreFolderPath = Path.Combine(Path.GetTempPath(), nameof(GoogleVisionTest));
            Directory.CreateDirectory(m_StoreFolderPath);

            while (true)
            {
                var dialog = new System.Windows.Forms.OpenFileDialog
                {
                    Filter = "*.tif|*.tif",
                    Multiselect = true
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        foreach (var filePath in dialog.FileNames)
                        {
                            ProcessImage(filePath);
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

        private static void ProcessImage(string imageFilePath)
        {
            using var image = new Image<Gray, ushort>(imageFilePath);
            using var byteImage = ToByteImage(image);
            var visionImage = Image.FromBytes(byteImage.ToJpegData(100));

            var stopwatch = Stopwatch.StartNew();

            var client = ImageAnnotatorClient.Create();
            var labels = client.DetectText(visionImage)
                .Skip(1)
                .Select(annotation => new Label
                {
                    Text = annotation.Description,
                    Rect = GetBoundingRect(annotation.BoundingPoly)
                })
                .ToList();

            stopwatch.Stop();
            Console.WriteLine($"Processing {imageFilePath}: {stopwatch.ElapsedMilliseconds} ms");

            labels = Label.Merge(labels, 10, 10, _ => false);
            ShowLabels(imageFilePath, labels);
        }

        private static System.Drawing.Rectangle GetBoundingRect(BoundingPoly polygon) => System.Drawing.Rectangle.FromLTRB(
            polygon.Vertices.Min(vertex => vertex.X),
            polygon.Vertices.Min(vertex => vertex.Y),
            polygon.Vertices.Max(vertex => vertex.X),
            polygon.Vertices.Max(vertex => vertex.Y));

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
                image.Draw(label.Rect, new(0, 255, 0), 4);
                var labelLocation = label.Rect.Location;
                labelLocation.Y += label.Rect.Height / 2;
                image.Draw(label.Text, labelLocation, FontFace.HersheyPlain, 3, new(0, 0, 255), 3);
            }
        }
    }
}
