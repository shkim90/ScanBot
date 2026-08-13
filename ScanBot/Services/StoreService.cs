using BitMiracle.LibTiff.Classic;
using Dicom;
using DicomScu;
using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.EntityFrameworkCore;
using ScanBot.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    class StoreService
    {
        readonly Settings.StoreSettings m_Settings;
        readonly string m_RootFolderPath;
        readonly AppDbContext m_DbContext;

        public StoreService(Settings settings, AppDbContext dbContext)
        {
            m_Settings = settings.Store;
            m_RootFolderPath = m_Settings.RootFolderPath;
            if (!Directory.Exists(m_RootFolderPath))
            {
                m_RootFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Store");
                Directory.CreateDirectory(m_RootFolderPath);
            }
            Log.Information("Image data will store in {FolderPath}", m_RootFolderPath);

            m_DbContext = dbContext;
            m_DbContext.DatabaseFilePath = Path.Combine(m_RootFolderPath, nameof(ScanBot) + ".db");
            if (m_DbContext.Database.EnsureCreated())
            {
                Log.Information("Database {FilePath} created", m_DbContext.DatabaseFilePath);
            }
        }

        public const string ScanDateKey = "ScanDate";
        public const string ScanDateFormat = "yyyyMMdd";

        public void AddScanDate(Dictionary<string, string> tags)
        {
            var scanDate = m_Settings.ScanDate ?? DateTime.Today;
            tags[ScanDateKey] = scanDate.ToString(ScanDateFormat);
        }

        public string SaveImage(Image<Gray, ushort> image, ushort resolution, Guid scanId)
        {
            var folderPath = Path.Combine(m_RootFolderPath, scanId.ToString());
            Directory.CreateDirectory(folderPath);
            var imageFilePath = Path.Combine(folderPath, Guid.NewGuid() + ".tif");
            image.Save(imageFilePath);
            SetImageResolution(imageFilePath, resolution);
            Log.Information("File {FilePath} saved", imageFilePath);
            return imageFilePath;
        }

        public static void SetImageResolution(string imageFilePath, ushort resolution)
        {
            using var file = Tiff.Open(imageFilePath, "ah");
            file.SetDirectory(0);
            file.SetField(TiffTag.XRESOLUTION, resolution);
            file.SetField(TiffTag.YRESOLUTION, resolution);
            file.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
            file.WriteDirectory();
        }

        public static ushort GetImageResolution(string imageFilePath)
        {
            using var file = Tiff.Open(imageFilePath, "r");
            return file.GetField(TiffTag.XRESOLUTION)[0].ToUShort();
        }

        public static ushort GetImageResolution(byte[] imageData)
        {
            using var stream = new MemoryStream(imageData);
            using var file = Tiff.ClientOpen("", "r", stream, new());
            return file.GetField(TiffTag.XRESOLUTION)[0].ToUShort();
        }

        public void ProcessImage(ImageRef imageRef, Func<Image<Gray, ushort>, Image<Gray, ushort>> processFunc)
        {
            var imageFilePath = GetImagePath(imageRef);
            var resolution = GetImageResolution(imageFilePath);

            using var image = new Image<Gray, ushort>(imageFilePath);
            using var processedImage = processFunc(image);
            processedImage.Save(imageFilePath);

            SetImageResolution(imageFilePath, resolution);
        }

        public void DeleteImage(ImageRef imageRef)
        {
            m_DbContext.ImageRefs.Remove(imageRef);
            m_DbContext.SaveChanges();

            PurgeData(Path.Combine(m_RootFolderPath, imageRef.FolderName, imageRef.FileName));
        }

        public void PurgeData(string imageFilePath)
        {
            var folderPath = Path.GetDirectoryName(imageFilePath);

            try
            {
                File.Delete(imageFilePath);
                Log.Information("File {FilePath} deleted", imageFilePath);
            }
            catch
            {
            }

            if (Directory.Exists(folderPath) && Directory.GetFiles(folderPath).Length == 0)
            {
                try
                {
                    Directory.Delete(folderPath);
                    Log.Information("Folder {FolderPath} deleted", folderPath);
                }
                catch
                {
                }
            }
        }

        public ImageRef CreateImageRef(string imageFilePath, Dictionary<string, string> tags)
        {
            var folderPath = Path.GetDirectoryName(imageFilePath);
            var imageRef = new ImageRef
            {
                FileName = Path.GetFileName(imageFilePath),
                FolderName = Path.GetFileName(folderPath)
            };
            m_DbContext.ImageRefs.Add(imageRef);

            imageRef.SerializeTags(tags);
            m_DbContext.SaveChanges();
            return imageRef;
        }

        public void UpdateImageRef(ImageRef imageRef, Dictionary<string, string> tags)
        {
            imageRef.SerializeTags(tags);
            m_DbContext.SaveChanges();
        }

        public List<ImageRef> GetLastScanImageRefs()
        {
            var lastImageRef = m_DbContext.ImageRefs
                .OrderByDescending(imageRef => imageRef.Timestamp)
                .FirstOrDefault();
            if (lastImageRef == null)
            {
                return null;
            }
            var lastScanImageRefs = m_DbContext.ImageRefs
                .Where(imageRef => imageRef.FolderName == lastImageRef.FolderName)
                .OrderBy(imageRef => imageRef.Timestamp);
            return lastScanImageRefs.ToList();
        }

        public ImageRef GetImageRef(int id) => m_DbContext.ImageRefs
            .SingleOrDefault(imageRef => imageRef.Id == id);

        public async Task<List<ImageRef>> GetImageRefs(List<int> ids) => await m_DbContext.ImageRefs
            .Where(imageRef => ids.Contains(imageRef.Id)).ToListAsync();

        public string GetImagePath(ImageRef imageRef) => Path.Combine(m_RootFolderPath, imageRef.FolderName, imageRef.FileName);

        public async Task<List<ImageRef>> GetImageRefs(DateTime startDate, DateTime endDate)
        {
            var imageRefs = m_DbContext.ImageRefs
                .Where(imageRef => imageRef.Timestamp >= startDate && imageRef.Timestamp < endDate.AddDays(1))
                .OrderByDescending(imageRef => imageRef.Timestamp);
            return await imageRefs.ToListAsync();
        }

        public bool IsExportEnabled => Directory.Exists(m_Settings.ExportFolderPath);

        public string ExportFile(Image<Gray, ushort> image)
        {
            var folderPath = Path.Combine(m_Settings.ExportFolderPath, DateTime.Today.ToString(m_Settings.ExportPathPattern));
            Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, $"{GetFileCount(folderPath) + 1:d4}.jpg");
            using var byteImage = image.ToByteImage();
            byteImage.Save(filePath);
            Log.Information("File exported to {FilePath}", filePath);
            IncrementFileCount(folderPath);
            return filePath;
        }

        static readonly char[] m_InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private static string ReplaceInvalidFileNameChars(string fileName) => new(fileName.Select(c => m_InvalidFileNameChars.Contains(c) ? '_' : c).ToArray());

        readonly Dictionary<string, int> m_FileCounts = new();

        private int GetFileCount(string folderPath)
        {
            if (!m_FileCounts.TryGetValue(folderPath, out var fileCount))
            {
                if (Directory.Exists(folderPath))
                {
                    fileCount = Directory.GetFiles(folderPath).Length;
                }
                m_FileCounts.Add(folderPath, fileCount);
            }
            return fileCount;
        }

        private void IncrementFileCount(string folderPath) => ++m_FileCounts[folderPath];

        public bool IsSendEnabled => m_Settings.SendToServer;

        public async Task<bool> SendFile(DicomFile file)
        {
            if (m_Settings.ProtocolName != "")
            {
                file.Dataset.AddOrUpdate(DicomTag.ProtocolName, m_Settings.ProtocolName);
            }

            try
            {
                var client = new DicomStoreClient(m_Settings.ServerHost, m_Settings.ServerPort, m_Settings.ServerAeTitle, m_Settings.ClientAeTitle);
                await client.StoreAsync(new[] { file });
                Log.Information("File sent");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
                return false;
            }
        }

        public void AddDigitizerInfo(Dictionary<string, string> tags)
        {
            if (m_Settings.DensityRangeOnUpload?.Length > 1)
            {
                tags[ScanServiceBase.BitsPerPixelKey] = m_Settings.BitsPerPixelOnUpload.ToString();
                tags[ScanServiceBase.MinDensityKey] = m_Settings.DensityRangeOnUpload[0].ToString(CultureInfo.InvariantCulture);
                tags[ScanServiceBase.MaxDensityKey] = m_Settings.DensityRangeOnUpload[1].ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
