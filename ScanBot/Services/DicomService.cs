using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
using Dicom.Log;
using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ScanBot.Services
{
    class DicomService
    {
        public DicomService()
        {
            LogManager.SetImplementation(new SerilogManager());
        }

        public DicomFile CreateDicomFile(Image<Gray, ushort> image, ushort resolution, DateTime timestamp, Dictionary<string, string> tags, ImageTemplate.FilmTypeTemplate filmTypeTemplate)
        {
            var studyTags = ImageTemplate.Default.GetStudyTags(tags);
            var studyUid = GenerateUid(filmTypeTemplate.Id, string.Join(null, filmTypeTemplate.GetTagValues(studyTags)));
            var dataset = CreateDicomDataset(studyUid);
            AddTags(timestamp, tags, dataset);
            AddPixelData(image, resolution, dataset);
            return new(dataset);
        }

        private static DicomDataset CreateDicomDataset(string studyUid)
        {
            var dataset = new DicomDataset();
            dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyUid);
            var imageUid = DicomUID.Generate().UID;
            dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, imageUid);
            dataset.AddOrUpdate(DicomTag.SOPInstanceUID, imageUid);

            dataset.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage);
            dataset.AddOrUpdate(DicomTag.Modality, "SC");
            dataset.AddOrUpdate(DicomTag.ConversionType, "DF");
            return dataset;
        }

        private static string GenerateUid(int id, string text)
        {
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var hashNumber = new BigInteger(hash);
            hashNumber = BigInteger.Abs(hashNumber);
            var uid = $"1.2.724.33963612.{id}.{hashNumber}";
            if (uid.Length > 64)
            {
                uid = uid.Remove(64);
            }
            return uid;
        }

        private static void AddTags(DateTime timestamp, Dictionary<string, string> tags, DicomDataset dataset)
        {
            foreach (var tag in tags.OrderBy(tag => ImageTemplate.Default.Tags.FindIndex(tagTemplate => tagTemplate.Key == tag.Key)))
            {
                var tagTemplate = ImageTemplate.Default.Tags.SingleOrDefault(tagTemplate => tagTemplate.Key == tag.Key);
                var dicomTagValue = tagTemplate?.GetDicomTagValue();
                if (dicomTagValue != null)
                {
                    var date = tagTemplate.ConvertToDate(tag.Value);
                    if (date != null)
                    {
                        dataset.AddOrUpdate(dicomTagValue, date.Value);
                    }
                    else
                    {
                        var value = tag.Value;
                        if (dataset.Contains(dicomTagValue))
                        {
                            value = dataset.GetSingleValue<string>(dicomTagValue) + "_" + value;
                        }
                        try
                        {
                            dataset.AddOrUpdate(dicomTagValue, value);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            if (tags.TryGetValue(StoreService.ScanDateKey, out var scanDateString) &&
                DateTime.TryParseExact(scanDateString, StoreService.ScanDateFormat, null, DateTimeStyles.None, out var scanDate))
            {
                dataset.AddOrUpdate(DicomTag.StudyDate, scanDate);
                dataset.AddOrUpdate(DicomTag.StudyTime, timestamp);
            }
            if (dataset.Contains(DicomTag.SeriesDescription))
            {
                var seriesDescription = dataset.GetSingleValue<string>(DicomTag.SeriesDescription);
                var match = Regex.Match(seriesDescription, @"\d+");
                if (match.Success)
                {
                    dataset.AddOrUpdate(DicomTag.SeriesNumber, int.Parse(match.Value));
                }
            }

            if (tags.TryGetValue(ScanServiceBase.ModelNameKey, out var modelName))
            {
                dataset.AddOrUpdate(DicomTag.ManufacturerModelName, modelName);
            }
            if (tags.TryGetValue(ScanServiceBase.SerialNumberKey, out var serialNumber))
            {
                dataset.AddOrUpdate(DicomTag.DeviceSerialNumber, serialNumber);
            }
            if (tags.TryGetValue(ScanServiceBase.MinDensityKey, out var minDensityString) &&
                float.TryParse(minDensityString, NumberStyles.Float, CultureInfo.InvariantCulture, out var minDensity) &&
                tags.TryGetValue(ScanServiceBase.MaxDensityKey, out var maxDensityString) &&
                float.TryParse(maxDensityString, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxDensity))
            {
                dataset.AddOrUpdate(DicomTag.RescaleType, "OD");
                var bitsPerPixel = tags.TryGetValue(ScanServiceBase.BitsPerPixelKey, out var bitsPerPixelString) ? int.Parse(bitsPerPixelString) : 16;
                dataset.AddOrUpdate(DicomTag.RescaleSlope, (minDensity - maxDensity) * 1000 / ((1 << bitsPerPixel) - 1));
                dataset.AddOrUpdate(DicomTag.RescaleIntercept, maxDensity * 1000);
            }
        }

        private static void AddPixelData(Image<Gray, ushort> image, ushort resolution, DicomDataset dataset)
        {
            dataset.AddOrUpdate(DicomTag.BitsAllocated, (ushort)16);
            image.MinMax(out var minValues, out var maxValues, out var _, out var _);
            var low = minValues[0];
            var high = maxValues[0];
            var rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            var rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);
            low = low * rescaleSlope + rescaleIntercept;
            high = high * rescaleSlope + rescaleIntercept;
            dataset.AddOrUpdate(DicomTag.WindowWidth, Math.Abs(high - low));
            dataset.AddOrUpdate(DicomTag.WindowCenter, (high + low) / 2);
            var pixelSpacing = Math.Round(25.4 / resolution, 5);
            dataset.AddOrUpdate(DicomTag.PixelSpacing, pixelSpacing, pixelSpacing);

            var pixelData = DicomPixelData.Create(dataset, true);
            pixelData.SamplesPerPixel = 1;
            pixelData.PixelRepresentation = PixelRepresentation.Unsigned;
            pixelData.BitsStored = pixelData.BitsAllocated;
            pixelData.HighBit = (ushort)(pixelData.BitsStored - 1);
            pixelData.Width = (ushort)image.Width;
            pixelData.Height = (ushort)image.Height;
            pixelData.PhotometricInterpretation = rescaleSlope < 0 ? PhotometricInterpretation.Monochrome1 : PhotometricInterpretation.Monochrome2;

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            foreach (var pixelValue in image.Data)
            {
                writer.Write(pixelValue);
            }
            pixelData.AddFrame(new MemoryByteBuffer(stream.ToArray()));
        }
    }
}
