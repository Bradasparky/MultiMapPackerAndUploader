using Steamworks;

namespace MapPackerAndUploader
{
    class BspUploader
    {
        private static bool CallbackFinished = false;
        private static uint UGCItemsPage = 0;
        private static int Uploaded = 0;
        private static UGCUpdateHandle_t UploadHandle;
        private static UGCQueryHandle_t QueryHandle;
        private static SteamAPICall_t SteamAPICall;
        private static CallResult<SubmitItemUpdateResult_t> UploadCallback;
        private static CallResult<SteamUGCQueryCompleted_t> QueryCallback;
        private static IList<SteamUGCDetails_t> UGCDetails = [];
        private static IList<BspOptions> UploadList = [];

        public static void UploadMapsToWorkshop(ConfigSettings settings, IList<BspOptions> workshopMaps)
        {
            Console.ForegroundColor = ConsoleColor.White;
            InitSteam();
            Console.ForegroundColor = ConsoleColor.Gray;
            Logger.LogLine("");

            var existingIds = new Dictionary<ulong, string>();

            for (int i = 0; i < workshopMaps.Count; i++)
            {
                if (workshopMaps[i].Workshop.ID == 0)
                {
                    Logger.LogLine($"WARNING: '{workshopMaps[i].Name}' is set to upload with a workshop id of 0. Discarding from upload list", ConsoleColor.Yellow);
                    workshopMaps.RemoveAt(i--);
                    continue;
                }

                if (existingIds.TryGetValue(workshopMaps[i].Workshop.ID, out string? value))
                {
                    Logger.LogLine($"WARNING: '{workshopMaps[i].Name}'s workshop id is a duplicate of {value}'s. Discarding from upload list", ConsoleColor.Yellow);
                    workshopMaps.RemoveAt(i--);
                    continue;
                }

                existingIds.Add(workshopMaps[i].Workshop.ID, workshopMaps[i].Name);
            }

            if (workshopMaps.Count == 0)
            {
                Logger.LogLine("\nNo maps were found to be uploaded to the workshop", ConsoleColor.Yellow);
                Program.Exit(true);
            }

            QueryCallback = CallResult<SteamUGCQueryCompleted_t>.Create(OnUGCQueryCompleted);
            FindUGCMaps(ref workshopMaps);

            UploadCallback = CallResult<SubmitItemUpdateResult_t>.Create(OnUGCUploadCompleted);
            UploadUGCMaps(workshopMaps);

            SteamAPI.Shutdown();
        }      

        public static void FindUGCMaps(ref IList<BspOptions> workshopMaps)
        {
            Logger.LogLine("\nFinding owned workshop maps...", ConsoleColor.White);
            EnumerateAll();

            while (true)
            {
                SteamAPI.RunCallbacks();

                if (CallbackFinished)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            if (UGCDetails.Count == 0)
            {
                Logger.LogLine("No workshop maps were found", ConsoleColor.Yellow);
                Program.Exit(false);
            }

            Logger.LogLine($"Found {UGCDetails.Count} workshop maps!", ConsoleColor.Yellow);
            Logger.LogLine("Verifying that maps marked to be uploaded exist on the workshop...\n", ConsoleColor.Yellow);

            // Store all UGC ids in a map for fast checking between config and ugc ids
            var UGCFileIds = new Dictionary<PublishedFileId_t, SteamUGCDetails_t>();

            foreach (var details in UGCDetails)
            {
                if (details.m_eFileType == EWorkshopFileType.k_EWorkshopFileTypeCommunity)
                {
                    UGCFileIds.Add(details.m_nPublishedFileId, details);
                }
            }

            var confirmedMaps = new List<BspOptions>();

            foreach (var map in workshopMaps)
            {
                if (UGCFileIds.ContainsKey((PublishedFileId_t)map.Workshop.ID))
                {
                    map.Workshop.Details = UGCFileIds[(PublishedFileId_t)map.Workshop.ID];
                    Logger.LogLine($"Found {map.Name} = ({map.Workshop.ID}) - {map.Workshop.Details.m_rgchTitle}", ConsoleColor.Cyan);
                    confirmedMaps.Add(map);
                }
            }

            if (confirmedMaps.Count < workshopMaps.Count)
            {
                if (confirmedMaps.Count == 0)
                {
                    Logger.LogLine("\nNo maps marked to be uploaded were found on the workshop\n", ConsoleColor.Yellow);
                    Program.Exit(false);
                }

                Logger.LogLine("Would you still like to upload the ones that were found?\n");
                Logger.Log("Enter \"y\" to continue the upload process, enter anything else to abort: ");
                var input = Console.ReadLine();

                if (string.IsNullOrEmpty(input) || !input.Equals("y"))
                {
                    Program.Exit(false);
                }

                Logger.LogLine("");
            }

            workshopMaps = confirmedMaps;
        }

        private static void EnumerateAll()
        {
            CallbackFinished = false;
            UGCItemsPage = 1;
            Enumerate(UGCItemsPage);
        }

        private static void Enumerate(uint page)
        {
            QueryHandle = SteamUGC.CreateQueryUserUGCRequest(
                SteamUser.GetSteamID().GetAccountID(),
                EUserUGCList.k_EUserUGCList_Published,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
                EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderDesc,
                SteamUtils.GetAppID(), SteamUtils.GetAppID(), page);

            if (QueryHandle == UGCQueryHandle_t.Invalid)
            {
                Logger.LogLine("Failed to fetch owned maps from the workshop", ConsoleColor.Red);
                Program.Exit(false);
            }

            SteamAPICall = SteamUGC.SendQueryUGCRequest(QueryHandle);

            if (SteamAPICall == SteamAPICall_t.Invalid)
            {
                Logger.LogLine("Failed to send steam workshop ugc query", ConsoleColor.Red);
                Program.Exit(false);
            }

            QueryCallback.Set(SteamAPICall);
        }

        public static void OnUGCQueryCompleted(SteamUGCQueryCompleted_t result, bool bIOFailure)
        {
            if (bIOFailure || result.m_eResult != EResult.k_EResultOK)
            {
                SteamUGC.ReleaseQueryUGCRequest(QueryHandle);
                Logger.LogLine($"Failed to query workshop maps. Result: {result.m_eResult}");
                Program.Exit(false);
            }

            uint itemCount = result.m_unNumResultsReturned;

            for (uint i = 0; i < itemCount; i++)
            {
                SteamUGC.GetQueryUGCResult(result.m_handle, i, out SteamUGCDetails_t details);
                UGCDetails.Add(details);
            }

            uint matchingResults = result.m_unTotalMatchingResults;
            SteamUGC.ReleaseQueryUGCRequest(QueryHandle);

            if (itemCount == 0 || UGCDetails.Count >= matchingResults) 
            {
                CallbackFinished = true;
            }
            else
            {
                Enumerate(++UGCItemsPage);
            }
        }

        public static void UploadUGCMaps(IList<BspOptions> workshopMaps)
        {
            Logger.LogLine("\n> The next step will upload all maps found from the previous step to the workshop.", ConsoleColor.White);
            Logger.LogLine("Before proceeding, ensure you have reviewed the packed bsps in the output path using GCFScape or VPKEdit.", ConsoleColor.White);
            Logger.LogLine("Copy-paste any bsps from the output path to your TF2 maps folder and check in-game to ensure they work as expected.\n", ConsoleColor.White);

            Logger.LogLine("NOTE:", ConsoleColor.Yellow);
            Logger.LogLine("TF2 won't launch if it's not currently open because Steam believes it's already running.", ConsoleColor.Yellow);
            Logger.LogLine("As a workaround, you can launch tf_win64.exe manually via adding it as a non-steam game,", ConsoleColor.Yellow);
            Logger.LogLine("or by creating a .bat file with the parameters tf_win64.exe -game tf -steam -insecure\n", ConsoleColor.Yellow);

            Logger.Log("Enter 'y' to start the upload process, enter anything else to abort: ", ConsoleColor.White);

            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input) || !input.Equals("y"))
            {
                Program.Exit(true);
            }

            Logger.LogLine("");
            Logger.LogLine("\n> Uploading modified maps to the workshop...\n", ConsoleColor.White);

            UploadList = workshopMaps;
            UploadAll();

            while (true)
            {
                SteamAPI.RunCallbacks();

                if (CallbackFinished)
                {
                    break;
                }

                Thread.Sleep(500);
            }
        }

        public static void UploadAll()
        {
            CallbackFinished = false;
            Upload(UploadList[0].Workshop.Details.m_nPublishedFileId);
        }

        public static void Upload(PublishedFileId_t id)
        {
            Logger.Log($"Uploading {UploadList[Uploaded].Name} ({id})...", ConsoleColor.Yellow);

            if (!File.Exists(UploadList[0].AbsOutputPath))
            {
                Logger.LogLine($"The file at '{UploadList[Uploaded].AbsOutputPath}' no longer exists. Was the file deleted?", ConsoleColor.Red);
                Program.Exit(false);
            }

            UploadHandle = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), id);

            if (UploadHandle == UGCUpdateHandle_t.Invalid)
            {
                Logger.LogLine($"Failed to begin update for {UploadList[Uploaded].Name} ({id})", ConsoleColor.Red);
                Program.Exit(false);
            }

            if (!SteamUGC.SetItemContent(UploadHandle, UploadList[Uploaded].AbsOutputPath))
            {
                Logger.LogLine($"Failed to set the map to upload from the output path for {UploadList[Uploaded].Name} ({id})", ConsoleColor.Red);
                Program.Exit(false);
            }

            if (!SteamUGC.SetItemVisibility(UploadHandle, UploadList[Uploaded].Workshop.Visibility))
            {
                Logger.LogLine($"Failed to set the workshop visibility for {UploadList[Uploaded].Name} ({id})", ConsoleColor.Red);
                Program.Exit(false);
            }

            SteamAPICall = SteamUGC.SubmitItemUpdate(UploadHandle, UploadList[Uploaded].Workshop.Changelog);

            if (SteamAPICall == SteamAPICall_t.Invalid)
            {
                Logger.LogLine($"Failed to submit an upload request for {UploadList[Uploaded].Name} ({id})", ConsoleColor.Red);
                Program.Exit(false);
            }

            UploadCallback.Set(SteamAPICall);

            OnUGCUploadCompleted(new SubmitItemUpdateResult_t()
            {
                m_eResult = EResult.k_EResultOK,
                m_bUserNeedsToAcceptWorkshopLegalAgreement = false,
                m_nPublishedFileId = (PublishedFileId_t)3453602691
            }, false);
        }

        public static void OnUGCUploadCompleted(SubmitItemUpdateResult_t result, bool bIOFailure)
        {
            if (result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            {
                Logger.LogLine($"Failed to upload {UploadList[Uploaded].Name} ({UploadList[Uploaded].Workshop.ID}). User needs to agree to the workshop legal agreement", ConsoleColor.Red);
                Program.Exit(false);
            }

            if (bIOFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Logger.LogLine($"Failed to upload {UploadList[Uploaded].Name} ({UploadList[Uploaded].Workshop.ID}). Result: {result.m_eResult}", ConsoleColor.Red);
                Program.Exit(false);
            }

            Logger.LogLine($"\rSuccessfully uploaded {UploadList[Uploaded].Name} ({UploadList[Uploaded].Workshop.ID})", ConsoleColor.Cyan);

            if (++Uploaded >= UploadList.Count)
            {
                CallbackFinished = true;
            }
            else
            {
                for (int i = 5; i > 0; i--)
                {
                    Logger.Log($"Waiting a moment to avoid tripping spam filters ({i})...\r", ConsoleColor.White);
                    Thread.Sleep(1000);
                }

                Logger.Log("                                                                                                    \r");
                Upload(UploadList[Uploaded].Workshop.Details.m_nPublishedFileId);
            }
        }

        public static void InitSteam()
        {
            try
            {
                if (!SteamAPI.Init())
                {
                    Logger.LogLine("Failed to initialize the Steam API", ConsoleColor.Red);
                    Program.Exit(false);
                }
            }
            catch (DllNotFoundException ex)
            {
                Logger.LogLine(ex.Message);
                Program.Exit(false);
            }

            if (!SteamUser.BLoggedOn())
            {
                Logger.LogLine("Failed to connect to Steam. Are you logged in?", ConsoleColor.Red);
                Program.Exit(false);
            }

            if (!Packsize.Test())
            {
                Logger.LogLine("You're using the wrong Steamworks.NET Assembly for this platform");
                Program.Exit(false);
            }

            if (!DllCheck.Test())
            {
                Console.WriteLine("You're using the wrong dlls for this platform");
                Program.Exit(false);
            }
        }
    }
}
