using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScanBot.Services
{
    class LaserMarkerService
    {
        readonly byte[,] m_Buffer = new byte[6, 18];

        public void Initialize(int index)
        {
            var rows = m_Buffer.GetLength(0);
            var columns = m_Buffer.GetLength(1);
            for (var i = 0; i < rows; ++i)
            {
                var j = 0;
                m_Buffer[i, j++] = (byte)((index + 1) * 16 + i + 1);
                m_Buffer[i, j++] = 0x10;
                m_Buffer[i, j++] = (byte)(i == 0 ? 'N' : 0x20);
                m_Buffer[i, j++] = (byte)(i == 0 ? 'G' : 0x20);
                while (j < columns)
                {
                    m_Buffer[i, j++] = 0x20;
                }
            }
        }

        public void SetTags(Dictionary<string, string> tags)
        {
            foreach (var rowTemplate in ImageTemplate.Default.LaserMarkerRows)
            {
                var values = rowTemplate.GetTagValues(tags).ToList();
                if (values.Count > 0)
                {
                    var ok = rowTemplate.Row == 0 && ImageTemplate.Default.FilmTypes.Any(filmTypeTemplate => filmTypeTemplate.ContainsTags(tags));
                    SetTag(rowTemplate.Row, string.Join(" ", values), ok);
                }
            }
        }

        private void SetTag(int row, string value, bool ok)
        {
            if (ok)
            {
                m_Buffer[row, 2] = (byte)'O';
                m_Buffer[row, 3] = (byte)'K';
            }
            var data = Encoding.ASCII.GetBytes(value);
            var columns = m_Buffer.GetLength(1);
            for (var i = 0; i < data.Length; ++i)
            {
                if (i + 4 >= columns)
                {
                    break;
                }
                m_Buffer[row, i + 4] = data[i];
            }
        }

        public byte[] GetBuffer()
        {
            var buffer = new byte[m_Buffer.Length];
            Buffer.BlockCopy(m_Buffer, 0, buffer, 0, m_Buffer.Length);
            return buffer;
        }
    }
}
