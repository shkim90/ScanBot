using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using ScanBot.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ScanBot.Services
{
    class OcrService
    {
        readonly Settings.OcrSettings m_Settings;
        readonly IOcrEngine m_Engine;
        readonly Dictionary<string, FlipType?> m_OrientationHintTags;

        public OcrService(Settings settings, IOcrEngine engine)
        {
            m_Settings = settings.Ocr;
            m_Engine = engine;
            m_OrientationHintTags = ImageTemplate.Default.OrientationHintTags.ToDictionary(tag => tag.Text, tag => tag.Flip);

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CloudKey.json"));
        }

        public async Task<(Dictionary<string, string>, bool)> FindTags(Image<Gray, ushort> image, ushort resolution)
        {
            var labels = await FindLabels(image, resolution);
            var imageModified = false;
            if (m_Settings.RecognizeOrientation)
            {
                var orientationHintLabel = labels.FirstOrDefault(label => label.OrientationHint);
                if (orientationHintLabel != null)
                {
                    var flipType = m_OrientationHintTags[orientationHintLabel.Text];
                    if (flipType != null)
                    {
                        image._Flip(flipType.Value);
                        imageModified = true;
                        labels = await FindLabels(image, resolution);
                    }
                }
            }

            // Safety net for noise that OCR detects as multiple fragments and only becomes a
            // complete match for an ignored pattern once Label.Merge reassembles it - the pre-merge
            // filter in FindLabels only catches noise that already reads as one complete token.
            labels.RemoveAll(label => !label.OrientationHint && ImageTemplate.Default.MatchesIgnoredTagPattern(label.Text));
            labels.ForEach(label => label.Text = ImageTemplate.Default.MapStrings(label.Text));
            return (FindTags(labels), imageModified);
        }

        private async Task<List<Label>> FindLabels(Image<Gray, ushort> image, ushort resolution)
        {
            using var byteImage = image.ToByteImage();
            var rawLabels = await m_Engine.FindLabels(byteImage);
            // OrientationHint must be set before ignored-pattern filtering, and an orientation
            // marker must never be dropped by that filter - otherwise, if a future template edit
            // ever makes an ignored_tag_pattern match an orientation_hint_tags text, the marker
            // would be deleted before FindTags ever gets a chance to find it as the hint, and the
            // required flip would be silently skipped.
            rawLabels.ForEach(label => label.OrientationHint = m_OrientationHintTags.ContainsKey(label.Text));
            // Ignored noise (IQI markers, etc.) is filtered before merging, not after: once a noise
            // token has fused onto real data (e.g. "95P"+"IQI"+"T12" -> "95PIQIT12"), no pattern
            // match can tell the noise apart from the data anymore, so removing it post-merge either
            // misses it or - anchored - has to keep the whole contaminated label. Removing it here
            // stops it from ever becoming a merge candidate in the first place. A post-merge pass
            // still runs in FindTags as a safety net for noise OCR splits into several fragments
            // that only becomes a complete ignored token once merge reassembles it.
            var labels = rawLabels
                .Where(label => label.OrientationHint || !ImageTemplate.Default.MatchesIgnoredTagPattern(label.Text))
                .ToList();
            var pixelSpacing = 25.4 / resolution;
            var mergeXDistance = (int)Math.Round(m_Settings.MergeXDistanceInMm / pixelSpacing);
            var mergeYDistance = (int)Math.Round(m_Settings.MergeYDistanceInMm / pixelSpacing);
            labels = Label.Merge(labels, mergeXDistance, mergeYDistance, IsLockedTagValue);
            return labels;
        }

        private static bool IsLockedTagValue(string text) =>
            ImageTemplate.Default.Tags.Any(tagTemplate => tagTemplate.LockWhenMatched && tagTemplate.MatchesPattern(text));

        private static Dictionary<string, string> FindTags(List<Label> labels)
        {
            var lookup = labels
                .ToLookup(label => ImageTemplate.Default.Tags.FirstOrDefault(tagTemplate => tagTemplate.MatchesPattern(label.Text)));
            return lookup
                .Where(group =>
                {
                    var tagTemplate = group.Key;
                    return tagTemplate?.Multiplicity == group.Count();
                })
                .ToDictionary(group =>
                {
                    var tagTemplate = group.Key;
                    return tagTemplate.Key;
                }, group =>
                {
                    var tagTemplate = group.Key;
                    var labels = group.ToList();
                    return labels.Count == 1 ? tagTemplate.RetrieveTextByPattern(labels[0].Text) :
                        string.Join(tagTemplate.Separator, labels.OrderBy(label => label.Rect.X).Select(label => tagTemplate.RetrieveTextByPattern(label.Text)));
                });
        }
    }
}
