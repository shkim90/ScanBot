using System;

namespace MtToolkit
{
    public class ImageEventArgs : EventArgs
    {
        public ImageEventArgs(byte[] data, int width)
        {
            Data = data;
            Width = width;
        }

        public byte[] Data { get; }

        public int Width { get; }
    }
}
