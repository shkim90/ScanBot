using Emgu.CV;
using Emgu.CV.Structure;
using ScanBot.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    // Used in place of any real engine when Host is "localhost" (optionally with a port), so the
    // scan/DICOM pipeline can be exercised without a live OCR backend - regardless of which engine
    // is otherwise configured in Settings.Ocr.Engine (see Startup.ConfigureServices).
    class NullOcrEngine : IOcrEngine
    {
        public Task<List<Label>> FindLabels(Image<Gray, byte> byteImage) => Task.FromResult(new List<Label>());
    }
}
