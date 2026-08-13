using BitMiracle.LibTiff.Classic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VidarToolkit;

namespace VidarToolkitTest
{
    static class Program
    {
        static IDigitizer m_Digitizer;

        [STAThread]
        static void Main()
        {
            try
            {
                using (m_Digitizer = new Digitizer())
                {
                    Console.WriteLine($"S/N: {m_Digitizer.SerialNumber}");
                    m_Digitizer.BitsPerPixel = m_Digitizer.SupportedBitsPerPixelValues.Max();
                    m_Digitizer.Resolution = 300;
                    m_Digitizer.ScanningFilm += Digitizer_ScanningFilm;
                    while (true)
                    {
                        var dialog = new System.Windows.Forms.SaveFileDialog
                        {
                            Filter = "TIFF Files (*.tif)|*.tif"
                        };
                        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            try
                            {
                                Console.WriteLine("Scanning...");
                                if (!Console.IsInputRedirected)
                                {
                                    Console.WriteLine("Press Esc key to abort...");
                                }
                                if (ProcessFilm(dialog.FileName))
                                {
                                    Process.Start(dialog.FileName);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
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
                Console.WriteLine(ex.Message);
            }
        }

        private static void Digitizer_ScanningFilm(object sender, ScanEventArgs e)
        {
            Console.WriteLine($"Line #{e.LineCount}");

            if (!Console.IsInputRedirected && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                m_Digitizer.AbortFilm();
            }
        }

        private static bool ProcessFilm(string filePath)
        {
            using (var stream = new MemoryStream())
            {
                var scanned = m_Digitizer.ScanFilm(stream, 14, 200, out var imageWidth, out var imageHeight);
                m_Digitizer.EjectFilm();
                if (scanned)
                {
                    stream.Position = 0;
                    WriteToFile(stream, imageWidth, imageHeight, m_Digitizer.Resolution, filePath);
                }
                return scanned;
            }
        }

        private static void WriteToFile(Stream stream, int imageWidth, int imageHeight, double resolution, string filePath)
        {
            using (var file = Tiff.Open(filePath, "w"))
            {
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

                var bytesPerRow = (int)imageWidth * m_Digitizer.BytesPerPixel;
                var buffer = new byte[bytesPerRow];
                for (var i = 0; i < imageHeight; ++i)
                {
                    stream.Position = (imageHeight - 1 - i) * bytesPerRow;
                    stream.Read(buffer, 0, bytesPerRow);
                    file.WriteScanline(buffer, i);
                }
            }
        }
    }
}
