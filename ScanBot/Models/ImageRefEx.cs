using ScanBot.Data;
using System.Collections.Generic;

namespace ScanBot.Models
{
    class ImageRefEx
    {
        public ImageRefEx(ImageRef source, bool sent)
        {
            Source = source;
            Sent = sent;
        }

        public ImageRef Source { get; }

        public bool Sent { get; }

        Dictionary<string, string> m_Tags;

        public Dictionary<string, string> Tags => m_Tags ??= Source.DeserializeTags();
    }
}
