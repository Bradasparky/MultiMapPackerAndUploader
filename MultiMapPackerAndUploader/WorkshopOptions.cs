using Steamworks;

namespace MapPackerAndUploader
{
    public class WorkshopOptions
    {
        public bool Upload { get; init; } = false;
        public ulong ID { get; init; } = 0;
        public SteamUGCDetails_t Details { get; set; }
        public ERemoteStoragePublishedFileVisibility Visibility { get; init; } = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted;
        public string Changelog { get; init; } = string.Empty;
    }
}
