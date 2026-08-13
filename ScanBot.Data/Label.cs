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

        public static List<Label> Merge(List<Label> labels, int distance)
        {
            var resultLabels = new List<Label>();
            foreach (var label in labels.Where(label => !label.OrientationHint).OrderBy(label => label.Center.X))
            {
                var nearLabel = resultLabels.LastOrDefault(label2 =>
                    Math.Abs(label.Center.Y - label2.Center.Y) < distance &&
                    Math.Abs(label.Center.X - label2.Center.X) - (label.Rect.Width + label2.Rect.Width) / 2 < distance);
                if (nearLabel != null)
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
