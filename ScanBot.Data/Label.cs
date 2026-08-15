using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ScanBot.Data
{
    public class Label
    {
        public string Text { get; set; }

        public Rectangle Rect { get; set; }

        public bool OrientationHint { get; set; }

        [JsonIgnore]
        public Point Center => new(Rect.X + Rect.Width / 2, Rect.Y + Rect.Height / 2);

        public override string ToString() => Text;

        // xDistance/yDistance are separate because the two axes measure different things: yDistance
        // tolerates vertical jitter within the same physical line (true same-line pairs run ~5-15px
        // apart), while xDistance bridges the horizontal gap between characters of the same field.
        // A single shared distance can't satisfy both - the gap that must merge on one field can be
        // larger than the gap that must NOT merge between two unrelated but vertically-close lines.
        // isLocked flags a group whose accumulated text already fully matches a "complete" tag
        // pattern (e.g. a finished Piece_No_2 value) so nothing more merges into it; without this,
        // widening xDistance enough to bridge a fragmented field also fuses adjacent already-complete
        // fields together. Only use it on tags with a fixed-length, unambiguous-when-complete pattern
        // - a variable-length pattern can look "complete" before the real value has fully merged.
        public static List<Label> Merge(List<Label> labels, int xDistance, int yDistance, Func<string, bool> isLocked)
        {
            var resultLabels = new List<Label>();
            foreach (var label in labels.Where(label => !label.OrientationHint).OrderBy(label => label.Center.X))
            {
                // Find the nearest candidate by position alone first, then decide whether locking
                // allows the merge - checking !isLocked(label2.Text) as part of the search predicate
                // would make LastOrDefault skip a locked nearest neighbor and reach past it to a
                // farther unlocked one, which is exactly the cross-field fusion locking is meant to
                // prevent. A label that is itself already a complete locked value (e.g. a full
                // Piece_No_2 token in one OCR box) must not be merged into anything either.
                var nearLabel = resultLabels.LastOrDefault(label2 =>
                    Math.Abs(label.Center.Y - label2.Center.Y) < yDistance &&
                    Math.Abs(label.Center.X - label2.Center.X) - (label.Rect.Width + label2.Rect.Width) / 2 < xDistance);
                if (nearLabel != null && !isLocked(nearLabel.Text) && !isLocked(label.Text))
                {
                    nearLabel.Text += label.Text;
                    nearLabel.Rect = Rectangle.Union(nearLabel.Rect, label.Rect);
                }
                else
                {
                    resultLabels.Add(label);
                }
            }
            resultLabels.AddRange(labels.Where(label => label.OrientationHint));
            return resultLabels;
        }
    }
}
