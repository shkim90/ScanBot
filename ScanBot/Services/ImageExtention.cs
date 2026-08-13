using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ScanBot.Services
{
    static class ImageExtention
    {
        public static Image<Gray, ushort> Rotate(this Image<Gray, ushort> image, RotateFlags rotateCode)
        {
            var image2 = rotateCode switch
            {
                RotateFlags.Rotate180 => new Image<Gray, ushort>(image.Width, image.Height),
                _ => new Image<Gray, ushort>(image.Height, image.Width)
            };
            CvInvoke.Rotate(image, image2, rotateCode);
            return image2;
        }

        public static Image<Gray, byte> ToByteImage(this Image<Gray, ushort> image)
        {
            image.MinMax(out var _, out var maxValues, out var _, out var _);
            var slope = 255 / maxValues[0];
            return image.Convert(value => (byte)(value * slope));
        }

        public static IEnumerable<Image<Gray, ushort>> SplitFilmIntoStrips(this Image<Gray, ushort> image, ushort gapThreshold, int minWidth)
        {
            var profile = BuildMinProfile(image, 10);

            var i = 0;
            while (true)
            {
                i = profile.FindIndex(i, value => value < gapThreshold);
                if (i == -1)
                {
                    break;
                }
                var j = profile.FindIndex(i + 1, value => value >= gapThreshold);
                if (j == -1)
                {
                    break;
                }

                var width = j - i;
                if (width >= minWidth)
                {
                    yield return image.Copy(new Rectangle(i, 0, width, image.Height));
                }

                i = j;
            }

            if (i >= 0)
            {
                var width = profile.Count - i;
                if (width >= minWidth)
                {
                    yield return image.Copy(new Rectangle(i, 0, width, image.Height));
                }
            }
        }

        private static List<ushort> BuildMinProfile(Image<Gray, ushort> image, int verticalMargin)
        {
            var width = image.Width;
            var profile = Enumerable.Repeat(ushort.MaxValue, width).ToArray();
            var length = width * (image.Height - verticalMargin * 2);
            var offset = width * verticalMargin;
            unsafe
            {
                fixed (ushort* pData = image.Data, pProfile = profile)
                {
                    for (var i = offset; i < length; ++i)
                    {
                        var j = i % width;
                        pProfile[j] = Math.Min(pData[i], pProfile[j]);
                    }
                }
            }
            return profile.ToList();
        }
    }
}
