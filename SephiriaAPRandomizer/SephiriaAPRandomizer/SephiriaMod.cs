using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using MelonLoader;
using SephiriaAPRandomizer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

[assembly: MelonInfo(
    typeof(SephiriaAPRandomizer.SephiriaMod),
    "Sephiria AP Randomizer",
    "0.2.0",
    "TheSilly"
)]

namespace SephiriaAPRandomizer
{
    public class SephiriaMod : MelonMod
    {
        private static object session;

        static SephiriaMod()
        {
            CosturaUtility.Initialize();
        }

        public static int GoalChapter { get; private set; }
        public static int RequiredChapterClears { get; private set; }
        public static bool UniqueWeaponsRequired { get; private set; }
        public static int CompletedChapterClears { get; private set; }
        public static bool Chapter2EndingReached { get; set; }
        public static bool ArchipelagoGoalPending { get; set; }

        private static bool hardModeSyncPending;

        public const string ArchipelagoShrineMarker = "__SEPHIRIA_ARCHIPELAGO_SHRINE__";
        public const string ArchipelagoShrineUsedMarker = "__SEPHIRIA_ARCHIPELAGO_SHRINE_USED__";
        private const string ArchipelagoShrineSourcePrefix = "__SEPHIRIA_AP_SHRINE_SOURCE_";
        private const string ArchipelagoShrineUsedSavePrefix = "ArchipelagoShrineUsed_";
        private const string ArchipelagoShrineLocationPrefix = "Archipelago Shrine Check ";
        private const string ArchipelagoSephiriteEligibleCountKey = "ArchipelagoSephiriteEligibleCount";
        private const string ArchipelagoSephiriteAssignmentPrefix = "ArchipelagoSephiriteAssignment_";
        private static readonly Dictionary<int, string> archipelagoSephiriteRuntimeIdentities = new Dictionary<int, string>();
        private static readonly System.Random archipelagoShrineCheckRandom = new System.Random();
        private static int hiddenRoomRewardAttemptsThisRun;
        private static UnityEngine.Sprite archipelagoShrineSprite;

        private static bool wasArchipelagoConnected;
        private static bool disconnectMessageShown;
        public static bool IsArchipelagoConnected
        {
            get
            {
                ArchipelagoSession apSession =
                    session as ArchipelagoSession;

                return apSession != null &&
                       apSession.Socket.Connected;
            }
        }

        public static void ResetArchipelagoShrineRunState()
        {
            hiddenRoomRewardAttemptsThisRun = 0;
        }

        public static bool ShouldSpawnArchipelagoShrine(int randomId)
        {
            if (!IsArchipelagoSave)
                return false;

            if (!HasUncheckedArchipelagoShrineChecks())
                return false;

            hiddenRoomRewardAttemptsThisRun++;

            if (hiddenRoomRewardAttemptsThisRun >= 3)
            {
                MelonLogger.Msg(
                    "[AP Shrine] Third hidden-room reward - " +
                    "forcing Archipelago Shrine."
                );

                return true;
            }

            System.Random apRandom = new System.Random(unchecked(randomId ^ 0x5A17C3));

            bool spawnArchipelagoShrine = apRandom.Next(0, 2) == 0;

            MelonLogger.Msg(
                $"[AP Shrine] Hidden-room reward " +
                $"#{hiddenRoomRewardAttemptsThisRun}: " +
                $"{(spawnArchipelagoShrine ? "AP Shrine" : "normal reward")}."
            );

            return spawnArchipelagoShrine;
        }

        public static void MarkArchipelagoShrineSpawned()
        {
            hiddenRoomRewardAttemptsThisRun = 0;
        }

        public static bool IsArchipelagoShrine(DiceSpawner shrine)
        {
            return shrine != null && shrine.servedGuidHashes.Contains(ArchipelagoShrineMarker);
        }

        public static int GetArchipelagoShrineCheckCount()
        {
            switch (GoalChapter)
            {
                case 1:
                    return 15;

                case 2:
                    return 20;

                case 3:
                case 4:
                case 5:
                    return 25;

                case 6:
                    return 30;

                default:
                    return 0;
            }
        }

        private static List<long>
    GetMissingArchipelagoShrineChecks(
        ArchipelagoSession apSession)
        {
            return apSession
                .Locations
                .AllMissingLocations
                .Where(locationId =>
                {
                    string locationName =
                        apSession.Locations.GetLocationNameFromId(
                            locationId,
                            "Sephiria"
                        );

                    return !string.IsNullOrEmpty(locationName) &&
                           locationName.StartsWith(
                               ArchipelagoShrineLocationPrefix,
                               StringComparison.Ordinal
                           );
                })
                .ToList();
        }

        public static bool
            HasUncheckedArchipelagoShrineChecks()
        {
            if (!IsArchipelagoSave)
                return false;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return false;
            }

            return GetMissingArchipelagoShrineChecks(
                apSession
            ).Count > 0;
        }

        public static bool
            TryCompleteRandomArchipelagoShrineCheck(
                out string locationName)
        {
            locationName = null;

            if (!IsArchipelagoSave)
                return false;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    "[AP Shrine] Cannot check location: " +
                    "Archipelago is not connected."
                );

                return false;
            }

            List<long> missingShrineChecks =
                GetMissingArchipelagoShrineChecks(
                    apSession
                );

            if (missingShrineChecks.Count == 0)
            {
                MelonLogger.Msg(
                    "[AP Shrine] No unchecked shrine locations remain."
                );

                return false;
            }

            int randomIndex =
                archipelagoShrineCheckRandom.Next(
                    0,
                    missingShrineChecks.Count
                );

            long locationId =
                missingShrineChecks[randomIndex];

            locationName =
                apSession.Locations.GetLocationNameFromId(
                    locationId,
                    "Sephiria"
                );

            try
            {
                apSession.Locations.CompleteLocationChecks(
                    locationId
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    "[AP Shrine] Failed to send location check: " +
                    ex.Message
                );

                locationName = null;

                return false;
            }

            MelonLogger.Msg(
                $"[AP Shrine] Checked {locationName}."
            );

            return true;
        }

        private static UnityEngine.Sprite GetArchipelagoShrineSprite(UnityEngine.Sprite originalDiceSprite)
        {
            if (archipelagoShrineSprite != null)
                return archipelagoShrineSprite;

            if (originalDiceSprite == null)
                return null;

            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                using (Stream stream =
                    assembly.GetManifestResourceStream(
                        "SephiriaAPRandomizer.Resources.ap_shrine_icon.png"
                    ))
                {
                    if (stream == null)
                    {
                        MelonLogger.Warning(
                            "[AP Shrine] Could not find embedded logo."
                        );

                        foreach (string resourceName in
                            assembly.GetManifestResourceNames())
                        {
                            MelonLogger.Msg(
                                "[AP Shrine Resource] " +
                                resourceName
                            );
                        }

                        return null;
                    }

                    using (System.Drawing.Bitmap bitmap =
                        new System.Drawing.Bitmap(stream))
                    {
                        UnityEngine.Texture2D texture =
                            new UnityEngine.Texture2D(
                                bitmap.Width,
                                bitmap.Height,
                                UnityEngine.TextureFormat.RGBA32,
                                false
                            );

                        UnityEngine.Color32[] pixels =
                            new UnityEngine.Color32[
                                bitmap.Width * bitmap.Height
                            ];

                        for (int y = 0;
                             y < bitmap.Height;
                             y++)
                        {
                            for (int x = 0;
                                 x < bitmap.Width;
                                 x++)
                            {
                                System.Drawing.Color pixel =
                                    bitmap.GetPixel(x, y);

                                int flippedY =
                                    bitmap.Height - 1 - y;

                                pixels[
                                    flippedY *
                                    bitmap.Width +
                                    x
                                ] =
                                    new UnityEngine.Color32(
                                        pixel.R,
                                        pixel.G,
                                        pixel.B,
                                        pixel.A
                                    );
                            }
                        }

                        texture.SetPixels32(pixels);
                        texture.Apply();

                        texture.filterMode =
                            UnityEngine.FilterMode.Point;

                        texture.wrapMode =
                            UnityEngine.TextureWrapMode.Clamp;

                        float originalWorldWidth =
                            originalDiceSprite.rect.width /
                            originalDiceSprite.pixelsPerUnit;

                        float originalWorldHeight =
                            originalDiceSprite.rect.height /
                            originalDiceSprite.pixelsPerUnit;

                        float replacementPixelsPerUnit =
                            UnityEngine.Mathf.Max(
                                texture.width /
                                    originalWorldWidth,

                                texture.height /
                                    originalWorldHeight
                            );

                        archipelagoShrineSprite =
                            UnityEngine.Sprite.Create(
                                texture,
                                new UnityEngine.Rect(
                                    0,
                                    0,
                                    texture.width,
                                    texture.height
                                ),
                                new UnityEngine.Vector2(
                                    0.5f,
                                    0.5f
                                ),
                                replacementPixelsPerUnit
                            );

                        archipelagoShrineSprite.name =
                            "ArchipelagoShrineLogo";

                        MelonLogger.Msg(
                            $"[AP Shrine] Loaded logo: " +
                            $"{texture.width}x" +
                            $"{texture.height}"
                        );

                        return archipelagoShrineSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    "[AP Shrine] Logo load failed: " +
                    ex
                );

                return null;
            }
        }

        public static void ApplyArchipelagoShrineVisual(
            DiceSpawner shrine)
        {
            if (shrine == null ||
                shrine.diceRenderer == null ||
                shrine.diceRenderer.sprite == null)
            {
                return;
            }

            UnityEngine.Sprite logo =
                GetArchipelagoShrineSprite(
                    shrine.diceRenderer.sprite
                );

            if (logo == null)
                return;

            shrine.diceRenderer.sprite = logo;

            MelonLogger.Msg(
                "[AP Shrine] Applied Archipelago logo."
            );
        }

        public static string GetArchipelagoShrineSourceMarker(
            int sourceRandomId)
        {
            return
                ArchipelagoShrineSourcePrefix +
                sourceRandomId;
        }

        public static bool TryGetArchipelagoShrineSourceId(
            DiceSpawner shrine,
            out int sourceRandomId)
        {
            sourceRandomId = 0;

            if (shrine == null)
                return false;

            foreach (string marker in shrine.servedGuidHashes)
            {
                if (!marker.StartsWith(
                        ArchipelagoShrineSourcePrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string idText = marker.Substring(
                    ArchipelagoShrineSourcePrefix.Length
                );

                return int.TryParse(
                    idText,
                    out sourceRandomId
                );
            }

            return false;
        }

        public static bool WasArchipelagoShrineUsed(
            int sourceRandomId)
        {
            if (SaveManager.CurrentRun == null)
                return false;

            return SaveManager.CurrentRun.GetBool(
                ArchipelagoShrineUsedSavePrefix +
                sourceRandomId,
                false
            );
        }

        public static void MarkArchipelagoShrineUsed(
            int sourceRandomId)
        {
            if (SaveManager.CurrentRun == null)
                return;

            SaveManager.CurrentRun.SetBool(
                ArchipelagoShrineUsedSavePrefix +
                sourceRandomId,
                true
            );

            SaveManager.Save(
                saveCurrent: false,
                saveCurrentRun: true
            );

            MelonLogger.Msg(
                $"[AP Shrine] Persisted used shrine " +
                $"RandomID={sourceRandomId}."
            );
        }

        public static bool IsEligibleArchipelagoSephirite(
            Sephirite sephirite)
        {
            if (sephirite == null)
                return false;

            return
                sephirite.type == Sephirite.Type.NORMAL ||
                sephirite.type == Sephirite.Type.CHARM ||
                sephirite.type == Sephirite.Type.TABLET;
        }

        private static string GetArchipelagoSephiritePoolSaveToken(
            string poolName)
        {
            return poolName
                .Replace(" ", "")
                .Replace("'", "");
        }


        private static bool TryGetArchipelagoSephiriteIdentity(
            Sephirite sephirite,
            GameObject actor,
            out string identity)
        {
            identity = null;

            if (sephirite == null)
                return false;

            if (actor == null)
                return false;

            int runtimeInstanceId =
                sephirite.GetInstanceID();

            // If this exact runtime object has already been seen,
            // keep using its original identity.
            //
            // This is important because rerolling a Sephirite
            // changes CurrentSeed by +10000.
            if (archipelagoSephiriteRuntimeIdentities.TryGetValue(
                    runtimeInstanceId,
                    out identity))
            {
                return true;
            }

            int stageProgress;
            string poolName;

            if (!TryGetArchipelagoSephiritePool(
                    actor,
                    out stageProgress,
                    out poolName))
            {
                return false;
            }

            string poolToken =
                GetArchipelagoSephiritePoolSaveToken(
                    poolName
                );

            identity =
                poolToken + "_" +
                sephirite.type + "_" +
                sephirite.CurrentSeed;

            archipelagoSephiriteRuntimeIdentities[
                runtimeInstanceId
            ] = identity;

            return true;
        }


        public static bool TryGetOrAssignArchipelagoSephirite(
            Sephirite sephirite,
            GameObject actor,
            out int eligibleNumber,
            out bool wasAlreadyAssigned)
        {
            eligibleNumber = 0;
            wasAlreadyAssigned = false;

            if (!IsArchipelagoSave)
                return false;

            if (sephirite == null)
                return false;

            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteIdentity(
                    sephirite,
                    actor,
                    out identity))
            {
                return false;
            }

            string assignmentKey =
                ArchipelagoSephiriteAssignmentPrefix +
                identity;

            // Has this exact Sephirite already received
            // its permanent place in the sequence?
            eligibleNumber =
                SaveManager.CurrentRun.GetInt(
                    assignmentKey,
                    0
                );

            if (eligibleNumber > 0)
            {
                wasAlreadyAssigned = true;
                return true;
            }

            // Truly new Sephirite. Advance the universal
            // alternating sequence exactly once.
            eligibleNumber =
                SaveManager.CurrentRun.GetInt(
                    ArchipelagoSephiriteEligibleCountKey,
                    0
                ) + 1;

            SaveManager.CurrentRun.SetInt(
                ArchipelagoSephiriteEligibleCountKey,
                eligibleNumber
            );

            // Save this exact Sephirite's permanent
            // sequence position.
            SaveManager.CurrentRun.SetInt(
                assignmentKey,
                eligibleNumber
            );

            SaveManager.Save(
                saveCurrent: false,
                saveCurrentRun: true
            );

            return true;
        }

        private static bool TryGetArchipelagoSephiritePool(
            GameObject actor,
            out int stageProgress,
            out string poolName)
        {
            stageProgress = -1;
            poolName = null;

            if (actor == null)
                return false;

            if (DungeonManager.Instance == null)
                return false;

            PlayerAvatar playerAvatar =
                actor.GetComponent<PlayerAvatar>();

            if (playerAvatar == null)
                return false;

            FloorData floorData;

            if (!DungeonManager.Instance
                    .generatedFloors
                    .TryGetValue(
                        playerAvatar.currentFloorGuid,
                        out floorData))
            {
                return false;
            }

            if (floorData == null)
                return false;

            if (!DungeonManager.Instance
                    .stageProgress
                    .TryGetValue(
                        floorData.stageName,
                        out stageProgress))
            {
                return false;
            }

            switch (stageProgress)
            {
                case 1:
                    poolName = "Mole Stronghold";
                    break;

                case 2:
                    poolName = "Woodlands";
                    break;

                case 3:
                    poolName = "Library";
                    break;

                case 4:
                    poolName = "Wasteland";
                    break;

                case 5:
                case 6:
                    poolName = "Rooted Cave";
                    break;

                case 7:
                    poolName = "Qliphoth's Temple";
                    break;

                default:
                    return false;
            }

            return true;
        }


        private static List<long>
            GetMissingArchipelagoSephiriteChecks(
                ArchipelagoSession apSession,
                string poolName)
        {
            string prefix =
                poolName + " Sephirite Item ";

            return apSession
                .Locations
                .AllMissingLocations
                .Where(locationId =>
                {
                    string locationName =
                        apSession.Locations
                            .GetLocationNameFromId(
                                locationId,
                                "Sephiria"
                            );

                    return
                        !string.IsNullOrEmpty(locationName) &&
                        locationName.StartsWith(
                            prefix,
                            StringComparison.Ordinal
                        );
                })
                .ToList();
        }


        public static bool
            TryGetArchipelagoSephiritePreviewChoices(
                GameObject actor,
                int normalChoiceCount,
                int seed,
                out int stageProgress,
                out string poolName,
                out List<string> locationNames)
        {
            stageProgress = -1;
            poolName = null;
            locationNames = new List<string>();

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return false;
            }

            if (!TryGetArchipelagoSephiritePool(
                    actor,
                    out stageProgress,
                    out poolName))
            {
                return false;
            }

            List<long> missingChecks =
                GetMissingArchipelagoSephiriteChecks(
                    apSession,
                    poolName
                );

            // This is valid. It means this floor's AP
            // Sephirite pool has already been exhausted.
            if (missingChecks.Count == 0)
                return true;

            int amountToShow =
                Math.Min(
                    normalChoiceCount,
                    missingChecks.Count
                );

            List<long> available =
                new List<long>(missingChecks);

            System.Random random =
                new System.Random(
                    unchecked(seed ^ 0x41A7B9)
                );

            for (int i = 0;
                 i < amountToShow;
                 i++)
            {
                int randomIndex =
                    random.Next(
                        0,
                        available.Count
                    );

                long locationId =
                    available[randomIndex];

                available.RemoveAt(randomIndex);

                string locationName =
                    apSession.Locations
                        .GetLocationNameFromId(
                            locationId,
                            "Sephiria"
                        );

                locationNames.Add(locationName);
            }

            return true;
        }

        // Hardmode Stuff
        private static readonly Dictionary<string, string>
            hardModeReductionItemsByShardKey = new Dictionary<string, string>
            {
                { "FEROCIOUSCLAWS", "Ferocious Claws Reduction" },
                { "TENACIOUSBODY", "Tenacious Body Reduction" },
                { "PLATEARMOR", "Protective Armor Reduction" },
                { "AGILECLAWS", "Agile Claws Reduction" },
                { "ENHANCEDSUPERARMOR", "Evil Energy Reduction" },
                { "BOSSPATTERN", "Combat Stance Reduction" },
                { "BLOODFESTIVAL", "Festival of Blood Reduction" },
                { "MIBO", "False Vanguard Reduction" },

                { "WEAKBOSSTABLET", "Dispossession Reduction" },
                { "DIZZINESS", "Dizziness Reduction" },
                { "REDUCELVUPHEAL", "Deterioration Reduction" },
                { "HOSTILITY", "Tension Reduction" },
                { "MINDBURDEN", "Heavy Heart Reduction" },

                { "DECLINE", "Decline Reduction" },
                { "HARDMERCHANT", "Severed Exchange Reduction" },
                { "TRAPDAMAGE", "Rebellion of Machines Reduction" },
                { "PROLIFERATE", "Proliferation Reduction" }
            };
        private static readonly Dictionary<string, int>
    hardModeMaxLevelsByShardKey =
        new Dictionary<string, int>
            {
                { "FEROCIOUSCLAWS", 7 },
                { "TENACIOUSBODY", 7 },
                { "PLATEARMOR", 3 },
                { "AGILECLAWS", 2 },
                { "ENHANCEDSUPERARMOR", 1 },
                { "BOSSPATTERN", 1 },
                { "BLOODFESTIVAL", 1 },
                { "MIBO", 1 },

                { "WEAKBOSSTABLET", 2 },
                { "DIZZINESS", 2 },
                { "REDUCELVUPHEAL", 4 },
                { "HOSTILITY", 1 },
                { "MINDBURDEN", 1 },

                { "DECLINE", 3 },
                { "HARDMERCHANT", 2 },
                { "TRAPDAMAGE", 2 },
                { "PROLIFERATE", 2 }
            };

        public static bool IsArchipelagoSave
        {
            get
            {
                return SaveManager.Current != null &&
                       SaveManager.Current.GetBool(
                           "ArchipelagoSave",
                           false
                       );
            }
        }

        public static void ResetArchipelagoRuntimeState()
        {
            GoalChapter = 0;
            RequiredChapterClears = 0;
            UniqueWeaponsRequired = false;
            CompletedChapterClears = 0;

            Chapter2EndingReached = false;
            ArchipelagoGoalPending = false;

            completedWeaponTypes.Clear();

            archipelagoSephiriteRuntimeIdentities.Clear();

            ResetArchipelagoShrineRunState();

            hardModeSyncPending = false;

            wasArchipelagoConnected = false;
            disconnectMessageShown = false;
        }

        public static HashSet<EWeaponType> completedWeaponTypes = new HashSet<EWeaponType>();

        public static async Task<bool> ConnectToArchipelago(
            string host,
            int port,
            string slotName,
            string password)
        {
            ArchipelagoSession apSession =
                ArchipelagoSessionFactory.CreateSession(host, port);

            session = apSession;

            apSession.Items.ItemReceived += helper =>
            {
                while (helper.Any())
                {
                    var item = helper.DequeueItem();

                    MelonLogger.Msg(
                        $"Received item: {item.ItemName} " +
                        $"from player {item.Player} " +
                        $"at location {item.LocationName} "
                    );
                }

                QueueHardModeSync();
            };

            MelonLogger.Msg("Connecting to Archipelago...");

            var result = apSession.TryConnectAndLogin(
                "Sephiria",
                slotName,
                ItemsHandlingFlags.AllItems,
                new Version(0, 6, 7),
                password: string.IsNullOrEmpty(password)
                    ? null
                    : password
            );

            if (result.Successful)
            {
                MelonLogger.Msg("Connected to Archipelago!");

                wasArchipelagoConnected = true;
                disconnectMessageShown = false;
                var loginSuccess = (LoginSuccessful)result;

                GoalChapter =
                    Convert.ToInt32(loginSuccess.SlotData["goal_chapter"]);

                RequiredChapterClears =
                    Convert.ToInt32(
                        loginSuccess.SlotData["required_chapter_clears"]
                    );

                UniqueWeaponsRequired =
                    Convert.ToBoolean(
                        loginSuccess.SlotData["unique_weapons_required"]
                    );

                apSession
                    .DataStorage["sephiria_completed_chapter_clears"]
                    .Initialize(0);

                apSession
                    .DataStorage["sephiria_completed_weapon_types"]
                    .Initialize(new List<string>());

                CompletedChapterClears =
                    await apSession
                        .DataStorage["sephiria_completed_chapter_clears"]
                        .GetAsync<int>();

                MelonLogger.Msg(
                    $"Loaded AP clear progress: " +
                    $"{CompletedChapterClears}/{RequiredChapterClears}"
                );

                var savedWeaponTypes =
                    await apSession
                        .DataStorage["sephiria_completed_weapon_types"]
                        .GetAsync<List<string>>();

                completedWeaponTypes.Clear();

                foreach (string weaponTypeName in savedWeaponTypes)
                {
                    if (Enum.TryParse(
                        weaponTypeName,
                        out EWeaponType weaponType))
                    {
                        completedWeaponTypes.Add(weaponType);
                    }
                }

                MelonLogger.Msg(
                    $"Loaded {completedWeaponTypes.Count} " +
                    $"previously used weapon types."
                );

                MelonLogger.Msg(
                    $"Goal settings: Chapter {GoalChapter}, " +
                    $"Clears {RequiredChapterClears}, " +
                    $"Unique Weapons {UniqueWeaponsRequired}"
                );

                QueueHardModeSync();

                return true;
            }

            MelonLogger.Msg("Failed to connect to Archipelago");

            session = null;

            return false;
        }

        public static async Task DisconnectFromArchipelago()
        {
            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            session = null;

            ResetArchipelagoRuntimeState();

            if (apSession == null)
                return;

            MelonLogger.Msg(
                "Disconnecting from Archipelago..."
            );

            await apSession.Socket.DisconnectAsync();

            MelonLogger.Msg(
                "Disconnected from Archipelago."
            );
        }

        public static void QueueHardModeSync()
        {
            if (!IsArchipelagoSave)
                return;

            hardModeSyncPending = true;
        }

        public override void OnUpdate()
        {
            if (IsArchipelagoSave)
            {
                bool connected =
                    IsArchipelagoConnected;

                if (wasArchipelagoConnected &&
                    !connected &&
                    !disconnectMessageShown)
                {
                    disconnectMessageShown = true;

                    MelonLogger.Warning(
                        "Archipelago connection lost."
                    );

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance
                            .GetElement<UI_SystemMessage>()
                            .Open(
                                "Archipelago connection lost. " +
                                "Return to the title screen to reconnect.",
                                15f
                            );
                    }
                }

                if (connected)
                {
                    disconnectMessageShown = false;
                }

                wasArchipelagoConnected = connected;
            }

            if (!hardModeSyncPending)
                return;

            hardModeSyncPending = false;

            ApplyArchipelagoHardModeState();
        }

        private static void ApplyArchipelagoHardModeState()
        {
            if (!IsArchipelagoSave)
                return;

            if (SaveManager.Current == null)
                return;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null)
                return;

            bool changed = false;

            foreach (KeyValuePair<string, int> shard
                in hardModeMaxLevelsByShardKey)
            {
                string shardKey = shard.Key;
                int shardMaxLevel = shard.Value;

                int reductionCount = 0;

                string reductionItemName;

                if (hardModeReductionItemsByShardKey.TryGetValue(
                        shardKey,
                        out reductionItemName))
                {
                    reductionCount =
                        apSession.Items.AllItemsReceived.Count(
                            item =>
                                item != null &&
                                item.ItemName == reductionItemName
                        );
                }

                int desiredLevel =
                    Math.Max(
                        0,
                        shardMaxLevel - reductionCount
                    );

                string saveKey =
                    "HardModeShard_" +
                    shardKey +
                    "_Level";

                int currentLevel =
                    SaveManager.Current.GetInt(
                        saveKey,
                        0
                    );

                if (currentLevel == desiredLevel)
                    continue;

                SaveManager.Current.SetInt(
                    saveKey,
                    desiredLevel
                );

                changed = true;

                MelonLogger.Msg(
                    $"AP Hard Mode: {shardKey} " +
                    $"{currentLevel} -> {desiredLevel} " +
                    $"({reductionCount} reductions)"
                );
            }

            if (changed)
            {
                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );
            }
        }

        public static void InitializeChapter1()
        {
            if (!IsArchipelagoSave)
                return;

            MelonLogger.Msg("Initializing Chapter 1 Archipelago state...");

            SaveManager.Current.SetBool(
                "DestinySwitch_PrologueClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_EnableTowntreePortal",
                true
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );
        }

        public static void InitializeChapter2()
        {
            if (!IsArchipelagoSave)
                return;

            MelonLogger.Msg("Initializing Chapter 2 Archipelago state...");

            SaveManager.Current.SetBool(
                "DestinySwitch_PrologueClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter1Clear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter1Complete",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_MainDungeonPortal",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_EnableTowntreePortal",
                true
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );
        }

        public static void InitializeChapter3()
        {
            if (!IsArchipelagoSave)
                return;

            MelonLogger.Msg("Initializing Chapter 3 Archipelago State");

            SaveManager.Current.SetBool("DestinySwitch_PrologueClear", true);

            SaveManager.Current.SetBool("DestinySwitch_Chapter2Clear", true);

            SaveManager.Current.SetBool("DestinySwitch_Chapter2Complete", true);

            SaveManager.Current.SetBool("DestinySwitch_Chapter3Begin", true);

            int chapter3ClearCount = SaveManager.Current.GetInt("Chapter3ClearCount", 0);

            if (chapter3ClearCount < 2)
            {
                SaveManager.Current.SetInt("Chapter3ClearCount", 2);
            }

            SaveManager.Current.SetBool("DestinySwitch_MainDungeonPortal", true);

            SaveManager.Current.SetBool("DestinySwitch_DeepCave_Hero_Meet_1", true);

            SaveManager.Current.SetBool("DestinySwitch_DeepCave_Hero_Meet_1_TreeTalk", true);

            SaveManager.Current.SetBool("DestinySwitch_EnableTowntreePortal", true);

            SaveManager.Save(saveCurrent: true, saveCurrentRun: false);
        }

        public static void InitializeChapter4()
        {
            if (!IsArchipelagoSave)
                return;

            MelonLogger.Msg("Initializing Chapter 4 Archipelago State");

            // Previous chapter progression
            SaveManager.Current.SetBool("DestinySwitch_PrologueClear", true);
            SaveManager.Current.SetBool("DestinySwitch_Chapter2Clear", true);
            SaveManager.Current.SetBool("DestinySwitch_Chapter2Complete", true);
            SaveManager.Current.SetBool("DestinySwitch_Chapter3Begin", true);

            // Chapter 3 must be fully completed
            int chapter3ClearCount =
                SaveManager.Current.GetInt("Chapter3ClearCount", 0);

            if (chapter3ClearCount < 3)
            {
                SaveManager.Current.SetInt("Chapter3ClearCount", 3);
            }

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_1",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_1_TreeTalk",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_2",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_2_TreeTalk",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_3",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_3_TreeTalk",
                true
            );

            // All three Chapter 4 mini-runs completed
            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_Accept",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_FoxClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_LionClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_MouseClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_AllComplete",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_EnableTowntreePortal",
                true
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );
        }

        public static void InitializeChapter5()
        {
            if (!IsArchipelagoSave)
                return;

            MelonLogger.Msg("Initializing Chapter 5 Archipelago State");

            // Previous chapter progression
            SaveManager.Current.SetBool(
                "DestinySwitch_PrologueClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter2Clear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter2Complete",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter3Begin",
                true
            );

            int chapter3ClearCount =
                SaveManager.Current.GetInt(
                    "Chapter3ClearCount",
                    0
                );

            if (chapter3ClearCount < 3)
            {
                SaveManager.Current.SetInt(
                    "Chapter3ClearCount",
                    3
                );
            }

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_1",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_1_TreeTalk",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_2",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_2_TreeTalk",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_3",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_DeepCave_Hero_Meet_3_TreeTalk",
                true
            );

            // Chapter 4 completed
            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_Accept",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_FoxClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_LionClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_MouseClear",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_FindDestiny_AllComplete",
                true
            );

            int chapter4ClearCount =
                SaveManager.Current.GetInt(
                    "Chapter4ClearCount",
                    0
                );

            if (chapter4ClearCount < 1)
            {
                SaveManager.Current.SetInt(
                    "Chapter4ClearCount",
                    1
                );
            }

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter4_Chapter4Cleared",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Talked_Rylie",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Peace1",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Talked_1",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Talked_2",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Talked_3",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Talked_4",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Talked_5",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_RylieBlessing",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_CampRest",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_RebuildStart",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Rebuild_Farm",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Rebuild_Building",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Rebuild_GatherResource",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_RebuildComplete",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_FoundAllCrack",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Escaped",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_StartMainRun",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_EnableTowntreePortal",
                true
            );

            if (CompletedChapterClears < RequiredChapterClears)
            {
                foreach (var node in QuestDatabase.GetAllMainQuestNodes())
                {
                    if (!node.isEndOfChapter)
                        continue;

                    if (!QuestDatabase.IsMainQuestCleared(
                            SaveManager.Current,
                            node))
                    {
                        continue;
                    }

                    SaveManager.Current.SetBool(
                        "ChapterClearScreen_" + node.nodeID,
                        true
                    );
                }
            }

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );
        }

        public static void InitializeChapter6()
        {
            if (!IsArchipelagoSave)
                return;

            MelonLogger.Msg("Initializing Chapter 6 Archipelago State");

            // Establish all chapter 5 progression first.
            InitializeChapter5();

            // Then use Sephiria's own Chapter 6 repair state.
            Chapter56SaveRepair.RestoreToChapter6Start();

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter5_Crack_ToriEscape",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_MainDungeonPortal",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter6_Start",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter6_Earthquake",
                true
            );

            SaveManager.Current.SetBool(
                "DestinySwitch_Chapter6_Alert",
                true
            );

            foreach (var node in QuestDatabase.GetAllMainQuestNodes())
            {
                // Only suppress chapters AP skipped.
                // Do NOT suppress real Chapter 6 completion screens.
                if (node.questChapterNum >= 6)
                    continue;

                if (!node.isEndOfChapter)
                    continue;

                if (!QuestDatabase.IsMainQuestCleared(
                        SaveManager.Current,
                        node))
                {
                    continue;
                }

                SaveManager.Current.SetBool(
                    "ChapterClearScreen_" + node.nodeID,
                    true
                );
            }

            SaveManager.Save(
               saveCurrent: true,
               saveCurrentRun: false
           );
        }

        public static bool RecordChapterClear(EWeaponType weaponType)
        {
            if (!IsArchipelagoSave)
                return false;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null || !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    "Cannot record AP clear: Archipelago session is not available."
                );

                return false;
            }

            if (UniqueWeaponsRequired)
            {
                if (completedWeaponTypes.Contains(weaponType))
                {
                    MelonLogger.Msg($"AP Chapter clear NOT counted: {weaponType} has already been used");
                    return false;
                }

                completedWeaponTypes.Add(weaponType);

                apSession.DataStorage["sephiria_completed_weapon_types"] =
                    completedWeaponTypes
                        .Select(type => type.ToString())
                        .ToList();
            }

            CompletedChapterClears++;

            apSession.DataStorage["sephiria_completed_chapter_clears"] = CompletedChapterClears;

            MelonLogger.Msg(
            $"AP Chapter clear recorded with {weaponType}" +
            $"{CompletedChapterClears}/{RequiredChapterClears}"
            );

            return CompletedChapterClears >= RequiredChapterClears;
        }

        public static void CompleteArchipelagoGoal()
        {
            if (!IsArchipelagoSave)
                return;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null || !apSession.Socket.Connected)
            {
                MelonLogger.Msg(
                    "Cannot complete AP goal: " +
                    "Archipelago session is not available."
                );

                return;
            }

            MelonLogger.Msg("Archipelago goal achieved!");

            apSession.SetGoalAchieved();
        }
    }
}

[HarmonyPatch(typeof(UI_TitleLobby), "CheckAndStart")]
public static class ArchipelagoSaveChoicePatch
{
    private static bool continueAfterChoice = false;

    private static bool reconnectingExistingSave = false;

    public static bool Prefix(UI_TitleLobby __instance)
    {
        if (continueAfterChoice)
        {
            continueAfterChoice = false;
            return true;
        }

        if (SaveManager.Current == null)
            return true;

        bool isArchipelagoSave =
            SaveManager.Current.GetBool(
                "ArchipelagoSave",
                false
            );

        if (isArchipelagoSave)
        {
            if (reconnectingExistingSave)
                return false;

            reconnectingExistingSave = true;

            _ = ConnectExistingArchipelagoSave(__instance);

            return false;
        }

        string playerName =
            SaveManager.Current.GetString("PlayerName", "")
                .Trim()
                .Trim('\u200b');

        // Existing save: don't show the choice.
        if (!string.IsNullOrWhiteSpace(playerName))
            return true;

        UIManager.Instance
            .GetElement<UI_MessageBox_YesNo>()
            .Open(
                "Create this save as an Archipelago save?",
                delegate
                {
                    OpenArchipelagoSetup(__instance);
                },
                delegate
                {
                    // No = Normal Sephiria save
                    SaveManager.Current.SetBool(
                        "ArchipelagoSave",
                        false
                    );

                    MelonLogger.Msg(
                        "[DEBUG] Created normal save."
                    );

                    continueAfterChoice = true;

                    AccessTools
                        .Method(
                            typeof(UI_TitleLobby),
                            "CheckAndStart"
                        )
                        .Invoke(__instance, null);
                }
            );

        // Stop the original CheckAndStart for now.
        // It resumes after the player makes a choice.
        return false;
    }

    private static void OpenArchipelagoSetup(UI_TitleLobby titleLobby)
    {
        UI_MessageBox_InputYesNo popup =
            UIManager.Instance.GetElement<UI_MessageBox_InputYesNo>();

        TMPro.TMP_InputField portInput = null;
        TMPro.TMP_InputField slotInput = null;
        TMPro.TMP_InputField passwordInput = null;

        popup.Open(
             "Archipelago Setup",
             async delegate (string host)
             {
                 string hostValue = host.Trim();
                 string portText = portInput.text.Trim();
                 string slotName = slotInput.text.Trim();
                 string password = passwordInput.text;

                 int port;

                 if (!int.TryParse(portText, out port) ||
                     port < 1 ||
                     port > 65535 ||
                     string.IsNullOrWhiteSpace(slotName))
                 {
                     UIManager.Instance
                         .GetElement<UI_MessageBoxHolder>()
                         .OpenYes(
                             "Invalid Archipelago settings.\n\n" +
                             "Enter a valid port and slot name.",
                             delegate
                             {
                                 MelonCoroutines.Start(
                                     ReopenArchipelagoSetupNextFrame(
                                         titleLobby
                                     )
                                 );
                             }
                         );

                     return;
                 }

                 bool connected =
                     await SephiriaMod.ConnectToArchipelago(
                         hostValue,
                         port,
                         slotName,
                         password
                     );

                 if (!connected)
                 {
                     MelonLogger.Warning(
                         "Could not connect to Archipelago. " +
                         "Save was not configured."
                     );

                     UIManager.Instance
                         .GetElement<UI_MessageBoxHolder>()
                         .OpenYes(
                             "Failed to connect to Archipelago.\n\n" +
                             "Check the server address, port, " +
                             "slot name, and password, then try again.",
                             delegate
                             {
                                 MelonCoroutines.Start(
                                     ReopenArchipelagoSetupNextFrame(
                                         titleLobby
                                     )
                                 );
                             }
                         );

                     return;
                 }

                 SaveManager.Current.SetBool(
                     "ArchipelagoSave",
                     true
                 );

                 SaveManager.Current.SetString(
                     "ArchipelagoHost",
                     hostValue
                 );

                 SaveManager.Current.SetInt(
                     "ArchipelagoPort",
                     port
                 );

                 SaveManager.Current.SetString(
                     "ArchipelagoSlot",
                     slotName
                 );

                 SaveManager.Current.SetString(
                     "ArchipelagoPassword",
                     password
                 );

                 SaveManager.Current.SetString(
                     "PlayerName",
                     slotName
                 );

                 SaveManager.Current.SetBool(
                     "DestinySwitch_PrologueClear",
                     true
                 );

                 SaveManager.Save(
                     saveCurrent: true,
                     saveCurrentRun: false
                 );

                 SephiriaMod.QueueHardModeSync();

                 MelonLogger.Msg(
                     $"[DEBUG] AP save configured: " +
                     $"{hostValue}:{port}, Slot={slotName}"
                 );

                 continueAfterChoice = true;

                 AccessTools
                     .Method(
                         typeof(UI_TitleLobby),
                         "CheckAndStart"
                     )
                     .Invoke(titleLobby, null);
             },
             null,
             "localhost",
             "",
             allowEmpty: false,
             100
        );

        // Make the popup taller.
        UnityEngine.RectTransform popupRect =
            popup.GetComponent<UnityEngine.RectTransform>();

        UnityEngine.Vector2 originalPopupSize =
            popupRect.sizeDelta;

        popupRect.sizeDelta =
            new UnityEngine.Vector2(240f, 320f);

        // Original input becomes the Host field.
        UnityEngine.RectTransform hostRect =
            popup.input.GetComponent<UnityEngine.RectTransform>();

        UnityEngine.Vector2 originalHostPosition =
            hostRect.anchoredPosition;

        hostRect.anchoredPosition =
            new UnityEngine.Vector2(0f, 90f);

        // Clone the existing input for Port.
        UnityEngine.GameObject portObject =
            UnityEngine.Object.Instantiate(
                popup.input.gameObject,
                popup.transform
            );

        portInput =
            portObject.GetComponent<TMPro.TMP_InputField>();

        portInput.text = "38281";
        portInput.characterLimit = 5;

        UnityEngine.RectTransform portRect =
            portObject.GetComponent<UnityEngine.RectTransform>();

        portRect.anchoredPosition =
            new UnityEngine.Vector2(0f, 30f);

        // Clone the existing input for Slot.
        UnityEngine.GameObject slotObject =
            UnityEngine.Object.Instantiate(
                popup.input.gameObject,
                popup.transform
            );

        slotInput =
            slotObject.GetComponent<TMPro.TMP_InputField>();

        slotInput.text = "";
        slotInput.characterLimit = 30;

        UnityEngine.RectTransform slotRect =
            slotObject.GetComponent<UnityEngine.RectTransform>();

        slotRect.anchoredPosition =
            new UnityEngine.Vector2(0f, -30f);

        // Add labels by cloning Sephiria's existing popup text.
        TMPro.TextMeshProUGUI hostLabel =
            UnityEngine.Object.Instantiate(
                popup.text,
                popup.transform
            );

        hostLabel.text = "Server Address";

        UnityEngine.RectTransform hostLabelRect =
            hostLabel.GetComponent<UnityEngine.RectTransform>();

        hostLabelRect.anchorMin =
            new UnityEngine.Vector2(0.5f, 0.5f);
        hostLabelRect.anchorMax =
            new UnityEngine.Vector2(0.5f, 0.5f);
        hostLabelRect.pivot =
            new UnityEngine.Vector2(0.5f, 0.5f);
        hostLabelRect.sizeDelta =
            new UnityEngine.Vector2(180f, 18f);
        hostLabelRect.anchoredPosition =
            new UnityEngine.Vector2(0f, 106f);

        TMPro.TextMeshProUGUI portLabel =
            UnityEngine.Object.Instantiate(
                popup.text,
                popup.transform
            );

        portLabel.text = "Port";

        UnityEngine.RectTransform portLabelRect =
            portLabel.GetComponent<UnityEngine.RectTransform>();

        portLabelRect.anchorMin =
            new UnityEngine.Vector2(0.5f, 0.5f);
        portLabelRect.anchorMax =
            new UnityEngine.Vector2(0.5f, 0.5f);
        portLabelRect.pivot =
            new UnityEngine.Vector2(0.5f, 0.5f);
        portLabelRect.sizeDelta =
            new UnityEngine.Vector2(180f, 18f);
        portLabelRect.anchoredPosition =
            new UnityEngine.Vector2(0f, 46f);

        TMPro.TextMeshProUGUI slotLabel =
            UnityEngine.Object.Instantiate(
                popup.text,
                popup.transform
            );

        slotLabel.text = "Slot Name";

        UnityEngine.RectTransform slotLabelRect =
            slotLabel.GetComponent<UnityEngine.RectTransform>();

        slotLabelRect.anchorMin =
            new UnityEngine.Vector2(0.5f, 0.5f);
        slotLabelRect.anchorMax =
            new UnityEngine.Vector2(0.5f, 0.5f);
        slotLabelRect.pivot =
            new UnityEngine.Vector2(0.5f, 0.5f);
        slotLabelRect.sizeDelta =
            new UnityEngine.Vector2(180f, 18f);
        slotLabelRect.anchoredPosition =
            new UnityEngine.Vector2(0f, -14f);

        // Clone the existing input for Password.
        UnityEngine.GameObject passwordObject =
            UnityEngine.Object.Instantiate(
                popup.input.gameObject,
                popup.transform
            );

        passwordInput =
            passwordObject.GetComponent<TMPro.TMP_InputField>();

        passwordInput.text = "";
        passwordInput.characterLimit = 100;
        passwordInput.contentType =
            TMPro.TMP_InputField.ContentType.Password;

        UnityEngine.RectTransform passwordRect =
            passwordObject.GetComponent<UnityEngine.RectTransform>();

        passwordRect.anchoredPosition =
            new UnityEngine.Vector2(0f, -90f);


        // Password label
        TMPro.TextMeshProUGUI passwordLabel =
            UnityEngine.Object.Instantiate(
                popup.text,
                popup.transform
            );

        passwordLabel.text = "Password";

        UnityEngine.RectTransform passwordLabelRect =
            passwordLabel.GetComponent<UnityEngine.RectTransform>();

        passwordLabelRect.anchorMin =
            new UnityEngine.Vector2(0.5f, 0.5f);

        passwordLabelRect.anchorMax =
            new UnityEngine.Vector2(0.5f, 0.5f);

        passwordLabelRect.pivot =
            new UnityEngine.Vector2(0.5f, 0.5f);

        passwordLabelRect.sizeDelta =
            new UnityEngine.Vector2(180f, 18f);

        passwordLabelRect.anchoredPosition =
            new UnityEngine.Vector2(0f, -74f);

        ArchipelagoTabNavigation tabNavigation =
            popup.gameObject.AddComponent<ArchipelagoTabNavigation>();

        tabNavigation.SetOrder(
            popup.input,
            portInput,
            slotInput,
            passwordInput,
            popup.yesButton,
            popup.noButton
        );

        popup.onClosed += delegate
        {
            // Restore Sephiria's original popup layout.
            popupRect.sizeDelta = originalPopupSize;
            hostRect.anchoredPosition = originalHostPosition;

            // Remove everything we cloned for the AP setup popup.
            UnityEngine.Object.Destroy(portObject);
            UnityEngine.Object.Destroy(slotObject);
            UnityEngine.Object.Destroy(passwordObject);
            UnityEngine.Object.Destroy(tabNavigation);

            UnityEngine.Object.Destroy(hostLabel.gameObject);
            UnityEngine.Object.Destroy(portLabel.gameObject);
            UnityEngine.Object.Destroy(slotLabel.gameObject);
            UnityEngine.Object.Destroy(passwordLabel.gameObject);
        };
    }

    public class ArchipelagoTabNavigation : UnityEngine.MonoBehaviour
    {
        private UnityEngine.UI.Selectable[] order;

        public void SetOrder(
            TMPro.TMP_InputField host,
            TMPro.TMP_InputField port,
            TMPro.TMP_InputField slot,
            TMPro.TMP_InputField password,
            UnityEngine.UI.Button yes,
            UnityEngine.UI.Button no)
        {
            order = new UnityEngine.UI.Selectable[]
            {
            host,
            port,
            slot,
            password,
            yes,
            no
            };
        }

        private void Update()
        {
            if (order == null ||
                UnityEngine.InputSystem.Keyboard.current == null ||
                !UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
            {
                return;
            }

            bool backwards =
                UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed ||
                UnityEngine.InputSystem.Keyboard.current.rightShiftKey.isPressed;

            UnityEngine.GameObject selected =
                UnityEngine.EventSystems.EventSystem.current
                    .currentSelectedGameObject;

            int currentIndex = -1;

            for (int i = 0; i < order.Length; i++)
            {
                if (order[i].gameObject == selected)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex;

            if (currentIndex == -1)
            {
                nextIndex = backwards
                    ? order.Length - 1
                    : 0;
            }
            else
            {
                nextIndex = backwards
                    ? (currentIndex - 1 + order.Length) % order.Length
                    : (currentIndex + 1) % order.Length;
            }

            UnityEngine.UI.Selectable next =
                order[nextIndex];

            next.Select();

            TMPro.TMP_InputField input =
                next as TMPro.TMP_InputField;

            if (input != null)
            {
                input.ActivateInputField();
            }
        }
    }

    private static IEnumerator ReopenArchipelagoSetupNextFrame(
    UI_TitleLobby titleLobby)
    {
        yield return null;

        OpenArchipelagoSetup(titleLobby);
    }

    private static async Task ConnectExistingArchipelagoSave(
        UI_TitleLobby titleLobby)
    {
        string host =
            SaveManager.Current.GetString(
                "ArchipelagoHost",
                ""
            );

        int port =
            SaveManager.Current.GetInt(
                "ArchipelagoPort",
                0
            );

        string slotName =
            SaveManager.Current.GetString(
                "ArchipelagoSlot",
                ""
            );

        string password =
            SaveManager.Current.GetString(
                "ArchipelagoPassword",
                ""
            );

        MelonLogger.Msg(
            $"Reconnecting AP save: " +
            $"{host}:{port}, Slot={slotName}"
        );

        bool connected =
            await SephiriaMod.ConnectToArchipelago(
                host,
                port,
                slotName,
                password
            );

        if (!connected)
        {
            reconnectingExistingSave = false;

            UIManager.Instance
                .GetElement<UI_MessageBoxHolder>()
                .OpenYes(
                    "Failed to reconnect to Archipelago.\n\n" +
                    "Check that the Archipelago server is running " +
                    "and that this save's connection information is still valid.",
                    null
                );

            return;
        }

        reconnectingExistingSave = false;

        continueAfterChoice = true;

        AccessTools
            .Method(
                typeof(UI_TitleLobby),
                "CheckAndStart"
            )
            .Invoke(titleLobby, null);
    }
}

[HarmonyPatch(
    typeof(HorayNetworkManager),
    nameof(HorayNetworkManager.GoToTitleScene)
)]
public static class ArchipelagoDisconnectPatch
{
    public static void Prefix()
    {
        _ = SephiriaMod.DisconnectFromArchipelago();
    }
}

[HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.NewGame))]
public static class NewGamePatch
{
    public static void Prefix(ref int ___forceChapter)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        SephiriaMod.ResetArchipelagoShrineRunState();

        MelonLogger.Msg(
            $"[DEBUG] Current Race ID = " +
            $"{SaveManager.CurrentRun.GetInt("CurrentGame", -1)}"
        );

        DesertEndEventPatch.Reset();

        MelonLogger.Msg($"NewGame reached - AP Goal Chapter: {SephiriaMod.GoalChapter}");

        switch (SephiriaMod.GoalChapter)
        {
            case 1:
                SephiriaMod.InitializeChapter1();
                break;

            case 2:
                SephiriaMod.InitializeChapter2();
                ___forceChapter = 9;
                break;
            case 3:
                SephiriaMod.InitializeChapter3();
                ___forceChapter = 12;
                break;
            case 4:
                SephiriaMod.InitializeChapter4();

                if (SaveManager.Current.GetInt("Chapter4ClearCount", 0) == 0)
                {
                    ___forceChapter = 14;
                }
                else
                {
                    ___forceChapter = -1;
                }

                break;
            case 5:
                SephiriaMod.InitializeChapter5();
                ___forceChapter = 26;
                break;
            case 6:
                if (SephiriaMod.CompletedChapterClears <
                    SephiriaMod.RequiredChapterClears)
                {
                    SephiriaMod.InitializeChapter6();
                }

                ___forceChapter = -1;
                break;
        }
    }
}

[HarmonyPatch(typeof(UI_GameOverLabel), nameof(UI_GameOverLabel.OnOpened))]
public static class GameOverLabelPatch
{
    public static void Postfix(UI_GameOverLabel __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;
        
        if (SephiriaMod.ArchipelagoGoalPending)
        {
            MelonLogger.Msg(
                "Final Archipelago run has reached the post-run screen."
            );

            SephiriaMod.CompleteArchipelagoGoal();
            SephiriaMod.ArchipelagoGoalPending = false;
        }
    }
}

[HarmonyPatch(typeof(Desert_EndEvent), "set_NetworkcutScenePhase")]
public static class DesertEndEventPatch
{
    private static bool intercepted;

    public static void Reset()
    {
        intercepted = false;
    }

    public static bool Prefix(Desert_EndEvent __instance, int value)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaAPRandomizer.SephiriaMod.GoalChapter != 1)
        {
            return true;
        }

        if (value != 1)
        {
            return true;
        }

        if (!__instance.isServer)
        {
            return false;
        }

        if (intercepted)
        {
            return false;
        }

        intercepted = true;

        var weaponController = UnityEngine.Object.FindObjectOfType<WeaponControllerSimple>();

        if (weaponController != null && weaponController.currentWeapon != null)
        {
            bool goalCompleted = SephiriaMod.RecordChapterClear(weaponController.currentWeapon.weaponType);

            if (goalCompleted)
            {
                MelonLogger.Msg("AP Goal requirements completed - allowing normal Chapter 1 ending.");

                SephiriaMod.ArchipelagoGoalPending = true;

                return true;
            }
        }
        else
        {
            MelonLogger.Warning("Chapter cleared, but current weapon could not be determined");
        }

        MelonLogger.Msg(
            "Chapter 1 clear recorded - ending intercepted for another AP run"
        );

        foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
        {
            if ((bool)player)
            {
                player.PlayerAvatar.Die(5, null);
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(PlayerLocalDataStorage), nameof(PlayerLocalDataStorage.UpdateMainQuestProgress))]
public static class Chapter2TitlePatch
{
    private static bool shown;

    public static void Postfix(PlayerLocalDataStorage __instance)
    {
        if(!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 2 || shown)
        {
            return;
        }

        shown = true;
        __instance.ChangeChapter(2);
    }
}

[HarmonyPatch(
    typeof(PlayerLocalDataStorage),
    nameof(PlayerLocalDataStorage.UpdateMainQuestProgress)
)]
public static class Chapter3TitlePatch
{
    private static bool shown;

    public static void Postfix(PlayerLocalDataStorage __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 3 || shown)
        {
            return;
        }

        shown = true;
        __instance.ChangeChapter(3);
    }
}

[HarmonyPatch(typeof(PlayerLocalDataStorage), nameof(PlayerLocalDataStorage.UpdateMainQuestProgress))]
public static class Chapter4TitlePatch
{
    private static bool shown;

    public static void Postfix(PlayerLocalDataStorage __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 4 || shown)
        {
            return;
        }

        shown = true;
        __instance.ChangeChapter(4);
    }
}

[HarmonyPatch(typeof(RaceEntity), nameof(RaceEntity.GetStartingFloorName))]
public static class Chapter3StartingFloorPatch
{
    public static void Postfix(RaceEntity __instance, ref string __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 3)
            return;

        if (__instance.id != 12)
            return;

        bool meet2 =
            SwitchManager.GetDestinySwitch(
                "DeepCave_Hero_Meet_2",
                false
            );

        bool meet2Tree =
            SwitchManager.GetDestinySwitch(
                "DeepCave_Hero_Meet_2_TreeTalk",
                false
            );

        int clearCount =
            SaveManager.Current.GetInt(
                "Chapter3ClearCount",
                0
            );

        if (meet2 && meet2Tree && clearCount == 2)
        {
            __result = "TheRabbittown";
        }
    }
}

[HarmonyPatch(
    typeof(UnitAI_QBossAdv),
    "RpcChapter5ClearCutSceneStart"
)]
public static class Chapter5ClearPatch
{
    public static bool Prefix(UnitAI_QBossAdv __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaMod.GoalChapter != 5)
            return true;

        if (DungeonManager.Instance == null ||
            DungeonManager.Instance.raceId != 26)
        {
            return true;
        }

        if (!__instance.isServer)
            return true;

        MelonLogger.Msg(
            "[DEBUG] Chapter 5 Qliphoth defeated."
        );

        var weaponController =
            UnityEngine.Object.FindObjectOfType<WeaponControllerSimple>();

        if (weaponController == null ||
            weaponController.currentWeapon == null)
        {
            MelonLogger.Warning(
                "Chapter 5 cleared, but current weapon could not be determined."
            );

            return true;
        }

        bool goalCompleted =
            SephiriaMod.RecordChapterClear(
                weaponController.currentWeapon.weaponType
            );

        if (goalCompleted)
        {
            MelonLogger.Msg(
                "AP Goal requirements completed - allowing normal Chapter 5 ending."
            );

            SephiriaMod.ArchipelagoGoalPending = true;

            return true;
        }

        MelonLogger.Msg(
            "Chapter 5 AP clear recorded - delaying player death."
        );

        MelonCoroutines.Start(DelayedPlayerDeath());

        // Stop Sephiria from starting the normal Chapter 5 ending.
        return false;
    }

    private static IEnumerator DelayedPlayerDeath()
    {
        // Let Qliphoth's defeated state sit on screen briefly,
        // so it is obvious the player won the fight.
        yield return new UnityEngine.WaitForSeconds(1.5f);

        foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
        {
            if ((bool)player &&
                (bool)player.PlayerAvatar)
            {
                player.PlayerAvatar.Die(5, null);
            }
        }
    }
}

[HarmonyPatch(
    typeof(BossEnvironment_QQBoss),
    nameof(BossEnvironment_QQBoss.DefeatBossUI)
)]
public static class Chapter6ClearPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaMod.GoalChapter != 6)
            return true;

        if (DungeonManager.Instance == null ||
            DungeonManager.Instance.raceId != 28)
        {
            return true;
        }

        // Only the host should record the AP clear.
        if (!Mirror.NetworkServer.active)
            return true;

        var weaponController =
            UnityEngine.Object.FindObjectOfType<WeaponControllerSimple>();

        if (weaponController == null ||
            weaponController.currentWeapon == null)
        {
            MelonLogger.Warning(
                "Chapter 6 cleared, but current weapon could not be determined."
            );

            return true;
        }

        bool goalCompleted =
            SephiriaMod.RecordChapterClear(
                weaponController.currentWeapon.weaponType
            );

        if (goalCompleted)
        {
            MelonLogger.Msg(
                "AP Goal requirements completed - allowing normal Chapter 6 ending."
            );

            SephiriaMod.ArchipelagoGoalPending = true;

            // Allow Sephiria to set Chapter6_DefeatBoss
            // and continue the normal ending.
            return true;
        }

        MelonLogger.Msg(
            "Chapter 6 AP clear recorded - ending intercepted for another AP run."
        );

        foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
        {
            if ((bool)player &&
                (bool)player.PlayerAvatar)
            {
                player.PlayerAvatar.Die(5, null);
            }
        }

        // Do NOT let Sephiria set Chapter6_DefeatBoss.
        return false;
    }
}

[HarmonyPatch(typeof(Desert_Chapter2Event), nameof(Desert_Chapter2Event.OnStartServer))]
public static class Chapter2EndRoomPatch
{
    public static void Postfix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 2)
        {
            return;
        }

        SephiriaMod.Chapter2EndingReached = true;
    }
}

[HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.RpcGameOver))]
public static class Chapter2GameOverPatch
{
    public static bool Prefix(PlayerSpawner __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaMod.GoalChapter != 2 ||
            !SephiriaMod.Chapter2EndingReached)
        {
            return true;
        }

        SephiriaMod.Chapter2EndingReached = false;

        var weaponController =
            __instance.GetComponent<WeaponControllerSimple>();

        if (weaponController == null ||
            weaponController.currentWeapon == null)
        {
            MelonLogger.Warning(
                "Chapter 2 cleared, but current weapon could not be determined."
            );
            return true;
        }

        bool goalCompleted = SephiriaMod.RecordChapterClear(
            weaponController.currentWeapon.weaponType
        );

        if (goalCompleted)
        {
            SephiriaMod.ArchipelagoGoalPending = true;

            var notification =
                UIManager.Instance.GetElement<UI_MainQuestClearNoti>();

            AccessTools.Method(
                typeof(UI_MainQuestClearNoti),
                "SetMainQuestProgress"
            ).Invoke(notification, new object[] { 4 });
        }
        else
        {
            SwitchManager.SetDestinySwitch(
               "Chapter2Clear",
               false
           );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );

            __instance.PlayerAvatar.Die(5, null);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(DungeonStair_Chapter3End), "UserCode_RpcEnd")]
public static class Chapter3EndPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaMod.GoalChapter != 3)
            return true;

        var weaponController =
            UnityEngine.Object.FindObjectOfType<WeaponControllerSimple>();

        if (weaponController == null ||
            weaponController.currentWeapon == null)
        {
            MelonLogger.Warning(
                "Chapter 3 cleared, but current weapon could not be determined."
            );

            return true;
        }

        bool goalCompleted = SephiriaMod.RecordChapterClear(
            weaponController.currentWeapon.weaponType
        );

        if (goalCompleted)
        {
            MelonLogger.Msg(
                "AP Goal requirements completed - allowing normal Chapter 3 ending."
            );

            SephiriaMod.ArchipelagoGoalPending = true;
            return true;
        }

        MelonLogger.Msg(
            "AP Goal requirements not yet completed - treating Chapter 3 clear as a normal death."
        );

        foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
        {
            if ((bool)player)
                player.PlayerAvatar.Die(5, null);
        }

        return false;
    }
}

[HarmonyPatch(typeof(DungeonStair_Chapter3End), "UserCode_RpcEnd")]
public static class Chapter4EndPatch
{
    public static bool Prefix(DungeonStair_Chapter3End __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaMod.GoalChapter != 4)
            return true;

        if (__instance.clearCountBoolKey != "Chapter4ClearCount")
            return true;

        var weaponController =
            UnityEngine.Object.FindObjectOfType<WeaponControllerSimple>();

        if (weaponController == null ||
            weaponController.currentWeapon == null)
        {
            MelonLogger.Warning(
                "Chapter 4 cleared, but current weapon could not be determined."
            );

            return false;
        }

        bool goalCompleted =
            SephiriaMod.RecordChapterClear(
                weaponController.currentWeapon.weaponType
            );

        if (goalCompleted)
        {
            MelonLogger.Msg(
                "AP Goal requirements completed - allowing normal Chapter 4 ending."
            );

            SephiriaMod.ArchipelagoGoalPending = true;

            return true;
        }

        MelonLogger.Msg(
            "AP Goal requirements not yet completed - treating Chapter 4 clear as a normal death."
        );

        foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
        {
            if ((bool)player)
            {
                player.PlayerAvatar.Die(5, null);
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(UI_MainQuestClearNoti), "SetMainQuestProgress")]
public static class Chapter2CompleteNotificationPatch
{
    public static bool Prefix(int progress)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaMod.GoalChapter == 2 &&
            progress == 4 &&
            SephiriaMod.CompletedChapterClears <
            SephiriaMod.RequiredChapterClears)
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(
    typeof(PlayerLocalDataStorage),
    nameof(PlayerLocalDataStorage.ChangeChapter)
)]
public static class ChangeChapterBlockPatch
{
    public static bool Prefix(int chapter)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if ((SephiriaMod.GoalChapter == 4 ||
             SephiriaMod.GoalChapter == 5 ||
             SephiriaMod.GoalChapter == 6) &&
            SephiriaMod.CompletedChapterClears <
            SephiriaMod.RequiredChapterClears &&
            chapter != SephiriaMod.GoalChapter)
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(
    typeof(UI_HardModePanel),
    nameof(UI_HardModePanel.ChangeShardLevel)
)]
public static class HardModeLevelLockPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return false;
    }
}

[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ServerReceiveHardModeReward))]
public static class HardModeRewardBlockPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return false;
    }
}

[HarmonyPatch(typeof(HiddenRoomRewardSpawner), nameof(HiddenRoomRewardSpawner.OnStartServer))]
public static class ArchipelagoHiddenRoomRewardPatch
{
    public static bool Prefix(
        HiddenRoomRewardSpawner __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (!Mirror.NetworkServer.active)
            return true;

        if (SephiriaMod.WasArchipelagoShrineUsed(
            __instance.RandomID))
        {
            MelonLogger.Msg(
                $"[AP Shrine] RandomID={__instance.RandomID} " +
                "was already used earlier in this run. " +
                "Suppressing respawn."
            );

            return false;
        }

        if (!SephiriaMod.ShouldSpawnArchipelagoShrine(
                __instance.RandomID))
        {
            // Normal 50% result:
            return true;
        }

        PropEntity diceSpawnerProp =
            PropDatabase.FindPropById("DiceSpawner");

        if (diceSpawnerProp == null ||
            diceSpawnerProp.propPrefab == null)
        {
            MelonLogger.Warning(
                "[AP Shrine] DiceSpawner prop could not be found. " +
                "Falling back to normal hidden-room reward."
            );

            return true;
        }

        System.Random shrineRandom =
            new System.Random(
                unchecked(
                    __instance.RandomID ^ 0x2F4B91
                )
            );

        UnityEngine.GameObject shrineObject =
            UnityEngine.Object.Instantiate(
                diceSpawnerProp.propPrefab,
                __instance.transform.position,
                UnityEngine.Quaternion.identity
            );

        DiceSpawner shrine =
            shrineObject.GetComponent<DiceSpawner>();

        if (shrine == null)
        {
            MelonLogger.Warning(
                "[AP Shrine] DiceSpawner prefab did not " +
                "contain a DiceSpawner component."
            );

            UnityEngine.Object.Destroy(shrineObject);

            return true;
        }

        // Preserve the same random-ID initialization style
        // used by HiddenRoomRewardSpawner.SpawnProp().
        IRandomID[] randomComponents =
            shrineObject.GetComponents<IRandomID>();

        foreach (IRandomID randomComponent
                 in randomComponents)
        {
            randomComponent.SetRandomID(
                shrineRandom.Next()
            );
        }

        // Mark this particular DiceSpawner as the
        // Archipelago version.
        shrine.servedGuidHashes.Add(
            SephiriaMod.ArchipelagoShrineMarker
        );

        shrine.servedGuidHashes.Add(
            SephiriaMod.GetArchipelagoShrineSourceMarker(__instance.RandomID)
        );

        Mirror.NetworkServer.Spawn(shrineObject);

        SephiriaMod.MarkArchipelagoShrineSpawned();

        MelonLogger.Msg(
            "[AP Shrine] Spawned Archipelago Shrine."
        );

        // Prevent HiddenRoomRewardSpawner from also
        // spawning its normal reward.
        return false;
    }
}

[HarmonyPatch(typeof(DiceSpawner), "ServerCreateDice")]
public static class ArchipelagoShrineInteractionPatch
{
    public static bool Prefix(
        DiceSpawner __instance,
        ref bool __result,
        ref int __state)
    {
        __state = -1;

        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (!SephiriaMod.IsArchipelagoShrine(
                __instance))
        {
            return true;
        }

        if (__instance.servedGuidHashes.Contains(
                SephiriaMod.ArchipelagoShrineUsedMarker))
        {
            __result = false;
            return false;
        }

        string locationName;

        if (!SephiriaMod
                .TryCompleteRandomArchipelagoShrineCheck(
                    out locationName))
        {
            __result = false;
            return false;
        }

        int sourceRandomId;

        if (SephiriaMod.TryGetArchipelagoShrineSourceId(
                __instance,
                out sourceRandomId))
        {
            SephiriaMod.MarkArchipelagoShrineUsed(
                sourceRandomId
            );
        }
        else
        {
            MelonLogger.Warning(
                "[AP Shrine] Could not determine source " +
                "RandomID for activated shrine."
            );
        }

        // Mark this physical shrine as consumed.
        __instance.servedGuidHashes.Add(
            SephiriaMod.ArchipelagoShrineUsedMarker
        );

        // Preserve DiceSpawner's normal interaction,
        // networking and "used" behavior, but make it
        // create zero actual dice.
        __state = __instance.diceAmount;
        __instance.diceAmount = 0;

        MelonLogger.Msg(
            $"[AP Shrine] Shrine activated: " +
            $"{locationName}"
        );

        // Run Sephiria's original ServerCreateDice().
        // Because diceAmount is temporarily 0,
        // it won't actually spawn any Dice objects.
        return true;
    }

    public static void Postfix(
        DiceSpawner __instance,
        int __state)
    {
        if (__state < 0)
            return;

        __instance.diceAmount = __state;
    }
}

[HarmonyPatch(typeof(DiceSpawner), "OnServedChanged")]
public static class ArchipelagoShrineUsedVisualPatch
{
    public static void Postfix(
        DiceSpawner __instance,
        string newItem)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (newItem !=
            SephiriaMod.ArchipelagoShrineUsedMarker)
        {
            return;
        }

        if (__instance.interactable)
        {
            __instance.interactable.enabled = false;
        }

        if (__instance.diceRenderer)
        {
            __instance.diceRenderer.enabled = false;
        }
    }
}

[HarmonyPatch(typeof(DiceSpawner), nameof(DiceSpawner.OnStartClient))]
public static class ArchipelagoShrineStartClientPatch
{
    public static void Postfix(
        DiceSpawner __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!SephiriaMod.IsArchipelagoShrine(
                __instance))
        {
            return;
        }

        SephiriaMod.ApplyArchipelagoShrineVisual(__instance);

        if (!__instance.servedGuidHashes.Contains(
                SephiriaMod.ArchipelagoShrineUsedMarker))
        {
            return;
        }

        if (__instance.interactable)
        {
            __instance.interactable.enabled = false;
        }

        if (__instance.diceRenderer)
        {
            __instance.diceRenderer.enabled = false;
        }
    }
}

[HarmonyPatch(
    typeof(Sephirite),
    nameof(Sephirite.GenerateItems)
)]
public static class ArchipelagoSephiriteDebugPatch
{
    public static void Postfix(
        Sephirite __instance,
        GameObject actor)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!Mirror.NetworkServer.active)
            return;

        if (!SephiriaMod
                .IsEligibleArchipelagoSephirite(
                    __instance))
        {
            MelonLogger.Msg(
                "[AP Sephirite] " +
                $"Type={__instance.type} - " +
                "does not participate."
            );

            return;
        }

        int eligibleNumber;
        bool wasAlreadyAssigned;

        if (!SephiriaMod
                .TryGetOrAssignArchipelagoSephirite(
                    __instance,
                    actor,
                    out eligibleNumber,
                    out wasAlreadyAssigned))
        {
            MelonLogger.Warning(
                "[AP Sephirite] Could not determine " +
                "persistent Sephirite identity. " +
                "Leaving vanilla."
            );

            return;
        }

        if (wasAlreadyAssigned)
        {
            MelonLogger.Msg(
                $"[AP Sephirite] Reused persistent " +
                $"assignment #{eligibleNumber}, " +
                $"Seed={__instance.CurrentSeed}."
            );
        }

        bool isArchipelagoPosition =
            eligibleNumber % 2 == 0;

        if (!isArchipelagoPosition)
        {
            MelonLogger.Msg(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} -> VANILLA " +
                $"({__instance.type}, " +
                $"{__instance.Rewards.Count} choices, " +
                $"Seed={__instance.CurrentSeed})"
            );

            return;
        }

        int stageProgress;
        string poolName;
        List<string> locationNames;

        bool foundPool =
            SephiriaMod
                .TryGetArchipelagoSephiritePreviewChoices(
                    actor,
                    __instance.Rewards.Count,
                    __instance.CurrentSeed,
                    out stageProgress,
                    out poolName,
                    out locationNames
                );

        if (!foundPool)
        {
            MelonLogger.Warning(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} was an AP position, " +
                "but the current floor pool could not " +
                "be determined. Leaving vanilla."
            );

            return;
        }

        if (locationNames.Count == 0)
        {
            MelonLogger.Msg(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} -> AP position, " +
                $"StageProgress={stageProgress}, " +
                $"Pool={poolName}, but that pool " +
                "has no unchecked locations. " +
                "Leaving vanilla."
            );

            return;
        }

        MelonLogger.Msg(
            $"[AP Sephirite] Eligible " +
            $"#{eligibleNumber} -> AP, " +
            $"Seed={__instance.CurrentSeed}"
        );

        MelonLogger.Msg(
            $"[AP Sephirite] " +
            $"StageProgress={stageProgress}, " +
            $"Pool={poolName}, " +
            $"NormalChoices={__instance.Rewards.Count}, " +
            $"APChoices={locationNames.Count}"
        );

        foreach (string locationName in locationNames)
        {
            MelonLogger.Msg(
                "[AP Sephirite Choice] " +
                locationName
            );
        }
    }
}