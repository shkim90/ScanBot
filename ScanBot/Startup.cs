using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Blazorise.LoadingIndicator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScanBot.Data;
using ScanBot.Services;
using System;

namespace ScanBot
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // Compares only the host part (before an optional ":port") so "localhost:8000" matches
        // the same as bare "localhost", since every real Host value in this config also uses the
        // host:port convention.
        private static bool IsLocalhost(string host) =>
            host?.Split(':')[0].Equals("localhost", StringComparison.OrdinalIgnoreCase) == true;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var settings = Settings.Load();
            services.AddSingleton(settings);
            services.AddHostedService<BotService>();
            services.AddSingleton<ControlService>();
            switch (settings.Digitizer.Device)
            {
                case 0:
                case 1:
                    services.AddSingleton<IScanService, VidarScanService>();
                    break;
                case 2:
                    services.AddSingleton<IScanService, MtScanService>();
                    break;
            }
            services.AddSingleton<OcrService>();
            // Host set to "localhost" (optionally with a port, e.g. "localhost:8000") means no real
            // OCR server is running - exercise the scan/DICOM pipeline without a recognition backend,
            // regardless of which Engine is configured, rather than special-casing one engine only.
            if (IsLocalhost(settings.Ocr.Host))
            {
                services.AddSingleton<IOcrEngine, NullOcrEngine>();
            }
            else
            {
                switch (settings.Ocr.Engine)
                {
                    case 0:
                        services.AddSingleton<IOcrEngine, MagicBoxEngine>();
                        break;
                    case 1:
                        services.AddSingleton<IOcrEngine, NewMagicBoxEngine>();
                        break;
                    case 2:
                        services.AddSingleton<IOcrEngine, GoogleVisionEngine>();
                        break;
                    case 3:
                        services.AddSingleton<IOcrEngine, OcrEngine>();
                        break;
                }
            }
            services.AddTransient<StoreService>();
            services.AddSingleton<UploadService>();
            services.AddSingleton<DicomService>();
            services.AddTransient<LaserMarkerService>();
            services.AddDbContext<AppDbContext>(ServiceLifetime.Transient);
            services.AddLoadingIndicator();

            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddBlazorise(options =>
            {
                options.Immediate = false;
            })
            .AddBootstrapProviders()
            .AddFontAwesomeIcons();

            services.AddBlazoredLocalStorage();
            services.AddBlazoredSessionStorage();

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapControllers();
            });
        }
    }
}
