using System;

namespace VidarToolkit
{
    public class ScanEventArgs : EventArgs
    {
        public ScanEventArgs(int lineCount)
        {
            LineCount = lineCount;
        }

        public int LineCount { get; }
    }
}
