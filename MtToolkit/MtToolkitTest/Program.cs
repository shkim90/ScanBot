using BitMiracle.LibTiff.Classic;
using MtToolkit;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MtToolkitTest
{
    static class Program
    {
        static MtDigitizer m_Digitizer;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var name = GetArgValue(args, "name") ?? "NDT";
                using (m_Digitizer = new MtDigitizer(name))
                {
                    m_Digitizer.BitsPerPixel = int.TryParse(GetArgValue(args, "bitsperpixel"), out var bitsPerPixel) ? bitsPerPixel : m_Digitizer.SupportedBitsPerPixelValues.Max();
                    m_Digitizer.Resolution = int.TryParse(GetArgValue(args, "resolution"), out var resolution) ? resolution : 300;
                    m_Digitizer.FrameArea = new RectangleF(0, 0,
                        int.TryParse(GetArgValue(args, "framewidth"), out var frameWidth) ? frameWidth : 14,
                        int.TryParse(GetArgValue(args, "frameheight"), out var frameHeight) ? frameHeight : 17);
                    m_Digitizer.Density = Enum.TryParse(GetArgValue(args, "density"), out MtDensity density) ? density : MtDensity.D3_50;
                    m_Digitizer.AutoFilmFeeder = bool.TryParse(GetArgValue(args, "autofilmfeeder"), out var autoFilmFeeder) ? autoFilmFeeder : false;
                    m_Digitizer.MultiChannelCrop = bool.TryParse(GetArgValue(args, "multichannelcrop"), out var multiChannelCrop) ? multiChannelCrop : false;
                    var filePath = GetArgValue(args);
                    if (filePath != null)
                    {
                        ProcessFilm(filePath, false);
                        return;
                    }
                    while (true)
                    {
                        var dialog = new System.Windows.Forms.SaveFileDialog
                        {
                            Filter = "TIFF Files (*.tif)|*.tif"
                        };
                        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            Console.WriteLine("Scanning...");
                            ProcessFilm(dialog.FileName, true);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static string GetArgValue(string[] args, string name = null)
        {
            if (name == null)
            {
                return args.FirstOrDefault(arg2 => !arg2.StartsWith("-"));
            }

            name = "-" + name.ToLower();
            var arg = args.FirstOrDefault(arg2 => arg2.ToLower().StartsWith(name + ":"));
            return arg?.Substring(name.Length + 1);
        }

        private static void ProcessFilm(string filePath, bool show)
        {
            var i = 0;
            m_Digitizer.FilmScanned += Digitizer_FilmScanned;
            m_Digitizer.ScanFilm();
            m_Digitizer.FilmScanned -= Digitizer_FilmScanned;

            void Digitizer_FilmScanned(object sender, ImageEventArgs e)
            {
                var densityRange = m_Digitizer.DensityRange;
                if (densityRange.Length > 1)
                {
                    Console.WriteLine($"Density range: {densityRange[0]} - {densityRange[1]}");
                }

                var filePath2 = i == 0 ? filePath : Path.Combine(Path.GetDirectoryName(filePath), $"{Path.GetFileNameWithoutExtension(filePath)}_{i + 1:d5}" + Path.GetExtension(filePath));
                ++i;
                using (var stream = new MemoryStream(e.Data))
                {
                    WriteToFile(stream, e.Width, m_Digitizer.Resolution, filePath2);
                }

                if (show)
                {
                    Process.Start(filePath2);
                }
            }
        }

        private static void WriteToFile(Stream stream, int imageWidth, float resolution, string filePath)
        {
            using (var file = Tiff.Open(filePath, "w"))
            {
                var bytesPerRow = (imageWidth * m_Digitizer.BytesPerPixel + 3) / 4 * 4;
                var imageHeight = (int)(stream.Length / bytesPerRow);

                file.SetField(TiffTag.IMAGEWIDTH, imageWidth);
                file.SetField(TiffTag.IMAGELENGTH, imageHeight);
                file.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                file.SetField(TiffTag.BITSPERSAMPLE, m_Digitizer.BytesPerPixel * 8);
                file.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
                file.SetField(TiffTag.ROWSPERSTRIP, imageHeight);
                file.SetField(TiffTag.XRESOLUTION, resolution);
                file.SetField(TiffTag.YRESOLUTION, resolution);
                file.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
                file.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                file.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                file.SetField(TiffTag.COMPRESSION, Compression.NONE);
                file.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);

                RegisterCustomFields(file);
                SetCustomFields(file);

                var buffer = new byte[bytesPerRow];
                for (var i = 0; i < imageHeight; ++i)
                {
                    stream.Position = (imageHeight - 1 - i) * bytesPerRow;
                    stream.Read(buffer, 0, bytesPerRow);
                    file.WriteScanline(buffer, i);
                }
            }
        }

        const TiffTag m_GroupNameTag = (TiffTag)65000;
        const TiffTag m_RescaleSlopeTag = (TiffTag)65001;
        const TiffTag m_RescaleInterceptTag = (TiffTag)65002;

        private static void RegisterCustomFields(Tiff file)
        {
            var infos = new[]
            {
                new TiffFieldInfo(m_GroupNameTag, -1, -1, TiffType.ASCII, FieldBit.Custom, false, false, "Custom Group"),
                new TiffFieldInfo(m_RescaleSlopeTag, 1, 1, TiffType.DOUBLE, FieldBit.Custom, true, false, "Rescale Slope"),
                new TiffFieldInfo(m_RescaleInterceptTag, 1, 1, TiffType.DOUBLE, FieldBit.Custom, true, false, "Rescale Intercept"),
            };
            file.MergeFieldInfo(infos, infos.Length);
        }

        private static void SetCustomFields(Tiff file)
        {
            var densityRange = m_Digitizer.DensityRange;
            if (densityRange.Length > 1)
            {
                file.SetField(m_GroupNameTag, "IBERIS");
                var maxPixelValue = (1 << m_Digitizer.BitsPerPixel) - 1;
                file.SetField(m_RescaleSlopeTag, (densityRange[0] - densityRange[1]) * 1000 / maxPixelValue);
                file.SetField(m_RescaleInterceptTag, densityRange[1] * 1000);
            }
        }
    }
}
