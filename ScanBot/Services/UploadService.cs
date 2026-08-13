using Serilog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    class UploadService
    {
        readonly IScanService m_ScanService;
        readonly Settings.StoreSettings m_Settings;

        public UploadService(IScanService scanService, Settings settings)
        {
            m_ScanService = scanService;
            m_Settings = settings.Store;
        }

        bool m_IsRunning;
        Task m_JobTask;

        public void Start()
        {
            if (Directory.Exists(m_Settings.UploadFolderPath))
            {
                m_IsRunning = true;
                m_JobTask = Task.Run(async() => await JobRunner());
            }
        }

        public void Stop()
        {
            m_IsRunning = false;
            m_JobTask?.Wait();
            m_JobTask = null;
        }

        private void FolderWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (File.Exists(e.FullPath))
            {
                Task.Run(async () => await UploadFile(e.FullPath));
            }
        }

        readonly ConcurrentQueue<string> m_JobQueue = new();

        private async Task UploadFile(string filePath)
        {
            Log.Information("Auto-uploading file {FilePath}", filePath);
            await Task.Delay(TimeSpan.FromSeconds(m_Settings.AutoUploadDelay));
            m_JobQueue.Enqueue(filePath);
        }

        private async Task JobRunner()
        {
            using var folderWatcher = new FileSystemWatcher(m_Settings.UploadFolderPath, "*.tif")
            {
                IncludeSubdirectories = true
            };
            folderWatcher.Created += FolderWatcher_Created;
            folderWatcher.EnableRaisingEvents = true;
            Log.Information("Monitoring folder {FolderPath}", folderWatcher.Path);

            while (m_IsRunning)
            {
                if (m_JobQueue.TryDequeue(out var filePath))
                {
                    try
                    {
                        var data = File.ReadAllBytes(filePath);
                        await m_ScanService.ImportImageFile(data, StoreService.GetImageResolution(data), m_Settings.RotateOnUpload);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred");
                    }
                }
                else
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}
