using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    abstract class ScanServiceBase
    {
        public const string ModelNameKey = "ModelName";
        public const string SerialNumberKey = "SerialNumber";
        public const string BitsPerPixelKey = "BitsPerPixel";
        public const string MinDensityKey = "MinDensity";
        public const string MaxDensityKey = "MaxDensity";

        protected async Task HandleImage(Image<Gray, ushort> image, ushort resolution, Guid scanId, bool rotate, bool imported = false)
        {
            if (rotate)
            {
                using var rotatedImage = RotateImage(image);
                await InvokeFilmScanned(new() { Image = rotatedImage, Resolution = resolution, ScanId = scanId, Imported = imported });
            }
            else
            {
                await InvokeFilmScanned(new() { Image = image, Resolution = resolution, ScanId = scanId, Imported = imported });
            }
        }

        protected static Image<Gray, ushort> CreateImage(byte[] buffer, int width, int height, int stride)
        {
            unsafe
            {
                fixed (byte* data = buffer)
                {
                    using var image = new Image<Gray, ushort>(width, height, stride, (IntPtr)data);
                    return image.Flip(FlipType.Vertical);
                }
            }
        }

        private static Image<Gray, ushort> RotateImage(Image<Gray, ushort> image)
        {
            using var rotatedImage = new Image<Gray, ushort>(image.Height, image.Width);
            CvInvoke.Transpose(image, rotatedImage);
            return rotatedImage.Flip(FlipType.Horizontal);
        }

        public async Task ImportImageFile(byte[] data, ushort resolution, bool rotate)
        {
            try
            {
                using var mat = new Mat();
                CvInvoke.Imdecode(data, ImreadModes.Grayscale | ImreadModes.AnyDepth, mat);
                using var image = mat.ToImage<Gray, ushort>();
                await HandleImage(image, resolution, Guid.NewGuid(), rotate, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
            }
        }

        private async Task InvokeFilmScanned(ScanEventArgs e)
        {
            if (FilmScanned != null)
            {
                foreach (var func in FilmScanned.GetInvocationList().Cast<Func<ScanEventArgs, Task>>())
                {
                    await func(e);
                }
            }
        }

        public event Func<ScanEventArgs, Task> FilmScanned;
    }
}
