using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace ScanBot.Services
{
    class AutoFeeder : IDisposable
    {
        readonly Motor m_Motor;

        public AutoFeeder()
        {
            var portName = FindPort();
            if (portName != null)
            {
                m_Motor = new(portName);
            }
            else
            {
                throw new InvalidOperationException("Port not found");
            }
        }

        private static string FindPort()
        {
            foreach (var portName in SerialPort.GetPortNames())
            {
                try
                {
                    using var motor = new Motor(portName);
                    motor.GetSensorState();
                    return portName;
                }
                catch
                {
                }
            }
            return null;
        }

        public void Dispose()
        {
            m_Motor.Dispose();
        }

        public bool Start()
        {
            m_Motor.Start();

            var sensorState = false;
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 10000)
            {
                Thread.Sleep(500);
                sensorState = m_Motor.GetSensorState();
                if (sensorState)
                {
                    Thread.Sleep(2000);
                    break;
                }
            }
            stopwatch.Stop();
            return sensorState;
        }

        class Motor : IDisposable
        {
            readonly SerialPort m_Port;

            public Motor(string portName)
            {
                m_Port = new(portName)
                {
                    NewLine = "\r",
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                m_Port.Open();
            }

            public void Dispose()
            {
                m_Port.Dispose();
            }

            public void Start()
            {
                var response = ExecuteCommand("\x02S010000000");
                if (response != "\x02S010000000")
                {
                    throw new InvalidOperationException("Error response");
                }
            }

            public void Stop()
            {
                var response = ExecuteCommand("\x02S000000001");
                if (response != "\x02S010000000")
                {
                    throw new InvalidOperationException("Error response");
                }
            }

            public bool GetSensorState()
            {
                var response = ExecuteCommand("\x02I00000000B");
                return response switch
                {
                    "\x02I00000000B" => false,
                    "\x02I01000000A" => true,
                    _ => throw new InvalidOperationException("Error response")
                };
            }

            private string ExecuteCommand(string command)
            {
                m_Port.DiscardInBuffer();
                m_Port.WriteLine(command);
                return m_Port.ReadLine();
            }
        }
    }
}
