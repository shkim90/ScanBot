using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    class ControlService
    {
        readonly IScanService m_ScanService;
        readonly LaserMarkerService m_LaserMarkerService;
        readonly Settings.ControlSettings m_Settings;
        readonly IServiceScopeFactory m_ServiceScopeFactory;
        readonly SerialPort m_Port;

        public ControlService(IScanService scanService, LaserMarkerService laserMarkerService, Settings settings, IServiceScopeFactory serviceScopeFactory)
        {
            m_ScanService = scanService;
            m_LaserMarkerService = laserMarkerService;
            m_Settings = settings.Control;
            m_ServiceScopeFactory = serviceScopeFactory;
            m_Port = new()
            {
                ReadTimeout = 1000
            };
        }

        bool m_IsReady = true;

        public bool IsReady
        {
            get => m_IsReady;
            set
            {
                if (m_IsReady != value)
                {
                    m_IsReady = value;
                    IsReadyChanged?.Invoke();
                }
            }
        }

        public event Action IsReadyChanged;

        bool m_IsScanning;
        bool m_IsEjecting;

        public string[] GetSerialPortNames() => SerialPort.GetPortNames().OrderBy(name => name).ToArray();

        AutoFeeder m_AutoFeeder;

        public void Start()
        {
            if (m_Settings.NewAutoFeeder)
            {
                try
                {
                    m_AutoFeeder = new();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred");
                }
            }

            if (string.IsNullOrEmpty(m_Settings.SerialPort))
            {
                Log.Warning("Serial port not defined");
                return;
            }

            try
            {
                m_Port.PortName = m_Settings.SerialPort;
                m_Port.Open();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred");
                return;
            }

            m_IsScanning = false;
            m_IsEjecting = false;

            Task.Run(() =>
            {
                while (m_Port.IsOpen)
                {
                    if (m_Port.BytesToRead > 0)
                    {
                        try
                        {
                            var command = m_Port.ReadLine();
                            Log.Debug("Command: {Command}", command);
                            var binaryResult = ExecuteBinaryCommand(command);
                            if (binaryResult != null)
                            {
                                m_Port.Write(binaryResult, 0, binaryResult.Length);
                            }
                            else
                            {
                                var result = ExecuteCommand(command);
                                Log.Debug("Result: {Result}", result);
                                m_Port.WriteLine(result ?? "NG");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception occurred");
                            m_Port.WriteLine("NG");
                        }
                    }
                    Thread.Sleep(100);
                }
                Log.Information("Control service stopped");
            });
            Log.Information("Control service started on {SerialPort}", m_Settings.SerialPort);
        }

        public void Stop()
        {
            if (m_AutoFeeder != null)
            {
                m_AutoFeeder.Dispose();
                m_AutoFeeder = null;
            }

            m_Port.Close();
        }

        private string ExecuteCommand(string command)
        {
            return ExecuteReadyCommand(command) ??
                ExecuteScanCommand(command) ??
                ExecuteEjectCommand(command) ??
                ExecuteResolutionCommand(command) ??
                ExecuteRestartCommand(command);
        }

        private string ExecuteReadyCommand(string command)
        {
            if (command == "RD?")
            {
                return $"RD={GetDigitizerValue(IsReady)}";
            }
            return null;
        }

        private string ExecuteScanCommand(string command)
        {
            if (command == "SC?")
            {
                return $"SC={GetDigitizerValue(m_IsScanning)}";
            }
            if (command.StartsWith("SC="))
            {
                if (!m_ScanService.IsDigitizerConnected)
                {
                    return "NG";
                }
                if (int.Parse(command[3..]) != 0)
                {
                    ScanFilm();
                }
                else
                {
                    AbortFilm();
                }
                return "OK";
            }
            return null;
        }

        public void ScanFilm()
        {
            DoWork(() =>
            {
                if (m_AutoFeeder != null)
                {
                    while (m_AutoFeeder.Start())
                    {
                        if (!ScanFilmCore())
                        {
                            break;
                        }
                    }
                }
                else
                {
                    ScanFilmCore();
                }
            });
        }

        private bool ScanFilmCore()
        {
            m_IsScanning = true;
            try
            {
                Log.Information("Film scanning");
                var scanned = m_ScanService.ScanFilm();
                Log.Information(scanned ? "Film scanned" : "Film not scanned");
                return scanned;
            }
            finally
            {
                m_IsScanning = false;
            }
        }

        public void AbortFilm()
        {
            Log.Information("Abort scanning");
            m_ScanService.AbortFilm();
        }

        private string ExecuteEjectCommand(string command)
        {
            if (command == "EJ?")
            {
                return $"EJ={GetDigitizerValue(m_IsEjecting)}";
            }
            if (command.StartsWith("EJ="))
            {
                if (!m_ScanService.IsDigitizerConnected)
                {
                    return "NG";
                }
                if (int.Parse(command[3..]) != 0)
                {
                    EjectFilm();
                }
                return "OK";
            }
            return null;
        }

        public void EjectFilm()
        {
            DoWork(() =>
            {
                m_IsEjecting = true;
                try
                {
                    Log.Information("Film ejecting");
                    m_ScanService.EjectFilm();
                    Log.Information("Film ejected");
                }
                finally
                {
                    m_IsEjecting = false;
                }
            });
        }

        private string ExecuteResolutionCommand(string command)
        {
            if (command == "RS?")
            {
                return $"RS={GetDigitizerValue(m_ScanService.Resolution)}";
            }
            if (command.StartsWith("RS="))
            {
                var resolution = ushort.Parse(command[3..]);
                try
                {
                    m_ScanService.Resolution = resolution;
                    Log.Information("Resolution sets to {Resolution} dpi", m_ScanService.Resolution);
                }
                catch
                {
                    Log.Information("Resolution cannot set to {InvalidResolution} dpi, still {Resolution} dpi", resolution, m_ScanService.Resolution);
                }
                return "OK";
            }
            return null;
        }

        private string ExecuteRestartCommand(string command)
        {
            if (command == "RST")
            {
                DoWork(() =>
                {
                    m_ScanService.Stop();
                    m_ScanService.Start();
                });
                return "OK";
            }
            return null;
        }

        private byte[] ExecuteBinaryCommand(string command)
        {
            var result = ExecuteLaserMakerCommand(command);
            if (result != null)
            {
                SaveLaserMakerJob(result);
            }
            return result;
        }

        private byte[] ExecuteLaserMakerCommand(string command)
        {
            if (command == "LM?")
            {
                using var scope = m_ServiceScopeFactory.CreateScope();
                var storeService = scope.ServiceProvider.GetService<StoreService>();

                var imageRefs = storeService.GetLastScanImageRefs();
                if (imageRefs != null)
                {
                    using var stream = new MemoryStream();
                    for (var i = 0; i < 4; ++i)
                    {
                        m_LaserMarkerService.Initialize(i);
                        if (i < imageRefs.Count)
                        {
                            m_LaserMarkerService.SetTags(imageRefs[i].DeserializeTags());
                        }
                        var buffer = m_LaserMarkerService.GetBuffer();
                        stream.Write(buffer, 0, buffer.Length);
                    }
                    stream.WriteByte(0x0a);
                    return stream.ToArray();
                }
            }
            return null;
        }

        private static void SaveLaserMakerJob(byte[] buffer)
        {
            var timestamp = DateTime.Now;
            var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LaserMakerJobs", timestamp.ToString("yyyyMMdd"));
            Directory.CreateDirectory(folderPath);
            File.WriteAllBytes(Path.Combine(folderPath, timestamp.ToString("HHmmss") + ".txt"), buffer);
        }

        private int GetDigitizerValue(bool value) => !IsReady || m_ScanService.IsDigitizerConnected ? value ? 1 : 0 : 2;

        private int GetDigitizerValue(int value) => !IsReady || m_ScanService.IsDigitizerConnected ? value : 0;

        private void DoWork(Action action)
        {
            Task.Run(() =>
            {
                if (!IsReady)
                {
                    Log.Warning("Not ready");
                    return;
                }

                IsReady = false;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred");
                }
                finally
                {
                    IsReady = true;
                }
            });
        }
    }
}
