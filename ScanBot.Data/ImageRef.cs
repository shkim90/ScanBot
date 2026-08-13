using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScanBot.Data
{
    public class ImageRef
    {
        public ImageRef()
        {
            Timestamp = DateTime.Now;
        }

        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        [MaxLength(256)]
        public string FolderName{ get; set; }

        [Required]
        [MaxLength(256)]
        public string FileName { get; set; }

        [Required]
        [MaxLength(1024)]
        public string Tags { get; set; }

        public void SerializeTags(Dictionary<string, string> tags) => Tags = JsonConvert.SerializeObject(tags);

        public Dictionary<string, string> DeserializeTags() => JsonConvert.DeserializeObject<Dictionary<string, string>>(Tags);
    };
}
