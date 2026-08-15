using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ScanBot
{
    class ImageTemplate
    {
        static ImageTemplate m_Default;

        public static ImageTemplate Default
        {
            get
            {
                if (m_Default == null)
                {
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build();
                    m_Default = deserializer.Deserialize<ImageTemplate>(File.ReadAllText(FilePath));
                }
                return m_Default;
            }
        }

        private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{nameof(ImageTemplate)}.yml");

        public List<TagTemplate> Tags { get; set; } = new();

        public List<string> IgnoredTagPatterns { get; set; } = new();

        // Anchored so a noise token that gets fused onto legitimate data by an over-merge (e.g.
        // "95PIQIT12") doesn't have the whole label silently deleted just because it CONTAINS a
        // pattern like "IQI" - only a label that IS entirely that noise pattern gets dropped.
        public bool MatchesIgnoredTagPattern(string text) => IgnoredTagPatterns.Any(pattern => Regex.IsMatch(text, "^(" + pattern + ")$"));

        public List<string[]> StringMapping { get; set; } = new();

        public string MapStrings(string text)
        {
            foreach (var strings in StringMapping)
            {
                text = text.Replace(strings[0], strings[1]);
            }
            return text;
        }

        public List<OrientationHintTagTemplate> OrientationHintTags { get; set; } = new();

        public List<FilmTypeTemplate> FilmTypes { get; set; } = new();

        public List<string> StudyTagKeys { get; set; } = new();

        public Dictionary<string, string> GetStudyTags(Dictionary<string, string> tags) => new(tags.Where(tag => StudyTagKeys.Contains(tag.Key)));

        public List<LaserMarkerRowTemplate> LaserMarkerRows { get; set; } = new();

        public class TagTemplate
        {
            public string Key { get; set; }

            public string Pattern { get; set; }

            public bool MatchesPattern(string text) => DateFormat != null ? ConvertToDate(text) != null : Regex.IsMatch(text, "^(" + Pattern + ")$");

            // When true, a merged label that already fully matches this tag is excluded from
            // accepting further merges (see Label.Merge). Only set this on tags whose pattern is a
            // FIXED length and distinctive enough that a partial in-progress value could never falsely
            // match it (e.g. Piece_No_2's P+digit). A variable-length pattern (e.g. \d{1,3}) can look
            // "complete" after the shortest alternative matches, locking out the remaining digits.
            public bool LockWhenMatched { get; set; }

            public string RetrieveTextByPattern(string text)
            {
                var match = Regex.Match(text, "^(" + Pattern + ")$");
                var group = match.Groups["value"];
                return group.Success ? group.Value : match.Value;
            }

            public string DateFormat { get; set; }

            public int Multiplicity { get; set; } = 1;

            public string Separator { get; set; }

            public DateTime? ConvertToDate(string value)
            {
                if (DateFormat != null)
                {
                    var match = Regex.Match(value, @$"\d{{{DateFormat.Length}}}");
                    if (match.Success && DateTime.TryParseExact(match.Value, DateFormat, null, DateTimeStyles.None, out var date))
                    {
                        return date;
                    }
                }
                return null;
            }

            public string DicomTag { get; set; }

            public uint? GetDicomTagValue()
            {
                if (DicomTag != null)
                {
                    try
                    {
                        return uint.Parse(DicomTag.Trim('(', ')').Replace(",", ""), NumberStyles.HexNumber);
                    }
                    catch
                    {
                    }
                }
                return null;
            }
        }

        public class OrientationHintTagTemplate
        {
            public string Text { get; set; }

            public FlipType? Flip { get; set; }
        }

        public class FilmTypeTemplate
        {
            public int Id { get; set; }

            public List<string> TagKeys { get; set; } = new();

            public bool ContainsTags(Dictionary<string, string> tags) => TagKeys.Intersect(tags.Keys).Count() == TagKeys.Count;

            public IEnumerable<string> GetTagValues(Dictionary<string, string> tags) => TagKeys
                .Where(key => tags.ContainsKey(key))
                .Select(key => tags[key]);
        }

        public class LaserMarkerRowTemplate
        {
            public int Row { get; set; }

            public List<string> TagKeys { get; set; } = new();

            public IEnumerable<string> GetTagValues(Dictionary<string, string> tags) => TagKeys
                .Where(key => tags.ContainsKey(key))
                .Select(key => tags[key]);
        }
    }
}
