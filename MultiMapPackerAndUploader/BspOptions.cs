using Microsoft.Extensions.Logging;
using Steamworks;

namespace MapPackerAndUploader
{
    public class BspOptions
    {
        public string FileName = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string AbsOutputPath { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool Compress { get; set; } = true;
        public bool IgnoreAssets { get; set; } = false;
        public IList<string> Assets = [];
        public WorkshopOptions Workshop = new();
    }
}
