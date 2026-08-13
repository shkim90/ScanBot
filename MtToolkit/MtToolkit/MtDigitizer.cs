using NTwain;
using NTwain.Data;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace MtToolkit
{
    public class MtDigitizer : IDisposable
    {
        TwainSession m_Session;
        DataSource m_DataSource;
        readonly MtCapabilities m_MtCapabilities;

        public MtDigitizer(string name)
        {
            var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, GetType().Assembly);
            appId.ProductName = "Microtek Toolkit";
            appId.ProductFamily = appId.ProductName;
            m_Session = new TwainSession(appId);
            m_Session.TransferReady += Session_TransferReady;
            m_Session.DataTransferred += Session_DataTransferred;
            m_Session.SourceDisabled += Session_SourceDisabled;
            if (m_Session.Open() != ReturnCode.Success)
            {
                throw new InvalidOperationException("Cannot open TWAIN source manager");
            }

            if (m_Session.OpenSource($"Microtek ScanWizard {name}") != ReturnCode.Success)
            {
                throw new InvalidOperationException("Cannot open Microtek TWAIN source");
            }
            m_DataSource = m_Session.CurrentSource;

            m_DataSource.Capabilities.CapIndicators.SetValue(BoolType.False);
            m_DataSource.Capabilities.ICapXferMech.SetValue(XferMech.Memory);
            m_MtCapabilities = new MtCapabilities(m_DataSource);
            m_MtCapabilities.AutoCrop.SetValue(BoolType.True);
            m_MtCapabilities.AutoLevel.SetValue(BoolType.False);
        }

        public void Dispose()
        {
            if (m_DataSource != null)
            {
                m_DataSource.Close();
                m_DataSource = null;
            }

            if (m_Session != null)
            {
                m_Session.Close();
                m_Session.TransferReady -= Session_TransferReady;
                m_Session.DataTransferred -= Session_DataTransferred;
                m_Session.SourceDisabled -= Session_SourceDisabled;
                m_Session = null;
            }
        }

        public string SerialNumber => m_DataSource.Capabilities.CapSerialNumber.GetCurrent();

        public int[] SupportedBitsPerPixelValues => m_DataSource.Capabilities.ICapBitDepth.GetValues().ToArray();

        public int BitsPerPixel
        {
            get => m_DataSource.Capabilities.ICapBitDepth.GetCurrent();
            set => m_DataSource.Capabilities.ICapBitDepth.SetValue(value);
        }

        public int BytesPerPixel => (BitsPerPixel + 7) / 8;

        public float Resolution
        {
            get => m_DataSource.Capabilities.ICapXResolution.GetCurrent();
            set => m_DataSource.Capabilities.ICapXResolution.SetValue(value);
        }

        public RectangleF FrameArea
        {
            get
            {
                var frame = m_DataSource.Capabilities.ICapFrames.GetCurrent();
                return RectangleF.FromLTRB(frame.Left, frame.Top, frame.Right, frame.Bottom);
            }
            set
            {
                var frame = new TWFrame { Left = value.Left, Top = value.Top, Right = value.Right, Bottom = value.Bottom };
                m_DataSource.Capabilities.ICapFrames.SetValue(frame);
            }
        }

        public MtDensity Density
        {
            get => (MtDensity)m_MtCapabilities.Density.GetValues().ElementAt(m_MtCapabilities.Density.GetCurrent());
            set => m_MtCapabilities.Density.SetValue((int)value);
        }

        public float[] DensityRange => m_MtCapabilities.DensityRange.GetValues().ToArray();

        public bool AutoFilmFeeder
        {
            get => m_MtCapabilities.AutoFilmFeeder.GetCurrent() == BoolType.True;
            set => m_MtCapabilities.AutoFilmFeeder.SetValue(value ? BoolType.True : BoolType.False);
        }

        public bool MultiChannelCrop
        {
            get => m_MtCapabilities.MultiChannelCrop.GetCurrent() == BoolType.True;
            set => m_MtCapabilities.MultiChannelCrop.SetValue(value ? BoolType.True : BoolType.False);
        }

        readonly AutoResetEvent m_SourceDisabledEvent = new AutoResetEvent(false);

        public void ScanFilm()
        {
            if (m_DataSource.Enable(SourceEnableMode.NoUI, false, IntPtr.Zero) != ReturnCode.Success)
            {
                throw new InvalidOperationException("Cannot scan");
            }
            m_SourceDisabledEvent.WaitOne();
        }

        MemoryStream m_Stream;
        TWImageInfo m_ImageInfo;

        private void Session_TransferReady(object sender, TransferReadyEventArgs e)
        {
            m_Stream = new MemoryStream();
            m_ImageInfo = e.PendingImageInfo;
        }

        private void Session_DataTransferred(object sender, DataTransferredEventArgs e)
        {
            m_Stream.Write(e.MemoryData, 0, e.MemoryData.Length);

            if (e.MemoryInfo.YOffset + e.MemoryInfo.Rows == m_ImageInfo.ImageLength)
            {
                FilmScanned?.Invoke(this, new ImageEventArgs(m_Stream.ToArray(), m_ImageInfo.ImageWidth));
                m_Stream.Close();
                m_Stream = null;
            }
        }

        private void Session_SourceDisabled(object sender, EventArgs e)
        {
            m_SourceDisabledEvent.Set();
        }

        public event EventHandler<ImageEventArgs> FilmScanned;
    }
}
