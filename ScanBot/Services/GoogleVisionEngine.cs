using Emgu.CV;
using Emgu.CV.Structure;
using Google.Cloud.Vision.V1;
using ScanBot.Data;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Image = Google.Cloud.Vision.V1.Image;

namespace ScanBot.Services
{
    class GoogleVisionEngine : IOcrEngine
    {
        public async Task<List<Label>> FindLabels(Image<Gray, byte> byteImage)
        {
            var visionImage = Image.FromBytes(byteImage.ToJpegData(100));
            var client = ImageAnnotatorClient.Create();
            var labels = (await client.DetectTextAsync(visionImage))
                .Skip(1)
                .Select(annotation => new Label
                {
                    Text = annotation.Description,
                    Rect = GetBoundingRect(annotation.BoundingPoly)
                })
                .ToList();
            return labels;
        }

        private static Rectangle GetBoundingRect(BoundingPoly polygon) => Rectangle.FromLTRB(
            polygon.Vertices.Min(vertex => vertex.X),
            polygon.Vertices.Min(vertex => vertex.Y),
            polygon.Vertices.Max(vertex => vertex.X),
            polygon.Vertices.Max(vertex => vertex.Y));
    }
}
