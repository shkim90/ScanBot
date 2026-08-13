using Emgu.CV;
using Emgu.CV.Structure;
using ScanBot.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    interface IOcrEngine
    {
        Task<List<Label>> FindLabels(Image<Gray, byte> image);
    }
}
