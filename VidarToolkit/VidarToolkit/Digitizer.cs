using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VidarToolkit
{
    public class Digitizer : IDigitizer
    {
        public Digitizer()
        {
            StopLogging();

            var errorCode = OpenToolKit(0, out var _);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }

            errorCode = FindScanner(out var _);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            errorCode = CheckStatus();
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }

            GetCapabilities();
        }

        public void Dispose()
        {
            var errorCode = CloseToolKit();
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
        }

        public bool IsConnected()
        {
            var serialNumber = new StringBuilder();
            var errorCode = GetSerialNo(serialNumber);
            return errorCode == cRECEIVECOMPLETE;
        }

        ushort m_BitsPerPixel;
        ushort m_Resolution;

        private void GetCapabilities()
        {
            var values = new ushort[14];
            var errorCode = GetCapability(Feature.CAP_BIT_DEPTH, values, (ushort)values.Length);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            SupportedBitsPerPixelValues = values.Skip(4).Take(values[1]).ToArray();
            m_BitsPerPixel = SupportedBitsPerPixelValues[values[2]];

            errorCode = GetCapability(Feature.CAP_RESOLUTION, values, (ushort)values.Length);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            SupportedResolutionValues = values.Skip(4).Take(values[1]).ToArray();
            m_Resolution = SupportedResolutionValues[values[2]];

            var serialNumber = new StringBuilder();
            errorCode = GetSerialNo(serialNumber);
            if (errorCode != cRECEIVECOMPLETE)
            {
                ThrowError(errorCode);
            }
            SerialNumber = serialNumber.ToString();
        }

        public ushort[] SupportedBitsPerPixelValues { get; private set; }

        public ushort BitsPerPixel
        {
            get => m_BitsPerPixel;
            set
            {
                if (!SupportedBitsPerPixelValues.Contains(value))
                {
                    throw new ArgumentException("Out of supported values", nameof(value));
                }
                m_BitsPerPixel = value;
            }
        }

        public int BytesPerPixel => (m_BitsPerPixel + 7) / 8;

        public ushort[] SupportedResolutionValues { get; private set; }

        public ushort Resolution
        {
            get => m_Resolution;
            set
            {
                if (!SupportedResolutionValues.Contains(value))
                {
                    throw new ArgumentException("Out of supported values", nameof(value));
                }
                m_Resolution = value;
            }
        }

        public string SerialNumber { get; private set; }

        public bool AutoFeeder { get; set; }

        public bool HalfSpeed { get; set; }

        public bool StageFilm()
        {
            var errorCode = GetPaperStatus(out var status);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            if (!AutoFeeder)
            {
                return status;
            }
            if (!status)
            {
                errorCode = StageFilmInFD();
                if (errorCode == cFD_TIMEOUT)
                {
                    return false;
                }
                if (errorCode != 0)
                {
                    ThrowError(errorCode);
                }
            }
            return true;
        }

        public void EjectFilm()
        {
            var errorCode = UnloadMedium();
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            errorCode = UnloadMedium();
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
        }

        bool m_Abort;

        public void AbortFilm() => m_Abort = true;

        public bool ScanFilm(Stream stream, double widthInInches, double lengthInInches, out short detectedWidth, out int detectedLength)
        {
            var width = (ushort)(widthInInches * m_Resolution);
            var length = (uint)(lengthInInches * m_Resolution);
            SetScanParameters(width, length);
            var errorCode = InitScanner();
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            var stride = width * BytesPerPixel;

            m_Abort = false;

            var buffer = new byte[0x10000];
            unsafe
            {
                fixed (byte* p = buffer)
                {
                    while (true)
                    {
                        if (m_Abort)
                        {
                            errorCode = StopScan();
                            if (errorCode != 0)
                            {
                                ThrowError(errorCode);
                            }
                            break;
                        }
                        errorCode = GetImage(p, buffer.Length, out var returnSize);
                        if (errorCode != 0 && errorCode != cENDOFPAPER)
                        {
                            ThrowError(errorCode);
                        }
                        stream.Write(buffer, 0, returnSize);
                        ScanningFilm?.Invoke(this, new ScanEventArgs((int)(stream.Position / stride)));
                        if (errorCode == cENDOFPAPER)
                        {
                            break;
                        }
                    }
                }
            }

            errorCode = GetDetectedWidth(out detectedWidth);
            if (errorCode != cRECEIVECOMPLETE)
            {
                ThrowError(errorCode);
            }
            errorCode = GetLinesScanned(out detectedLength);
            if (errorCode != cRECEIVECOMPLETE)
            {
                ThrowError(errorCode);
            }
            if (detectedWidth < width)
            {
                CropWidth(stream, (short)width, detectedWidth, detectedLength);
            }
            else
            {
                detectedWidth = (short)width;
            }
            return !m_Abort;
        }

        private void SetScanParameters(ushort width, uint length)
        {
            var errorCode = SetBitsPerPixel((byte)m_BitsPerPixel);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            errorCode = SetResolution(m_Resolution);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            errorCode = SetPixelSize(width, length);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            switch (m_BitsPerPixel)
            {
                case 8:
                    errorCode = SetOutputMode(OutputMode.cGRAYSCALE_8);
                    break;
                case 12:
                    errorCode = SetOutputMode(OutputMode.cGRAYSCALE_12);
                    break;
                case 16:
                    errorCode = SetOutputMode(OutputMode.cGRAYSCALE_16);
                    break;
            }
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            errorCode = SetScanMode(ScanMode.SM_SOFTSENSOR | ScanMode.SM_LINES);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            errorCode = SetBitOrder(BitOrder.BO_INTEL);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
            errorCode = SetFractionalSpeed(HalfSpeed ? 2 : 1);
            if (errorCode != 0)
            {
                ThrowError(errorCode);
            }
        }

        private void CropWidth(Stream stream, short oldWidth, short newWidth, int length)
        {
            var oldStride = oldWidth * BytesPerPixel;
            var newStride = newWidth * BytesPerPixel;
            var buffer = new byte[newStride];
            for (int i = 0, oldOffset = 0, newOffset = 0; i < length; ++i, oldOffset += oldStride, newOffset += newStride)
            {
                stream.Position = oldOffset;
                stream.Read(buffer, 0, buffer.Length);
                stream.Position = newOffset;
                stream.Write(buffer, 0, buffer.Length);
            }
            stream.SetLength(stream.Position);
        }

        private static void ThrowError(int errorCode) => throw new InvalidOperationException($"Error #{errorCode}");

        public event EventHandler<ScanEventArgs> ScanningFilm;

        #region P/Invoke

        const string m_DllName = "VToolkit.dll";

        [DllImport(m_DllName, EntryPoint = "StopLogging")]
        private extern static short StopLogging();

        [DllImport(m_DllName, EntryPoint = "openToolKit")]
        private extern static short OpenToolKit(ushort id, out ushort capabilities);

        [DllImport(m_DllName, EntryPoint = "closeToolKit")]
        private extern static short CloseToolKit();

        [DllImport(m_DllName, EntryPoint = "findScanner")]
        private extern static short FindScanner(out byte model);

        [DllImport(m_DllName, EntryPoint = "CheckStatus")]
        private extern static short CheckStatus();

        [DllImport(m_DllName, EntryPoint = "getCapability")]
        private extern static short GetCapability(Feature feature, ushort[] values, ushort length);

        [DllImport(m_DllName, EntryPoint = "getPaperStatus")]
        private extern static short GetPaperStatus([MarshalAs(UnmanagedType.U1)] out bool paperStatus);

        [DllImport(m_DllName, EntryPoint = "stageFilmInFD")]
        private extern static short StageFilmInFD();

        [DllImport(m_DllName, EntryPoint = "stopScan")]
        private extern static short StopScan();

        [DllImport(m_DllName, EntryPoint = "unloadMedium")]
        private extern static short UnloadMedium();

        [DllImport(m_DllName, EntryPoint = "setBitsPerPixel")]
        private extern static short SetBitsPerPixel(byte bitsPerPixel);

        [DllImport(m_DllName, EntryPoint = "setResolution")]
        private extern static short SetResolution(ushort resolution);

        [DllImport(m_DllName, EntryPoint = "setPixelSize")]
        private extern static short SetPixelSize(ushort width, uint length);

        [DllImport(m_DllName, EntryPoint = "setOutputMode")]
        private extern static short SetOutputMode(OutputMode outputMode);

        [DllImport(m_DllName, EntryPoint = "setScanMode")]
        private extern static short SetScanMode(ScanMode action);

        [DllImport(m_DllName, EntryPoint = "setBitOrder")]
        private extern static short SetBitOrder(BitOrder bitOrder);

        [DllImport(m_DllName, EntryPoint = "initScanner")]
        private extern static short InitScanner();

        [DllImport(m_DllName, EntryPoint = "getImage")]
        unsafe private extern static short GetImage(byte* imageBuffer, int requestSize, out int returnSize);

        [DllImport(m_DllName, EntryPoint = "getDetectedWidth")]
        private extern static short GetDetectedWidth(out short width);

        [DllImport(m_DllName, EntryPoint = "getLinesScanned")]
        private extern static short GetLinesScanned(out int numLines);

        [DllImport(m_DllName, EntryPoint = "setFractionalSpeed")]
        private extern static short SetFractionalSpeed(int factor);

        [DllImport(m_DllName, EntryPoint = "getSerialNo")]
        private extern static short GetSerialNo(StringBuilder serialNo);

        enum Feature : ushort
        {
            CAP_RESOLUTION = 0,
            CAP_BIT_DEPTH = 2
        }

        enum OutputMode : byte
        {
            cBINARY = 0,
            cDITHER = 1,
            cGRAYSCALE = 2,
            cGRAYSCALE_8 = 4,
            cGRAYSCALE_16 = 6,
            cGRAYSCALE_12 = 7
        }

        [Flags]
        enum ScanMode
        {
            SM_LINES = 0,
            SM_SOFTSENSOR = 1,
            SM_HARDSENSOR = 2,
            SM_SWITCH = 4
        }

        enum BitOrder : ushort
        {
            BO_MOTOROLA = 0,
            BO_INTEL = 1
        }

        const ushort cRECEIVECOMPLETE = 6;
        const ushort cENDOFPAPER = 10;
        const ushort cFD_TIMEOUT = 18;

        #endregion
    }
}
