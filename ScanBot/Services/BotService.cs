using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    class BotService : IHostedService
    {
        readonly ControlService m_ControlService;
        readonly IScanService m_ScanService;
        readonly OcrService m_OcrService;
        readonly UploadService m_UploadService;
        readonly DicomService m_DicomService;
        readonly IServiceScopeFactory m_ServiceScopeFactory;

        public BotService(ControlService controlService, IScanService scanService, OcrService ocrService, UploadService uploadService, DicomService dicomService,
            IServiceScopeFactory serviceScopeFactory)
        {
            m_ControlService = controlService;
            m_ScanService = scanService;
            m_ScanService.FilmScanned += ScanService_FilmScanned;
            m_OcrService = ocrService;
            m_UploadService = uploadService;
            m_DicomService = dicomService;
            m_ServiceScopeFactory = serviceScopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            m_ScanService.Start();
            m_ControlService.Start();
            m_UploadService.Start();
            Log.Information("Bot started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            m_ControlService.Stop();
            m_ScanService.Stop();
            m_UploadService.Stop();
            Log.Information("Bot stopped");
            return Task.CompletedTask;
        }

        StoreService m_StoreService;

        private async Task ScanService_FilmScanned(ScanEventArgs e)
        {
            using var scope = m_ServiceScopeFactory.CreateScope();
            m_StoreService = scope.ServiceProvider.GetService<StoreService>();

            var (tags, _) = await m_OcrService.FindTags(e.Image, e.Resolution);
            m_StoreService.AddScanDate(tags);
            if (e.Imported)
            {
                m_StoreService.AddDigitizerInfo(tags);
            }
            else
            {
                m_ScanService.AddDigitizerInfo(tags);
            }

            ProcessImage(e.Image, e.Resolution, e.ScanId, tags);
        }

        private void ProcessImage(Image<Gray, ushort> image, ushort resolution, Guid scanId, Dictionary<string, string> tags)
        {
            var imageFilePath = m_StoreService.SaveImage(image, resolution, scanId);
            var imageRef = m_StoreService.CreateImageRef(imageFilePath, tags);

            if (m_StoreService.IsExportEnabled)
            {
                m_StoreService.ExportFile(image);
            }

            var filmTypeTemplate = ImageTemplate.Default.FilmTypes.FirstOrDefault(filmTypeTemplate => filmTypeTemplate.ContainsTags(tags));
            if (filmTypeTemplate != null)
            {
                if (m_StoreService.IsSendEnabled)
                {
                    var dicomFile = m_DicomService.CreateDicomFile(image, resolution, imageRef.Timestamp, tags, filmTypeTemplate);
                    Task.Run(async () =>
                    {
                        if (await m_StoreService.SendFile(dicomFile))
                        {
                            m_StoreService.PurgeData(imageFilePath);
                        }
                    });
                }
            }
            else
            {
                Log.Warning("Film type not recognized");
            }
        }
    }
}
