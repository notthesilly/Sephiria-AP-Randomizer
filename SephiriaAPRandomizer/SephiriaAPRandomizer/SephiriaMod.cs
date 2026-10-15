using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using HarmonyLib;
using MelonLoader;
using Mirror;
using SephiriaAPRandomizer;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[assembly: MelonInfo(
    typeof(SephiriaAPRandomizer.SephiriaMod),
    "Sephiria AP Randomizer",
    "0.2.0",
    "TheSilly"
)]

namespace SephiriaAPRandomizer
{
    internal static class APDebug
    {
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(string message)
        {
            MelonLogger.Msg(message);
        }
    }

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
        public static bool RandomizeAnvilUnlocks { get; private set; } = true;
        public static int ProgressiveFloorUnlockCount { get; private set; }
        public static int StartingCintamaniStonePoolCount { get; private set; } = 0;
        private static bool cintamaniSyncPending;
        public static int SapphireMultiplier { get; private set; } = 1;
        public static string StartingCostumeId { get; private set; }
        public static EWeaponType? StartingWeaponType { get; private set; }
        public static int CompletedChapterClears { get; private set; }
        public static bool Chapter2EndingReached { get; set; }
        public static bool ArchipelagoGoalPending { get; set; }
        public static bool IsSynchronizingArchipelagoDestinyUpgrades { get; private set; }
        private static int archipelagoConsoleHistoryVersion;
        private static int archipelagoHintHistoryVersion;
        private static bool hardModeSyncPending;
        private static bool permanentItemSyncPending;
        private static bool startingWeaponInitializationPending;
        private static bool weaponStandSyncPending;
        private static bool archipelagoRecentItemsResetPending;
        public static bool IsBuildingArchipelagoCostumePanel;
        public static bool DeathLinkEnabled { get; private set; } = false;
        private static object deathLinkService;

        public static bool TrapsEnabled { get; private set; } = false;
        private static bool trapSyncPending;
        private static float cannotCastTrapUntil;
        private static float cannotDashTrapUntil;
        private static float cannotNormalAttackTrapUntil;
        private static float flippedControlsTrapUntil;

        public const string ArchipelagoShrineMarker = "__SEPHIRIA_ARCHIPELAGO_SHRINE__";
        public const string ArchipelagoShrineUsedMarker = "__SEPHIRIA_ARCHIPELAGO_SHRINE_USED__";
        private const string ArchipelagoShrineSourcePrefix = "__SEPHIRIA_AP_SHRINE_SOURCE_";
        private const string ArchipelagoShrineUsedSavePrefix = "ArchipelagoShrineUsed_";
        private const string ArchipelagoShrineLocationPrefix = "Archipelago Shrine Check ";
        private const string ArchipelagoSephiriteEligibleCountKey = "ArchipelagoSephiriteEligibleCount";
        private const string ArchipelagoSephiriteAssignmentPrefix = "ArchipelagoSephiriteAssignment_";
        private const string ArchipelagoSephiriteChoiceCountPrefix = "ArchipelagoSephiriteChoiceCount_";
        private const string ArchipelagoSephiriteChoiceLocationPrefix = "ArchipelagoSephiriteChoiceLocation_";
        private const string ArchipelagoSephiriteChoicesSavedPrefix = "ArchipelagoSephiriteChoicesSaved_";
        private const string ArchipelagoSephiriteClaimedPrefix = "ArchipelagoSephiriteClaimed_";
        private const string ArchipelagoSephiritePendingClaimPrefix = "ArchipelagoSephiritePendingClaim_";
        private const string ArchipelagoAnvilUsesThisRunKey = "ArchipelagoAnvilUsesThisRun";
        private const string ArchipelagoSapphireFillerItemName = "5 Sapphires";
        private const string ArchipelagoProcessedSapphireFillerCountKey = "ArchipelagoProcessedSapphireFillerCount";
        private const string ArchipelagoProcessedTrapCountKey = "ArchipelagoProcessedTrapCount";
        private const string ArchipelagoStartingCintamaniStoneItemName = "Starting Cintamani Stone";
        private const string ArchipelagoCintamaniGrantedThisRunKey = "ArchipelagoCintamaniGrantedThisRun";
        private const int ArchipelagoCintamaniEntityId = 1146;
        private const int ArchipelagoRecentItemsMaxVisible = 3;
        private const float ArchipelagoRecentItemLifetime = 8f;
        private const int ArchipelagoConsoleHistoryLimit = 120;

        private static readonly HashSet<int> activeArchipelagoSephiriteRuntimeInstances = new HashSet<int>();
        private static readonly HashSet<int> blockedArchipelagoSephiriteRuntimeInstances = new HashSet<int>();
        private static readonly Dictionary<int, long> archipelagoSephiriteLocationByRewardInstanceId = new Dictionary<int, long>();
        private static readonly Dictionary<int, string> archipelagoSephiriteLocationNameByRewardInstanceId = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> archipelagoSephiriteRuntimeIdentities = new Dictionary<int, string>();
        private static readonly object archipelagoRecentItemsLock = new object();
        private static readonly object archipelagoConsoleLock = new object();
        private static readonly object archipelagoHintLock = new object();
        private static readonly Queue<ArchipelagoRecentItemNotification> pendingArchipelagoRecentItems = new Queue<ArchipelagoRecentItemNotification>();
        private static readonly ConcurrentQueue<object> receivedDeathLinks = new ConcurrentQueue<object>();
        private static readonly List<ArchipelagoRecentItemNotification> visibleArchipelagoRecentItems = new List<ArchipelagoRecentItemNotification>();
        private static readonly List<ArchipelagoRecentItemCard> archipelagoRecentItemCards = new List<ArchipelagoRecentItemCard>();
        private static readonly List<string> archipelagoConsoleHistory = new List<string>();
        private static readonly List<string> archipelagoHintEntries = new List<string>();
        private sealed class ArchipelagoScoutedItemData { public string ItemName; public string PlayerName; public string GameName; public int Flags; }
        private sealed class ArchipelagoRecentItemNotification { public string FirstLine; public string SecondLine; public float ExpiresAt; }
        private sealed class ArchipelagoRecentItemCard { public GameObject Root; public RectTransform Rect; public Image Background; public TextMeshProUGUI MainText; public TextMeshProUGUI SourceText; }
        private static readonly Dictionary<long, ArchipelagoScoutedItemData> archipelagoSephiriteScoutedItems = new Dictionary<long, ArchipelagoScoutedItemData>();
        private static readonly Dictionary<long, ArchipelagoScoutedItemData> archipelagoCostumeMissionScoutedItems = new Dictionary<long, ArchipelagoScoutedItemData>();
        private static readonly Dictionary<long, ArchipelagoScoutedItemData> archipelagoDestinyInscriptionScoutedItems = new Dictionary<long, ArchipelagoScoutedItemData>();
        private static readonly Dictionary<string, string>
            archipelagoCostumeItems =
                new Dictionary<string, string>
                {
                    { "Pink Bunny", "PinkRabbit" },
                    { "Armored Bunny", "ArmoredRabbit" },
                    { "Brown Rabbit", "BrownRabbit" },
                    { "Braided Hair Rabbit", "DarkBrownRabbit" },
                    { "Orange Rabbit", "MandarinRabbit" },
                    { "Red-Hooded Rabbit", "RedHolyRabbit" },
                    { "White Rabbit", "WhiteRabbit" },
                    { "Wing-Eared Rabbit", "HolyRabbit" },

                    { "Red-Clothed Cat", "FireCat" },
                    { "Red Fox", "Fox" },
                    { "Frog", "Frog" },
                    { "Mole", "Mole" },
                    { "Otter", "Otter" },
                    { "White Wolf", "WhiteWolf" },
                    { "Wizard Bunny", "WizardRabbit" },
                    { "Eagle", "Bird" },
                    { "Crocodile", "Crocodile" },
                    { "Deer", "Deer" },
                    { "Fairy of Fortune", "Luck" },
                    { "Farmer Squirrel", "Squirrel" },
                    { "Turtle", "Turtle" },
                    { "Forest Cat", "ForestCat" },
                    { "Ghost", "Ghost" },
                    { "Scholar Lizard", "Seren" },

                    { "Wingless Bat", "Bat" },
                    { "Skeleton", "Skeleton" },
                    { "Adventurer Bunny", "AdventurerRabbit" }
                };
        private static readonly Dictionary<string, long>
            archipelagoCostumeMissionLocations =
                new Dictionary<string, long>
                {
                    { "FireCat", 2001 },
                    { "Fox", 2002 },
                    { "Frog", 2003 },
                    { "Mole", 2004 },
                    { "Otter", 2005 },
                    { "WhiteWolf", 2006 },
                    { "WizardRabbit", 2007 },
                    { "Bird", 2008 },
                    { "Turtle", 2013 },
                    { "ForestCat", 2014 },
                    { "Ghost", 2015 },
                    { "Skeleton", 2017 },
                    { "Bat", 2018 }
                };
        private static readonly Dictionary<string, string>
            archipelagoCostumeMissionNames =
                new Dictionary<string, string>
                {
                    { "FireCat", "Red-Clothed Cat" },
                    { "Fox", "Red Fox" },
                    { "Frog", "Frog" },
                    { "Mole", "Mole" },
                    { "Otter", "Otter" },
                    { "WhiteWolf", "White Wolf" },
                    { "WizardRabbit", "Wizard Bunny" },
                    { "Bird", "Eagle" },
                    { "Turtle", "Turtle" },
                    { "ForestCat", "Forest Cat" },
                    { "Ghost", "Ghost" },
                    { "Skeleton", "Skeleton" },
                    { "Bat", "Wingless Bat" }
                };
        private static readonly Dictionary<string, string>
            archipelagoCostumeMissionDescriptions =
                new Dictionary<string, string>
                {
                    { "FireCat", "Raise your Fire Damage up to 50" },
                    { "Fox", "Obtain at least 10 tablets in a single adventure" },
                    { "Frog", "Raise your Lightning Damage up to 50" },
                    { "Mole", "Defeat 300 fanatic moles" },
                    { "Otter", "Increase Critical Damage to 175%" },
                    { "WhiteWolf", "Defeat Rataka, pillager leader at least 5 times" },
                    { "WizardRabbit", "Defeat enemies 50 times with a grimoire" },
                    { "Bird", "Increase dash count to 8" },
                    { "Turtle", "Have 12 artifacts reach max level in a single adventure" },
                    { "ForestCat", "Collect 2500 leaf" },
                    { "Ghost", "Obtain at least 5 grimoires in a single adventure" },
                    { "Skeleton", "Take 333 damage in a single adventure" },
                    { "Bat", "Use the blood donation shrine 5 times" }
                };
        private static readonly HashSet<string>
            removedArchipelagoCostumeMissionIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                )
                {
                    "Crocodile",
                    "Deer",
                    "Luck",
                    "Squirrel",
                    "Seren"
                };
        public static bool IsRemovedArchipelagoCostumeMission(
            string costumeId)
        {
            return !string.IsNullOrEmpty(costumeId) &&
                   removedArchipelagoCostumeMissionIds.Contains(
                       costumeId
                   );
        }
        private static readonly Dictionary<string, int>
            archipelagoPermanentRewardItems =
                new Dictionary<string, int>(
                    StringComparer.Ordinal
                )
                {
                    { "Golden Needle", 3001 },

                    { "Boss Reward Unlock: Mogdi", 2000 },
                    { "Boss Reward Unlock: Rataka", 2001 },
                    { "Boss Reward Unlock: Erma", 2002 },
                    { "Boss Reward Unlock: Askard", 2003 },
                    { "Boss Reward Unlock: Odner", 2004 },
                    { "Boss Reward Unlock: Lerid", 2005 },
                    { "Boss Reward Unlock: Pentaxis", 2006 },
                    { "Boss Reward Unlock: Kovess", 2007 },
                    { "Boss Reward Unlock: Kraz", 2008 },
                    { "Boss Reward Unlock: Haz", 2009 }
                };
        private static readonly Dictionary<int, long>
            archipelagoBossDefeatLocations =
                new Dictionary<int, long>
                {
                    { 2000, 4001 }, // Mogdi
                    { 2001, 4002 }, // Rataka
                    { 2002, 4003 }, // Erma
                    { 2003, 4004 }, // Askard
                    { 2004, 4005 }, // Odner
                    { 2005, 4006 }, // Lerid
                    { 2006, 4007 }, // Pentaxis
                    { 2007, 4008 }, // Kovess
                    { 2008, 4009 }, // Kraz
                    { 2009, 4010 }  // Haz
                };
        private static readonly Dictionary<string, string>
            archipelagoMissionUnlockItems =
                new Dictionary<string, string>
                {
                    {
                        "Mission Unlock: Home Repairs",
                        "Destiny_RebuildHome_0"
                    },
                    {
                        "Mission Unlock: Fruit Skewer",
                        "Destiny_FruitSkewer_0"
                    },
                    {
                        "Mission Unlock: Magic Workshop",
                        "Destiny_RebuildWorkShop_0"
                    },
                    {
                        "Mission Unlock: Basement",
                        "Destiny_SalonBasement_0"
                    },
                    {
                        "Mission Unlock: Hide and Seek",
                        "Destiny_HideAndSeek_0"
                    },
                    {
                        "Mission Unlock: Rylie's Gift",
                        "Destiny_RyliePotion0"
                    },
                    {
                        "Mission Unlock: Terrain Recovery",
                        "Destiny_FillTheCliff_0"
                    }
                };
        private static readonly Dictionary<string, long>
            archipelagoMissionLocations =
                new Dictionary<string, long>(
                    StringComparer.Ordinal
                )
                {
                    { "Home Repairs", 3501 },
                    { "Fruit Skewer", 3502 },
                    { "Magic Workshop", 3503 },

                    // 3504 intentionally unused.

                    { "Basement", 3505 },
                    { "Hide and Seek", 3506 },
                    { "Rylie's Gift", 3507 },
                    { "Terrain Recovery", 3508 }
                };
        private static readonly Dictionary<string, EWeaponType>
            archipelagoWeaponUnlockItems =
                new Dictionary<string, EWeaponType>(
                    StringComparer.Ordinal
                )
                {
                    { "Sword and Shield Unlock", EWeaponType.SwordAndShield },
                    { "Greatsword Unlock", EWeaponType.GreatSword },
                    { "Dagger Unlock", EWeaponType.Dagger },
                    { "Crossbow Unlock", EWeaponType.Crossbow },
                    { "Staff Unlock", EWeaponType.Staff },
                    { "Katana Unlock", EWeaponType.Katana }
                };
        private static readonly Dictionary<int, long>
            archipelagoDestinyInscriptionLocationsByTreeShopId =
                new Dictionary<int, long>
                {
                    { 0, 3001 },       // Tablet
                    { 1, 3002 },       // Rylie's Gift
                    { 2, 3003 },       // Heavy Weapon Training
                    { 3, 3004 },       // Nimble Weapon Training
                    { 4, 3005 },       // Dice I
                    { 5, 3006 },       // Magic Workshop
                    { 6, 3007 },       // Home Repairs
                    { 7, 3008 },       // Terrain Recovery
                    { 9, 3009 },       // Dice Acquisition
                    { 10, 3010 },      // Seed Money
                    { 11, 3011 },      // Chest of Possibilities
                    { 12, 3012 },      // Ranged Weapon Training
                    { 13, 3013 },      // Basement
                    { 14, 3014 },      // Wishing Fountain
                    { 16, 3015 },      // Dice Transformation
                    { 17, 3016 },      // Sharp Weapon Training
                    { 18, 3017 },      // Hide and Seek
                    { 19, 3018 },      // Graceful Weapon Training
                    { 20, 3019 },      // Fruit Skewer

                    { 300, 3020 },     // Talent Point I
                    { 301, 3021 },     // Talent Point II
                    { 302, 3022 },     // Level Up Restoration
                    { 303, 3023 },     // Dice II
                    { 304, 3024 },     // Expanding Pouch I
                    { 305, 3025 },     // Expanding Pouch II
                    { 306, 3026 },     // Extra Experience Drop
                    { 307, 3027 },     // Talent Point III
                    { 308, 3028 },     // Talent Point IV
                    { 309, 3029 },     // MP Regeneration
                    { 310, 3030 },     // Talent Point V
                    { 311, 3031 },     // Increase HP I
                    { 312, 3032 },     // Increase HP II
                    { 313, 3033 },     // Dice III
                    { 314, 3034 },     // Side Bag I
                    { 315, 3035 },     // Side Bag II

                    { 1000, 3036 },    // Artifact Unlock I
                    { 1001, 3037 },    // Artifact Unlock II
                    { 1002, 3038 },    // Artifact Unlock III
                    { 1003, 3039 },    // Artifact Unlock IV
                    { 1004, 3040 },    // Artifact Unlock V
                    { 1005, 3041 },    // Artifact Unlock VI
                    { 1006, 3042 },    // Bond Artifact Unlock
                    { 1007, 3043 },    // Artifact Unlock VII
                    { 1008, 3044 }     // Artifact Unlock VIII
                };
        private static readonly Dictionary<string, int>
            archipelagoDestinyUpgradeItems =
                new Dictionary<string, int>(
                    StringComparer.Ordinal
                )
                {
                    { "Tablet Unlock", 0 },
                    { "Dice Acquisition", 9 },
                    { "Seed Money", 10 },
                    { "Chest of Possibilities", 11 },
                    { "Wishing Fountain Upgrade", 14 },
                    { "Dice Transformation", 16 },

                    { "Level Up Restoration", 302 },
                    { "Extra Experience Drop", 306 },
                    { "MP Regeneration", 309 },

                    { "Bond Artifact Unlock", 1006 }
                };


        private static readonly Dictionary<string, int[]>
            archipelagoProgressiveDestinyUpgradeItems =
                new Dictionary<string, int[]>(
                    StringComparer.Ordinal
                )
                {
            {
                "Progressive Dice Upgrade",
                new int[]
                {
                    4,
                    303,
                    313
                }
            },

            {
                "Progressive Expanding Pouch",
                new int[]
                {
                    304,
                    305
                }
            },

            {
                "Progressive Increase HP",
                new int[]
                {
                    311,
                    312
                }
            },

            {
                "Progressive Side Bag",
                new int[]
                {
                    314,
                    315
                }
            },

            {
                "Progressive Artifact Unlock",
                new int[]
                {
                    1000,
                    1001,
                    1002,
                    1003,
                    1004,
                    1005,
                    1007,
                    1008
                }
            }
                };
        private static readonly string[] archipelagoDeathLinkMessages = new string[]
        {
            "{0} met an unfortunate end in Sephiria.",
            "{0} was defeated during their adventure.",
            "{0} failed to make it back to town.",
            "{0}'s adventure came to an abrupt end.",
            "{0} has fallen in Sephiria.",
            "{0} made an oopsie.",
            "{0} forgot to dodge.",
            "{0} forgot that health is supposed to stay above 0",
            "{0} discovered that dying is, in fact, fatal",
            "{0} discovered a skill issue",
            "{0} died. Unfortunitely, that's everyone's problem now.",
            "{0} couldn't win, so nobody else gets to either"
        };
        private static readonly HashSet<string>
            archipelagoTrapItemNames =
                new HashSet<string>(
                    StringComparer.Ordinal
                )
                {
                    "Can't Cast Trap",
                    "Can't Dash Trap",
                    "Can't Normal Attack Trap",
                    "Flipped Controls Trap",
                    "Bag Shuffle Trap"
                };
        private static readonly HashSet<int> archipelagoPermanentRewardTreeShopIds = new HashSet<int>(archipelagoPermanentRewardItems.Values);
        private static readonly FieldInfo sephiriteRerollButtonGroupField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "rerollButtonGroup");
        private static readonly FieldInfo sephiriteSkipSelectButtonGroupField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "skipSelectButtonGroup");
        private static readonly FieldInfo sephiriteConvertRerollDiceButtonGroupField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "convertRerollDiceButtonGroup");
        private static readonly FieldInfo sephiriteSelectSoundField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "selectSound");
        private static readonly System.Random archipelagoShrineCheckRandom = new System.Random();
        private static readonly System.Random archipelagoDeathLinkMessageRandom = new System.Random();
        private static readonly System.Random archipelagoTrapRandom = new System.Random();
        private static int hiddenRoomRewardAttemptsThisRun;
        private static UnityEngine.Sprite archipelagoShrineSprite;
        private static UnityEngine.Sprite archipelagoSephiriteSprite;
        private static UnityEngine.Sprite archipelagoSephiriteOutlineSprite;
        private static UnityEngine.Sprite archipelagoSaveProfileSprite;
        private static UnityEngine.Sprite archipelagoSaveProfileOutlineSprite;
        private static GameObject archipelagoRecentItemsHudRoot;
        private static object pendingDeathLink;
        private static bool suppressNextOutgoingDeathLink;
        private static string currentArchipelagoSlotName;
        private static bool wasArchipelagoConnected;
        private static bool disconnectMessageShown;
        private static bool suppressOutgoingDeathLinkForRunVictory;
        private static bool suppressOutgoingDeathLinkForForcedFloorDeath;

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
                APDebug.Log(
                    "[AP Shrine] Third hidden-room reward - " +
                    "forcing Archipelago Shrine."
                );

                return true;
            }

            System.Random apRandom = new System.Random(unchecked(randomId ^ 0x5A17C3));

            bool spawnArchipelagoShrine = apRandom.Next(0, 2) == 0;

            APDebug.Log(
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
                            APDebug.Log(
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

                        APDebug.Log(
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

        public static UnityEngine.Sprite GetArchipelagoSephiriteSprite(
            UnityEngine.Sprite originalSprite)
        {
            if (archipelagoSephiriteSprite != null)
                return archipelagoSephiriteSprite;

            if (originalSprite == null)
                return null;

            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                using (Stream stream =
                    assembly.GetManifestResourceStream(
                        "SephiriaAPRandomizer.Resources.ap_sephirite_icon.png"
                    ))
                {
                    if (stream == null)
                    {
                        MelonLogger.Warning(
                            "[AP Sephirite] Could not find embedded AP logo."
                        );

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

                        archipelagoSephiriteSprite =
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
                                originalSprite.pixelsPerUnit
                            );

                        archipelagoSephiriteSprite.name =
                            "ArchipelagoSephiriteLogo";

                        return archipelagoSephiriteSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    "[AP Sephirite] Logo load failed: " +
                    ex
                );

                return null;
            }
        }

        public static UnityEngine.Sprite GetArchipelagoSephiriteOutlineSprite(
            UnityEngine.Sprite originalSprite)
        {
            if (archipelagoSephiriteOutlineSprite != null)
                return archipelagoSephiriteOutlineSprite;

            if (originalSprite == null)
                return null;

            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                using (Stream stream =
                    assembly.GetManifestResourceStream(
                        "SephiriaAPRandomizer.Resources.ap_sephirite_icon_outline.png"
                    ))
                {
                    if (stream == null)
                    {
                        MelonLogger.Warning(
                            "[AP Save Icon] Could not find embedded outline logo."
                        );

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

                        archipelagoSephiriteOutlineSprite =
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
                                originalSprite.pixelsPerUnit
                            );

                        archipelagoSephiriteOutlineSprite.name =
                            "ArchipelagoSephiriteOutlineLogo";

                        return archipelagoSephiriteOutlineSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    "[AP Save Icon] Outline logo load failed: " +
                    ex
                );

                return null;
            }
        }

        public static UnityEngine.Sprite GetArchipelagoSaveProfileSprite(
            UnityEngine.Sprite originalSprite)
        {
            if (archipelagoSaveProfileSprite != null)
                return archipelagoSaveProfileSprite;

            if (originalSprite == null)
                return null;

            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                using (Stream stream =
                    assembly.GetManifestResourceStream(
                        "SephiriaAPRandomizer.Resources.ap_sephirite_icon.png"
                    ))
                {
                    if (stream == null)
                    {
                        MelonLogger.Warning(
                            "[AP Save Icon] Could not find embedded AP logo."
                        );

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
                                    flippedY * bitmap.Width + x
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

                        archipelagoSaveProfileSprite =
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
                                originalSprite.pixelsPerUnit
                            );

                        archipelagoSaveProfileSprite.name =
                            "ArchipelagoSaveProfileLogo";

                        return archipelagoSaveProfileSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    "[AP Save Icon] Logo load failed: " +
                    ex
                );

                return null;
            }
        }

        public static UnityEngine.Sprite GetArchipelagoSaveProfileOutlineSprite(
            UnityEngine.Sprite originalSprite)
        {
            if (archipelagoSaveProfileOutlineSprite != null)
                return archipelagoSaveProfileOutlineSprite;

            if (originalSprite == null)
                return null;

            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                using (Stream stream =
                    assembly.GetManifestResourceStream(
                        "SephiriaAPRandomizer.Resources.ap_sephirite_icon_outline.png"
                    ))
                {
                    if (stream == null)
                    {
                        MelonLogger.Warning(
                            "[AP Save Icon] Could not find embedded outline logo."
                        );

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
                                    flippedY * bitmap.Width + x
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

                        archipelagoSaveProfileOutlineSprite =
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
                                originalSprite.pixelsPerUnit
                            );

                        archipelagoSaveProfileOutlineSprite.name =
                            "ArchipelagoSaveProfileOutlineLogo";

                        return archipelagoSaveProfileOutlineSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    "[AP Save Icon] Outline logo load failed: " +
                    ex
                );

                return null;
            }
        }

        public static void ApplyArchipelagoSephiriteRewardIcon(
            UI_SephiriteRewardElement rewardElement)
        {
            if (rewardElement == null ||
                rewardElement.iconImage == null ||
                rewardElement.iconImage.sprite == null)
            {
                return;
            }

            UnityEngine.Sprite logo =
                GetArchipelagoSephiriteSprite(
                    rewardElement.iconImage.sprite
                );

            if (logo == null)
                return;

            rewardElement.iconImage.sprite = logo;

            // Keep the AP logo proportional even when the
            // underlying vanilla shell used a differently
            // shaped icon.
            rewardElement.iconImage.preserveAspect = true;

            rewardElement.iconImage.rectTransform.sizeDelta =
                new Vector2(
                    rewardElement.iconImage.preferredWidth,
                    rewardElement.iconImage.preferredHeight
                );

            rewardElement.iconImage.material =
                rewardElement.iconImage.defaultMaterial;

            // Tablet shells can also rotate their icon.
            rewardElement.iconImage.rectTransform.rotation =
                Quaternion.identity;

            // AP items do not use the vanilla shell's sub-icons.
            if (rewardElement.subIconImages != null)
            {
                foreach (UnityEngine.UI.Image subIcon
                    in rewardElement.subIconImages)
                {
                    if (subIcon != null)
                    {
                        subIcon.gameObject.SetActive(false);
                    }
                }
            }
        }

        public static void ApplyArchipelagoSephiriteSelectedIcon(
            UI_SephiriteRewardElement rewardElement)
        {
            if (!IsArchipelagoSave ||
                rewardElement == null ||
                rewardElement.Sephirite == null ||
                rewardElement.iconImage == null)
            {
                return;
            }

            if (!IsActiveArchipelagoSephirite(
                    rewardElement.Sephirite))
            {
                return;
            }

            UnityEngine.Sprite outlineSprite =
                GetArchipelagoSephiriteOutlineSprite(
                    rewardElement.iconImage.sprite
                );

            if (outlineSprite != null)
            {
                rewardElement.iconImage.sprite =
                    outlineSprite;
            }
        }

        public static void ApplyArchipelagoSephiriteNormalIcon(
            UI_SephiriteRewardElement rewardElement)
        {
            if (!IsArchipelagoSave ||
                rewardElement == null ||
                rewardElement.Sephirite == null ||
                rewardElement.iconImage == null)
            {
                return;
            }

            if (!IsActiveArchipelagoSephirite(
                    rewardElement.Sephirite))
            {
                return;
            }

            UnityEngine.Sprite normalSprite =
                GetArchipelagoSephiriteSprite(
                    rewardElement.iconImage.sprite
                );

            if (normalSprite != null)
            {
                rewardElement.iconImage.sprite =
                    normalSprite;
            }
        }

        public static void ApplyArchipelagoSephiritePickerIcon(
            UI_NewItemPicker_Controller picker,
            UI_SephiriteRewardElement rewardElement)
        {
            if (!IsArchipelagoSave)
                return;

            if (picker == null ||
                picker.iconImage == null ||
                rewardElement == null ||
                rewardElement.Sephirite == null)
            {
                return;
            }

            if (!IsActiveArchipelagoSephirite(
                    rewardElement.Sephirite))
            {
                return;
            }

            long locationId;
            string locationName;

            if (!TryGetArchipelagoSephiriteChoice(
                    rewardElement.reward.instanceID,
                    out locationId,
                    out locationName))
            {
                return;
            }

            UnityEngine.Sprite logo =
                GetArchipelagoSephiriteSprite(
                    picker.iconImage.sprite
                );

            if (logo == null)
                return;

            picker.iconImage.sprite = logo;
            picker.iconImage.preserveAspect = true;

            picker.iconImage.rectTransform.sizeDelta =
                new Vector2(
                    picker.iconImage.preferredWidth,
                    picker.iconImage.preferredHeight
                );

            picker.iconImage.material =
                picker.iconImage.defaultMaterial;

            picker.iconImage.rectTransform.rotation =
                Quaternion.identity;
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

            APDebug.Log(
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

            APDebug.Log(
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
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Error(
                    "[AP Sephirite] CurrentRun is unavailable. " +
                    "This eligible Sephirite has been blocked."
                );

                return false;
            }

            string identity;

            if (!TryGetArchipelagoSephiriteIdentity(
                sephirite,
                actor,
                out identity))
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Error(
                    "[AP Sephirite] Persistent identity could not " +
                    "be determined. This eligible Sephirite " +
                    "has been blocked."
                );

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

            int previousEligibleCount =
                SaveManager.CurrentRun.GetInt(
                    ArchipelagoSephiriteEligibleCountKey,
                    0
                );

            // Truly new Sephirite. Advance the universal
            // alternating sequence exactly once.
            eligibleNumber =
                previousEligibleCount + 1;

            try
            {
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
            }
            catch (Exception ex)
            {
                try
                {
                    SaveManager.CurrentRun.SetInt(
                        ArchipelagoSephiriteEligibleCountKey,
                        previousEligibleCount
                    );

                    SaveManager.CurrentRun.SetInt(
                        assignmentKey,
                        0
                    );
                }
                catch (Exception rollbackEx)
                {
                    MelonLogger.Error(
                        "[AP Sephirite] Failed to roll back " +
                        "eligible assignment after save failure: " +
                        rollbackEx.Message
                    );
                }

                blockedArchipelagoSephiriteRuntimeInstances.Add(sephirite.GetInstanceID());

                eligibleNumber = 0;

                MelonLogger.Error(
                    "[AP Sephirite] Failed to persist " +
                    "eligible assignment: " +
                    ex.Message
                );

                return false;
            }

            return true;
        }

        private static bool TryGetArchipelagoSephiriteRuntimeIdentity(
            Sephirite sephirite,
            out string identity)
        {
            identity = null;

            if (sephirite == null)
                return false;

            return archipelagoSephiriteRuntimeIdentities.TryGetValue(
                sephirite.GetInstanceID(),
                out identity
            );
        }


        public static bool IsActiveArchipelagoSephirite(
            Sephirite sephirite)
        {
            if (!IsArchipelagoSave)
                return false;

            if (sephirite == null)
                return false;

            return activeArchipelagoSephiriteRuntimeInstances.Contains(
                sephirite.GetInstanceID()
            );
        }

        public static bool IsBlockedArchipelagoSephirite(
            Sephirite sephirite)
        {
            if (!IsArchipelagoSave)
                return false;

            if (sephirite == null)
                return false;

            return blockedArchipelagoSephiriteRuntimeInstances.Contains(
                sephirite.GetInstanceID()
            );
        }

        public static bool WasArchipelagoSephiriteClaimed(
            Sephirite sephirite)
        {
            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            return SaveManager.CurrentRun.GetBool(
                ArchipelagoSephiriteClaimedPrefix +
                identity,
                false
            );
        }

        public static bool MarkArchipelagoSephiriteClaimed(
            Sephirite sephirite)
        {
            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            SaveManager.CurrentRun.SetBool(
                ArchipelagoSephiriteClaimedPrefix +
                identity,
                true
            );

            try
            {
                SaveManager.Save(
                    saveCurrent: false,
                    saveCurrentRun: true
                );
            }
            catch (Exception ex)
            {
                SaveManager.CurrentRun.SetBool(
                    ArchipelagoSephiriteClaimedPrefix +
                    identity,
                    false
                );

                MelonLogger.Error(
                    "[AP Sephirite] Failed to persist Claimed state: " +
                    ex.Message
                );

                return false;
            }

            APDebug.Log(
                $"[AP Sephirite] Marked claimed: {identity}."
            );

            return true;
        }

        public static bool SetArchipelagoSephiritePendingClaim(
            Sephirite sephirite,
            long locationId)
        {
            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            SaveManager.CurrentRun.SetString(
                ArchipelagoSephiritePendingClaimPrefix +
                identity,
                locationId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                )
            );

            try
            {
                SaveManager.Save(
                    saveCurrent: false,
                    saveCurrentRun: true
                );
            }
            catch (Exception ex)
            {
                SaveManager.CurrentRun.SetString(
                    ArchipelagoSephiritePendingClaimPrefix +
                    identity,
                    ""
                );

                MelonLogger.Error(
                    "[AP Sephirite] Failed to persist Pending claim: " +
                    ex.Message
                );

                return false;
            }

            APDebug.Log(
                $"[AP Sephirite] Pending claim saved: " +
                $"{identity} -> {locationId}."
            );

            return true;
        }

        public static bool TryGetArchipelagoSephiritePendingClaim(
            Sephirite sephirite,
            out long locationId)
        {
            locationId = 0;

            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            string locationIdText =
                SaveManager.CurrentRun.GetString(
                    ArchipelagoSephiritePendingClaimPrefix +
                    identity,
                    ""
                );

            if (string.IsNullOrEmpty(locationIdText))
                return false;

            return long.TryParse(
                locationIdText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out locationId
            );
        }

        private static bool HasArchipelagoSephiritePendingClaimData(
            Sephirite sephirite)
        {
            if (sephirite == null)
                return false;

            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            string locationIdText =
                SaveManager.CurrentRun.GetString(
                    ArchipelagoSephiritePendingClaimPrefix +
                    identity,
                    ""
                );

            return !string.IsNullOrEmpty(locationIdText);
        }

        public static bool ClearArchipelagoSephiritePendingClaim(
            Sephirite sephirite)
        {
            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            string pendingKey =
                ArchipelagoSephiritePendingClaimPrefix +
                identity;

            string previousPendingValue =
                SaveManager.CurrentRun.GetString(
                    pendingKey,
                    ""
                );

            try
            {
                SaveManager.CurrentRun.SetString(
                    pendingKey,
                    ""
                );

                SaveManager.Save(
                    saveCurrent: false,
                    saveCurrentRun: true
                );
            }
            catch (Exception ex)
            {
                try
                {
                    SaveManager.CurrentRun.SetString(
                        pendingKey,
                        previousPendingValue
                    );
                }
                catch (Exception rollbackEx)
                {
                    MelonLogger.Error(
                        "[AP Sephirite] Failed to restore Pending " +
                        "state after clear failure: " +
                        rollbackEx.Message
                    );
                }

                MelonLogger.Error(
                    "[AP Sephirite] Failed to clear Pending claim: " +
                    ex.Message
                );

                return false;
            }

            return true;
        }

        public static bool TryResolveArchipelagoSephiritePendingClaim(
            Sephirite sephirite,
            out bool resolvedAsClaimed)
        {
            resolvedAsClaimed = false;

            if (sephirite == null)
                return false;

            bool hasPendingClaimData =
                HasArchipelagoSephiritePendingClaimData(
                    sephirite
                );

            // No saved Pending value means there is no
            // interrupted transaction to recover.
            if (!hasPendingClaimData)
                return true;

            long pendingLocationId;

            if (!TryGetArchipelagoSephiritePendingClaim(
                    sephirite,
                    out pendingLocationId))
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Error(
                    "[AP Sephirite] Pending claim data exists, " +
                    "but its location ID is invalid. " +
                    "This Sephirite has been blocked."
                );

                return false;
            }

            bool pendingIsSavedChoice;

            if (!TryIsSavedArchipelagoSephiriteChoice(
                    sephirite,
                    pendingLocationId,
                    out pendingIsSavedChoice))
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Error(
                    "[AP Sephirite] Pending claim could not " +
                    "be verified against the saved choices. " +
                    "This Sephirite has been blocked."
                );

                return false;
            }

            if (!pendingIsSavedChoice)
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Error(
                    "[AP Sephirite] Pending location " +
                    $"{pendingLocationId} was not one of this " +
                    "Sephirite's saved choices. " +
                    "This Sephirite has been blocked."
                );

                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    "[AP Sephirite] Cannot reconcile pending " +
                    "claim because Archipelago is not connected."
                );

                return false;
            }

            bool locationIsStillMissing =
                apSession.Locations
                    .AllMissingLocations
                    .Contains(pendingLocationId);

            if (locationIsStillMissing)
            {
                if (!ClearArchipelagoSephiritePendingClaim(
                    sephirite))
                {
                    MelonLogger.Error(
                        "[AP Sephirite] Pending location is still missing, " +
                        "but Pending state could not be cleared. " +
                        "Reward recovery was stopped."
                    );

                    return false;
                }

                MelonLogger.Msg(
                    $"[AP Sephirite] Pending location " +
                    $"{pendingLocationId} is still missing. " +
                    "Pending state cleared; reward may be retried."
                );

                return true;
            }

            // The location is no longer missing, so the AP
            // server already received the check.
            if (!MarkArchipelagoSephiriteClaimed(
                    sephirite))
            {
                MelonLogger.Error(
                    "[AP Sephirite] Pending location was already " +
                    "checked, but Claimed state could not be saved."
                );

                // Keep Pending intact so recovery can be
                // attempted again later.
                return false;
            }

            if (!ClearArchipelagoSephiritePendingClaim(
                sephirite))
            {
                MelonLogger.Error(
                    "[AP Sephirite] Claimed state was saved, " +
                    "but Pending state could not be cleared. " +
                    "Recovery will be retried."
                );

                return false;
            }

            resolvedAsClaimed = true;

            MelonLogger.Msg(
                $"[AP Sephirite] Recovered completed pending " +
                $"claim {pendingLocationId}. Sephirite is claimed."
            );

            return true;
        }

        private static bool SaveArchipelagoSephiriteChoices(
            Sephirite sephirite,
            List<long> selectedLocations)
        {
            if (sephirite == null)
                return false;

            if (selectedLocations == null ||
                selectedLocations.Count == 0)
            {
                return false;
            }

            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            try
            {
                SaveManager.CurrentRun.SetInt(
                    ArchipelagoSephiriteChoiceCountPrefix +
                    identity,
                    selectedLocations.Count
                );

                for (int i = 0;
                     i < selectedLocations.Count;
                     i++)
                {
                    string locationKey =
                        ArchipelagoSephiriteChoiceLocationPrefix +
                        identity +
                        "_" +
                        i;

                    SaveManager.CurrentRun.SetString(
                        locationKey,
                        selectedLocations[i].ToString(
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    );
                }

                SaveManager.CurrentRun.SetBool(
                    ArchipelagoSephiriteChoicesSavedPrefix +
                    identity,
                    true
                );

                SaveManager.Save(
                    saveCurrent: false,
                    saveCurrentRun: true
                );
            }
            catch (Exception ex)
            {
                try
                {
                    SaveManager.CurrentRun.SetInt(
                        ArchipelagoSephiriteChoiceCountPrefix +
                        identity,
                        0
                    );

                    for (int i = 0;
                         i < selectedLocations.Count;
                         i++)
                    {
                        string locationKey =
                            ArchipelagoSephiriteChoiceLocationPrefix +
                            identity +
                            "_" +
                            i;

                        SaveManager.CurrentRun.SetString(
                            locationKey,
                            ""
                        );
                    }

                    SaveManager.CurrentRun.SetBool(
                        ArchipelagoSephiriteChoicesSavedPrefix +
                        identity,
                        false
                    );
                }
                catch (Exception rollbackEx)
                {
                    MelonLogger.Error(
                        "[AP Sephirite] Failed to roll back " +
                        "choice state after save failure: " +
                        rollbackEx.Message
                    );
                }

                MelonLogger.Error(
                    "[AP Sephirite] Failed to persist AP choices: " +
                    ex.Message
                );

                return false;
            }

            APDebug.Log(
                $"[AP Sephirite] Persisted " +
                $"{selectedLocations.Count} choices for " +
                $"{identity}."
            );

            return true;
        }

        private static bool HasSavedArchipelagoSephiriteChoices(
            Sephirite sephirite)
        {
            if (sephirite == null)
                return false;

            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            bool choicesSaved =
                SaveManager.CurrentRun.GetBool(
                    ArchipelagoSephiriteChoicesSavedPrefix +
                    identity,
                    false
                );

            if (choicesSaved)
                return true;

            // Compatibility with Sephirites whose choices were
            // persisted before ChoicesSaved was introduced.
            int legacySavedChoiceCount =
                SaveManager.CurrentRun.GetInt(
                    ArchipelagoSephiriteChoiceCountPrefix +
                    identity,
                    0
                );

            return legacySavedChoiceCount > 0;
        }

        private static bool TryIsSavedArchipelagoSephiriteChoice(
            Sephirite sephirite,
            long locationId,
            out bool isSavedChoice)
        {
            isSavedChoice = false;

            if (sephirite == null)
                return false;

            if (SaveManager.CurrentRun == null)
                return false;

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            int savedChoiceCount =
                SaveManager.CurrentRun.GetInt(
                    ArchipelagoSephiriteChoiceCountPrefix +
                    identity,
                    0
                );

            if (savedChoiceCount <= 0)
                return false;

            for (int i = 0;
                 i < savedChoiceCount;
                 i++)
            {
                string locationKey =
                    ArchipelagoSephiriteChoiceLocationPrefix +
                    identity +
                    "_" +
                    i;

                string savedLocationText =
                    SaveManager.CurrentRun.GetString(
                        locationKey,
                        ""
                    );

                long savedLocationId;

                if (!long.TryParse(
                        savedLocationText,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out savedLocationId))
                {
                    return false;
                }

                if (savedLocationId == locationId)
                {
                    isSavedChoice = true;
                    return true;
                }
            }

            return true;
        }

        private static bool TryLoadSavedArchipelagoSephiriteChoices(
            Sephirite sephirite,
            ArchipelagoSession apSession,
            string poolName,
            out List<long> selectedLocations,
            out List<string> locationNames)
        {
            selectedLocations = new List<long>();
            locationNames = new List<string>();

            if (sephirite == null ||
                apSession == null ||
                SaveManager.CurrentRun == null)
            {
                return false;
            }

            string identity;

            if (!TryGetArchipelagoSephiriteRuntimeIdentity(
                    sephirite,
                    out identity))
            {
                return false;
            }

            int savedChoiceCount =
                SaveManager.CurrentRun.GetInt(
                    ArchipelagoSephiriteChoiceCountPrefix +
                    identity,
                    0
                );

            // No saved choices means this Sephirite has
            // never had its AP choices generated before.
            if (savedChoiceCount <= 0)
            {
                if (HasSavedArchipelagoSephiriteChoices(
                        sephirite))
                {
                    MelonLogger.Error(
                        "[AP Sephirite] Saved-choice marker exists, " +
                        "but the saved choice count is invalid."
                    );
                }

                return false;
            }

            for (int i = 0;
                 i < savedChoiceCount;
                 i++)
            {
                string locationKey =
                    ArchipelagoSephiriteChoiceLocationPrefix +
                    identity +
                    "_" +
                    i;

                string locationIdText =
                    SaveManager.CurrentRun.GetString(
                        locationKey,
                        ""
                    );

                long locationId;

                if (!long.TryParse(
                        locationIdText,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out locationId))
                {
                    MelonLogger.Warning(
                        "[AP Sephirite] Could not restore " +
                        $"saved choice #{i} for {identity}."
                    );

                    selectedLocations.Clear();
                    locationNames.Clear();

                    return false;
                }

                string locationName =
                    apSession.Locations
                        .GetLocationNameFromId(
                            locationId,
                            "Sephiria"
                        );

                if (string.IsNullOrEmpty(locationName))
                {
                    MelonLogger.Warning(
                        "[AP Sephirite] Saved location " +
                        $"{locationId} could not be resolved."
                    );

                    selectedLocations.Clear();
                    locationNames.Clear();

                    return false;
                }

                string expectedPrefix =
                    poolName + " Sephirite Item ";

                if (!locationName.StartsWith(
                        expectedPrefix,
                        StringComparison.Ordinal))
                {
                    MelonLogger.Error(
                        "[AP Sephirite] Saved location " +
                        $"{locationName} does not belong to " +
                        $"the expected pool {poolName}."
                    );

                    selectedLocations.Clear();
                    locationNames.Clear();

                    return false;
                }

                bool isStillMissing =
                    apSession.Locations
                        .AllMissingLocations
                        .Contains(locationId);

                if (!isStillMissing)
                {
                    MelonLogger.Msg(
                        $"[AP Sephirite] Saved choice " +
                        $"{locationId} is already checked. " +
                        "It will not be replaced."
                    );

                    continue;
                }

                selectedLocations.Add(locationId);
                locationNames.Add(locationName);
            }

            if (sephirite.Rewards.Count <
                selectedLocations.Count)
            {
                MelonLogger.Warning(
                    "[AP Sephirite] Not enough regenerated " +
                    "reward shells exist for the remaining " +
                    "saved AP choices."
                );

                selectedLocations.Clear();
                locationNames.Clear();

                return false;
            }

            APDebug.Log(
                $"[AP Sephirite] Restored " +
                $"{selectedLocations.Count} saved choices " +
                $"for {identity}."
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

        public static bool TryPrepareArchipelagoSephiriteChoices(
            Sephirite sephirite,
            GameObject actor,
            out int stageProgress,
            out string poolName,
            out List<string> locationNames)
        {
            stageProgress = -1;
            poolName = null;
            locationNames = new List<string>();

            if (sephirite == null)
                return false;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Warning(
                    "[AP Sephirite] Archipelago is disconnected. " +
                    "This AP-position Sephirite has been blocked " +
                    "instead of falling back to vanilla."
                );

                return true;
            }

            if (!TryGetArchipelagoSephiritePool(
                actor,
                out stageProgress,
                out poolName))
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Error(
                    "[AP Sephirite] Could not determine the current " +
                    "Sephirite floor pool. This AP-position Sephirite " +
                    "has been blocked instead of falling back to vanilla."
                );

                return true;
            }

            List<long> selectedLocations;
            List<string> restoredLocationNames;

            bool hasSavedChoices =
                HasSavedArchipelagoSephiriteChoices(
                    sephirite
                );

            bool restoredSavedChoices =
                TryLoadSavedArchipelagoSephiriteChoices(
                    sephirite,
                    apSession,
                    poolName,
                    out selectedLocations,
                    out restoredLocationNames
                );

            if (hasSavedChoices &&
                !restoredSavedChoices)
            {
                blockedArchipelagoSephiriteRuntimeInstances.Add(
                    sephirite.GetInstanceID()
                );

                MelonLogger.Error(
                    "[AP Sephirite] Saved AP choices exist, " +
                    "but they could not be restored. " +
                    "This Sephirite has been blocked."
                );

                return true;
            }

            int amountToShow;

            if (restoredSavedChoices)
            {
                blockedArchipelagoSephiriteRuntimeInstances.Remove(
                    sephirite.GetInstanceID()
                );

                locationNames = restoredLocationNames;

                amountToShow =
                    selectedLocations.Count;

                if (amountToShow == 0)
                {
                    MelonLogger.Msg(
                        "[AP Sephirite] All saved choices are " +
                        "already checked. Resolving this " +
                        "Sephirite without another reward."
                    );

                    int resolvedRuntimeInstanceId =
                        sephirite.GetInstanceID();

                    if (!MarkArchipelagoSephiriteClaimed(
                            sephirite))
                    {
                        blockedArchipelagoSephiriteRuntimeInstances.Add(
                            resolvedRuntimeInstanceId
                        );

                        MelonLogger.Error(
                            "[AP Sephirite] All saved choices are " +
                            "already checked, but Claimed state " +
                            "could not be persisted. " +
                            "This Sephirite has been blocked."
                        );

                        return true;
                    }

                    blockedArchipelagoSephiriteRuntimeInstances.Remove(
                        resolvedRuntimeInstanceId
                    );

                    activeArchipelagoSephiriteRuntimeInstances.Add(
                        resolvedRuntimeInstanceId
                    );

                    return true;
                }
            }
            else
            {
                // First time this Sephirite has received
                // AP choices.
                List<long> missingChecks =
                    GetMissingArchipelagoSephiriteChecks(
                        apSession,
                        poolName
                    );

                // AP position still consumes its position in the
                // sequence, but becomes vanilla if this floor's
                // AP pool is exhausted.
                if (missingChecks.Count == 0)
                {
                    int vanillaRuntimeInstanceId =
                        sephirite.GetInstanceID();

                    activeArchipelagoSephiriteRuntimeInstances.Remove(
                        vanillaRuntimeInstanceId
                    );

                    blockedArchipelagoSephiriteRuntimeInstances.Remove(
                        vanillaRuntimeInstanceId
                    );

                    return true;
                }

                amountToShow =
                    Math.Min(
                        sephirite.Rewards.Count,
                        missingChecks.Count
                    );

                if (amountToShow <= 0)
                {
                    int vanillaRuntimeInstanceId =
                        sephirite.GetInstanceID();

                    activeArchipelagoSephiriteRuntimeInstances.Remove(
                        vanillaRuntimeInstanceId
                    );

                    blockedArchipelagoSephiriteRuntimeInstances.Remove(
                        vanillaRuntimeInstanceId
                    );

                    return true;
                }

                // Sort first so the seeded selection is stable.
                List<long> available =
                    missingChecks
                        .OrderBy(locationId => locationId)
                        .ToList();

                selectedLocations =
                    new List<long>();

                System.Random random =
                    new System.Random(
                        unchecked(
                            sephirite.CurrentSeed ^
                            0x41A7B9
                        )
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
                    selectedLocations.Add(locationId);

                    string locationName =
                        apSession.Locations
                            .GetLocationNameFromId(
                                locationId,
                                "Sephiria"
                            );

                    locationNames.Add(locationName);
                }

                if (!SaveArchipelagoSephiriteChoices(
                    sephirite,
                    selectedLocations))
                {
                    blockedArchipelagoSephiriteRuntimeInstances.Add(
                        sephirite.GetInstanceID()
                    );

                    MelonLogger.Error(
                        "[AP Sephirite] Failed to persist " +
                        "new AP reward choices. " +
                        "This Sephirite has been blocked."
                    );

                    return true;
                }

                blockedArchipelagoSephiriteRuntimeInstances.Remove(
                    sephirite.GetInstanceID()
                );
            }

            // Remove extra vanilla shell choices.
            //
            // Example:
            // 5 normal choices, only 2 AP checks left
            // -> Sephirite now contains exactly 2 choices.
            while (sephirite.rewards.Count >
                   amountToShow)
            {
                sephirite.rewards.RemoveAt(
                    sephirite.rewards.Count - 1
                );
            }

            int runtimeInstanceId =
                sephirite.GetInstanceID();

            activeArchipelagoSephiriteRuntimeInstances.Add(
                runtimeInstanceId
            );

            // Each remaining vanilla reward metadata object is
            // only a shell. Associate it with an AP location.
            for (int i = 0;
                 i < amountToShow;
                 i++)
            {
                int rewardInstanceId =
                    sephirite.rewards[i].instanceID;

                long locationId =
                    selectedLocations[i];

                archipelagoSephiriteLocationByRewardInstanceId[
                    rewardInstanceId
                ] = locationId;

                archipelagoSephiriteLocationNameByRewardInstanceId[
                    rewardInstanceId
                ] = locationNames[i];
            }

            return true;
        }

        public static bool TryGetArchipelagoSephiriteChoice(
            int rewardInstanceId,
            out long locationId,
            out string locationName)
        {
            locationId = 0;
            locationName = null;

            if (!archipelagoSephiriteLocationByRewardInstanceId
                    .TryGetValue(
                        rewardInstanceId,
                        out locationId))
            {
                return false;
            }

            archipelagoSephiriteLocationNameByRewardInstanceId
                .TryGetValue(
                    rewardInstanceId,
                    out locationName
                );

            return true;
        }

        public static bool TryCompleteArchipelagoSephiriteChoice(
            int rewardInstanceId,
            out string locationName)
        {
            locationName = null;

            long locationId;

            if (!TryGetArchipelagoSephiriteChoice(
                    rewardInstanceId,
                    out locationId,
                    out locationName))
            {
                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    "[AP Sephirite] Cannot complete choice: " +
                    "Archipelago is not connected."
                );

                return false;
            }

            try
            {
                apSession.Locations.CompleteLocationChecks(
                    locationId
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    "[AP Sephirite] Failed to complete " +
                    "location check: " +
                    ex.Message
                );

                return false;
            }

            MelonLogger.Msg(
                $"[AP Sephirite] Checked {locationName}."
            );

            return true;
        }

        public static bool TryGetArchipelagoCostumeMissionInfo(
            string costumeId,
            out string description,
            out bool completed,
            out string sentItem)
        {
            description = null;
            completed = false;
            sentItem = null;

            if (!IsArchipelagoSave)
                return false;

            long locationId;

            if (!archipelagoCostumeMissionLocations.TryGetValue(
                    costumeId,
                    out locationId))
            {
                return false;
            }

            if (!archipelagoCostumeMissionDescriptions.TryGetValue(
                    costumeId,
                    out description))
            {
                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return false;
            }

            completed =
                !apSession.Locations
                    .AllMissingLocations
                    .Contains(locationId);

            // Do NOT reveal the randomized reward
            // until this mission has been completed.
            if (!completed)
                return true;

            ArchipelagoScoutedItemData itemData;

            if (archipelagoCostumeMissionScoutedItems.TryGetValue(
                    locationId,
                    out itemData) &&
                itemData != null &&
                !string.IsNullOrEmpty(itemData.ItemName))
            {
                sentItem = itemData.ItemName;

                // Important for multiworld:
                // show who the item was sent to when it
                // wasn't simply our own reward.
                if (!string.IsNullOrEmpty(itemData.PlayerName))
                {
                    sentItem +=
                        " → " +
                        itemData.PlayerName;
                }
            }

            return true;
        }

        public static void ClearArchipelagoSephiriteRuntimeState(
            Sephirite sephirite)
        {
            if (sephirite == null)
                return;

            int runtimeInstanceId =
                sephirite.GetInstanceID();

            foreach (SephiriteRewardMetadata reward
                in sephirite.Rewards)
            {
                archipelagoSephiriteLocationByRewardInstanceId
                    .Remove(reward.instanceID);

                archipelagoSephiriteLocationNameByRewardInstanceId
                    .Remove(reward.instanceID);
            }

            activeArchipelagoSephiriteRuntimeInstances.Remove(
                runtimeInstanceId
            );

            blockedArchipelagoSephiriteRuntimeInstances.Remove(
                runtimeInstanceId
            );

            archipelagoSephiriteRuntimeIdentities.Remove(
                runtimeInstanceId
            );
        }

        public static void PlayArchipelagoSephiriteSelectSound(
            UI_SephiriteRewardPanel panel)
        {
            if (panel == null ||
                sephiriteSelectSoundField == null)
            {
                return;
            }

            object sound =
                sephiriteSelectSoundField.GetValue(panel);

            if (sound == null)
                return;

            MethodInfo playMethod =
                AccessTools.Method(
                    sound.GetType(),
                    "Play"
                );

            if (playMethod == null)
                return;

            playMethod.Invoke(
                sound,
                null
            );
        }

        public static void RestoreVanillaSephiritePanelControls(
            UI_SephiriteRewardPanel panel,
            PlayerAvatar openedAvatar)
        {
            if (panel == null ||
                openedAvatar == null)
            {
                return;
            }

            SetSephiritePanelGroupActive(
                panel,
                sephiriteRerollButtonGroupField,
                true
            );

            bool canConvertToDice =
                openedAvatar.GetCustomStat(
                    ECustomStat.GiveUpRewardDice
                ) > 0;

            SetSephiritePanelGroupActive(
                panel,
                sephiriteConvertRerollDiceButtonGroupField,
                canConvertToDice
            );

            SetSephiritePanelGroupActive(
                panel,
                sephiriteSkipSelectButtonGroupField,
                !canConvertToDice
            );

            if (panel.skipGuide != null)
            {
                panel.skipGuide.SetActive(false);
            }
        }

        private static async Task ScoutArchipelagoSephiriteItems(
            ArchipelagoSession apSession)
        {
            archipelagoSephiriteScoutedItems.Clear();

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return;
            }

            long[] sephiriteLocationIds =
                apSession.Locations
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
                            locationName.Contains(
                                " Sephirite Item "
                            );
                    })
                    .ToArray();

            if (sephiriteLocationIds.Length == 0)
                return;

            try
            {
                var scoutedItems =
                    await apSession.Locations
                        .ScoutLocationsAsync(
                            false,
                            sephiriteLocationIds
                        );

                foreach (var pair in scoutedItems)
                {
                    var scoutedItem = pair.Value;

                    archipelagoSephiriteScoutedItems[
                        pair.Key
                    ] = new ArchipelagoScoutedItemData
                    {
                        ItemName = scoutedItem.ItemDisplayName,

                        PlayerName =
                            scoutedItem.Player != null
                                ? scoutedItem.Player.Alias
                                : "",

                        GameName =
                            scoutedItem.ItemGame,

                        Flags =
                            string.Equals(
                                scoutedItem.ItemGame,
                                "Sephiria",
                                StringComparison.Ordinal
                            ) &&
                            archipelagoMissionUnlockItems.ContainsKey(
                                scoutedItem.ItemDisplayName
                            )
                                ? 2
                                : Convert.ToInt32(
                                    scoutedItem.Flags
                                )
                    };
                }

                APDebug.Log(
                    "[AP Sephirite] Scouted " +
                    archipelagoSephiriteScoutedItems.Count +
                    " Sephirite item locations."
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "[AP Sephirite] Could not scout item names: " +
                    ex.Message
                );
            }
        }

        private static async Task ScoutArchipelagoCostumeMissionItems(
            ArchipelagoSession apSession)
        {
            archipelagoCostumeMissionScoutedItems.Clear();

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return;
            }

            long[] locationIds =
                archipelagoCostumeMissionLocations
                    .Values
                    .ToArray();

            if (locationIds.Length == 0)
                return;

            try
            {
                var scoutedItems =
                    await apSession.Locations
                        .ScoutLocationsAsync(
                            false,
                            locationIds
                        );

                foreach (var pair in scoutedItems)
                {
                    var scoutedItem = pair.Value;

                    archipelagoCostumeMissionScoutedItems[
                        pair.Key
                    ] = new ArchipelagoScoutedItemData
                    {
                        ItemName =
                            scoutedItem.ItemDisplayName,

                        PlayerName =
                            scoutedItem.Player != null
                                ? scoutedItem.Player.Alias
                                : "",

                        GameName =
                            scoutedItem.ItemGame,

                        Flags =
                            Convert.ToInt32(
                                scoutedItem.Flags
                            )
                    };
                }

                APDebug.Log(
                    "[AP Costume Mission] Scouted " +
                    archipelagoCostumeMissionScoutedItems.Count +
                    " costume mission locations."
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "[AP Costume Mission] Could not scout " +
                    "costume mission items: " +
                    ex.Message
                );
            }
        }

        private static async Task ScoutArchipelagoDestinyInscriptionItems(
            ArchipelagoSession apSession)
        {
            archipelagoDestinyInscriptionScoutedItems.Clear();

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return;
            }

            long[] locationIds =
                archipelagoDestinyInscriptionLocationsByTreeShopId
                    .Values
                    .ToArray();

            if (locationIds.Length == 0)
                return;

            try
            {
                var scoutedItems =
                    await apSession.Locations
                        .ScoutLocationsAsync(
                            false,
                            locationIds
                        );

                foreach (var pair in scoutedItems)
                {
                    var scoutedItem = pair.Value;

                    archipelagoDestinyInscriptionScoutedItems[
                        pair.Key
                    ] = new ArchipelagoScoutedItemData
                    {
                        ItemName =
                            scoutedItem.ItemDisplayName,

                        PlayerName =
                            scoutedItem.Player != null
                                ? scoutedItem.Player.Alias
                                : "",

                        GameName =
                            scoutedItem.ItemGame,

                        Flags =
                            Convert.ToInt32(
                                scoutedItem.Flags
                            )
                    };
                }

                APDebug.Log(
                    "[AP Tree] Scouted " +
                    archipelagoDestinyInscriptionScoutedItems.Count +
                    " Destiny Inscription locations."
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "[AP Tree] Could not scout Destiny " +
                    "Inscription items: " +
                    ex.Message
                );
            }
        }

        public static bool TryGetArchipelagoDestinyInscriptionScoutedDisplay(
            int treeShopItemId,
            out string itemName,
            out string itemDescription)
        {
            itemName = null;
            itemDescription = null;

            long locationId;

            if (!archipelagoDestinyInscriptionLocationsByTreeShopId
                    .TryGetValue(
                        treeShopItemId,
                        out locationId))
            {
                return false;
            }

            ArchipelagoScoutedItemData itemData;

            if (!archipelagoDestinyInscriptionScoutedItems
                    .TryGetValue(
                        locationId,
                        out itemData))
            {
                return false;
            }

            if (itemData == null ||
                string.IsNullOrEmpty(itemData.ItemName))
            {
                return false;
            }

            itemName = itemData.ItemName;

            // Same classification descriptions used
            // by the Sephirite AP rewards.
            string classificationDescription;

            if ((itemData.Flags & 1) != 0)
            {
                classificationDescription =
                    "This seems important.";
            }
            else if ((itemData.Flags & 2) != 0)
            {
                classificationDescription =
                    "This seems useful.";
            }
            else if ((itemData.Flags & 4) != 0)
            {
                classificationDescription =
                    "This seems dangerous.";
            }
            else
            {
                classificationDescription =
                    "This seems ordinary.";
            }

            string recipientText =
                itemData.ItemName;

            if (!string.IsNullOrEmpty(
                    itemData.PlayerName))
            {
                recipientText +=
                    " for " +
                    itemData.PlayerName;
            }

            if (!string.IsNullOrEmpty(
                    itemData.GameName))
            {
                recipientText +=
                    " (" +
                    itemData.GameName +
                    ")";
            }

            itemDescription =
                recipientText +
                "\n" +
                classificationDescription;

            return true;
        }

        public static bool TryGetArchipelagoSephiriteScoutedDisplay(
            long locationId,
            out string itemName,
            out string itemDescription)
        {
            itemName = null;
            itemDescription = null;

            ArchipelagoScoutedItemData itemData;

            if (!archipelagoSephiriteScoutedItems.TryGetValue(
                    locationId,
                    out itemData))
            {
                return false;
            }

            if (itemData == null ||
                string.IsNullOrEmpty(itemData.ItemName))
            {
                return false;
            }

            itemName = itemData.ItemName;

            // Archipelago item classification flags:
            // 1 = progression/advancement
            // 2 = useful
            // 4 = trap
            string classificationDescription;

            if ((itemData.Flags & 1) != 0)
            {
                classificationDescription =
                    "This seems important.";
            }
            else if ((itemData.Flags & 2) != 0)
            {
                classificationDescription =
                    "This seems useful.";
            }
            else if ((itemData.Flags & 4) != 0)
            {
                classificationDescription =
                    "This seems dangerous.";
            }
            else
            {
                classificationDescription =
                    "This seems ordinary.";
            }

            string recipientText =
                itemData.ItemName;

            if (!string.IsNullOrEmpty(
                    itemData.PlayerName))
            {
                recipientText +=
                    " for " +
                    itemData.PlayerName;
            }

            if (!string.IsNullOrEmpty(
                    itemData.GameName))
            {
                recipientText +=
                    " (" +
                    itemData.GameName +
                    ")";
            }

            itemDescription =
                recipientText +
                "\n" +
                classificationDescription;

            return true;
        }

        public static void ApplyArchipelagoSephiriteRarity(
            UI_SephiriteRewardElement rewardElement,
            long locationId)
        {
            if (!IsArchipelagoSave ||
                rewardElement == null ||
                rewardElement.bgImage == null)
            {
                return;
            }

            ArchipelagoScoutedItemData itemData;

            if (!archipelagoSephiriteScoutedItems.TryGetValue(
                    locationId,
                    out itemData) ||
                itemData == null)
            {
                return;
            }

            // Archipelago classification flags:
            // 1 = progression
            // 2 = useful
            // 4 = trap
            // 0 = filler
            if ((itemData.Flags & 1) != 0)
            {
                rewardElement.bgImage.sprite =
                    rewardElement.legendBGSprite;

                rewardElement.isGoodItem = true;
            }
            else if ((itemData.Flags & 4) != 0)
            {
                rewardElement.bgImage.sprite =
                    rewardElement.rareBGSprite;

                rewardElement.isGoodItem = true;
            }
            else if ((itemData.Flags & 2) != 0)
            {
                rewardElement.bgImage.sprite =
                    rewardElement.uncummonBGSprite;

                rewardElement.isGoodItem = false;
            }
            else
            {
                rewardElement.bgImage.sprite =
                    rewardElement.cummonBGSprite;

                rewardElement.isGoodItem = false;
            }

            // Clear any rarity effects that were activated by
            // the underlying vanilla shell.
            if (rewardElement.pooled != null)
            {
                foreach (UI_CursorFx effect in rewardElement.pooled)
                {
                    if (effect != null)
                    {
                        effect.gameObject.SetActive(false);
                    }
                }
            }

            // Do not let a legendary vanilla shell leave its
            // legendary animation active on a non-progression AP item.
            if (rewardElement.legendaryFxAnimation != null)
            {
                bool isProgression =
                    (itemData.Flags & 1) != 0;

                rewardElement.legendaryFxAnimation
                    .gameObject
                    .SetActive(isProgression);

                if (isProgression &&
                    rewardElement.legendaryFxAnimation.IsReady)
                {
                    rewardElement.legendaryFxAnimation
                        .SetEntryState();
                }
            }
        }

        public static bool TryGetArchipelagoSephiriteRarity(
            long locationId,
            out EItemRarity rarity)
        {
            rarity = EItemRarity.Common;

            if (!IsArchipelagoSave)
                return false;

            ArchipelagoScoutedItemData itemData;

            if (!archipelagoSephiriteScoutedItems.TryGetValue(
                    locationId,
                    out itemData) ||
                itemData == null)
            {
                return false;
            }

            if ((itemData.Flags & 1) != 0)
            {
                rarity = EItemRarity.Legend;
            }
            else if ((itemData.Flags & 4) != 0)
            {
                rarity = EItemRarity.Rare;
            }
            else if ((itemData.Flags & 2) != 0)
            {
                rarity = EItemRarity.Uncommon;
            }
            else
            {
                rarity = EItemRarity.Common;
            }

            return true;
        }

        public static void ApplyArchipelagoTooltipIcon(
            UnityEngine.UI.Image iconImage)
        {
            if (iconImage == null ||
                iconImage.sprite == null)
            {
                return;
            }

            UnityEngine.Sprite logo =
                GetArchipelagoSephiriteSprite(
                    iconImage.sprite
                );

            if (logo == null)
                return;

            iconImage.sprite = logo;
            iconImage.preserveAspect = true;

            iconImage.rectTransform.sizeDelta =
                new Vector2(
                    iconImage.preferredWidth,
                    iconImage.preferredHeight
                );

            iconImage.material =
                iconImage.defaultMaterial;

            iconImage.rectTransform.rotation =
                Quaternion.identity;
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
        private static HashSet<string> enabledArchipelagoHardModeSettings;
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
            RandomizeAnvilUnlocks = true;
            ProgressiveFloorUnlockCount = 0;
            enabledArchipelagoHardModeSettings = null;
            SapphireMultiplier = 1;
            StartingCostumeId = null;
            StartingWeaponType = null;
            CompletedChapterClears = 0;
            DeathLinkEnabled = false;
            pendingDeathLink = null;
            suppressNextOutgoingDeathLink = false;
            currentArchipelagoSlotName = null;
            deathLinkService = null;
            TrapsEnabled = false;
            trapSyncPending = false;
            cannotCastTrapUntil = 0f;
            cannotDashTrapUntil = 0f;
            cannotNormalAttackTrapUntil = 0f;
            flippedControlsTrapUntil = 0f;
            suppressOutgoingDeathLinkForRunVictory = false;
            suppressOutgoingDeathLinkForForcedFloorDeath = false;
            StartingCintamaniStonePoolCount = 0;
            cintamaniSyncPending = false;

            object ignoreDeathLink;

            while (receivedDeathLinks.TryDequeue(out ignoreDeathLink)) { }

            Chapter2EndingReached = false;
            ArchipelagoGoalPending = false;

            completedWeaponTypes.Clear();
            archipelagoSephiriteRuntimeIdentities.Clear();
            activeArchipelagoSephiriteRuntimeInstances.Clear();
            blockedArchipelagoSephiriteRuntimeInstances.Clear();
            archipelagoSephiriteLocationByRewardInstanceId.Clear();
            archipelagoSephiriteLocationNameByRewardInstanceId.Clear();
            archipelagoSephiriteScoutedItems.Clear();
            archipelagoCostumeMissionScoutedItems.Clear();
            archipelagoDestinyInscriptionScoutedItems.Clear();
            archipelagoReceivedTreeShopCounts.Clear();

            archipelagoReceivedTreeShopCachedItemTotal = -1;
            archipelagoReceivedTreeShopCachedStartingWeapon = null;

            ClearArchipelagoConsoleHistory();
            ClearArchipelagoHintHistory();

            ResetArchipelagoShrineRunState();

            hardModeSyncPending = false;
            permanentItemSyncPending = false;
            startingWeaponInitializationPending = false;
            weaponStandSyncPending = false;

            wasArchipelagoConnected = false;
            disconnectMessageShown = false;

            lock (archipelagoRecentItemsLock)
            {
                pendingArchipelagoRecentItems.Clear();

                archipelagoRecentItemsResetPending = true;
            }
        }

        public static HashSet<EWeaponType> completedWeaponTypes = new HashSet<EWeaponType>();

        private static void QueueArchipelagoRecentItem(
            string firstLine,
            string secondLine)
        {
            if (string.IsNullOrEmpty(firstLine))
                return;

            lock (archipelagoRecentItemsLock)
            {
                pendingArchipelagoRecentItems.Enqueue(
                    new ArchipelagoRecentItemNotification
                    {
                        FirstLine = firstLine,
                        SecondLine = secondLine
                    }
                );
            }
        }

        private static void HandleArchipelagoMessage(
            LogMessage message)
        {
            if (message == null)
                return;

            // -------------------------------------------------
            // Console log
            // 

            if (message.GetType() !=
                typeof(ItemSendLogMessage))
            {
                QueueArchipelagoConsoleLine(
                    message.ToString()
                );
            }


            // -------------------------------------------------
            // Recent Items
            // -------------------------------------------------

            if (message.GetType() !=
                typeof(ItemSendLogMessage))
            {
                return;
            }

            ItemSendLogMessage itemMessage =
                (ItemSendLogMessage)message;

            if (!itemMessage.IsRelatedToActivePlayer ||
                itemMessage.Item == null)
            {
                return;
            }

            string itemName =
                itemMessage.Item.ItemDisplayName;

            // -------------------------------------------------
            // RECEIVED
            // -------------------------------------------------

            if (itemMessage.IsReceiverTheActivePlayer)
            {
                // We found our own item.
                //
                // Do NOT also create a "Sent to myself"
                // notification. Show where in our game the
                // item came from instead.
                if (itemMessage.IsSenderTheActivePlayer)
                {
                    string locationName =
                        itemMessage.Item.LocationDisplayName;

                    QueueArchipelagoRecentItem(
                        $"Received: {itemName}",
                        $"from {locationName}"
                    );

                    MelonLogger.Msg(
                        $"[AP Recent] Received: {itemName} | " +
                        $"from {locationName}"
                    );

                    return;
                }

                // Another player found our item.
                string senderName =
                    itemMessage.Sender != null &&
                    !string.IsNullOrEmpty(
                        itemMessage.Sender.Alias
                    )
                        ? itemMessage.Sender.Alias
                        : "Another Player";

                QueueArchipelagoRecentItem(
                    $"Received: {itemName}",
                    $"from {senderName}"
                );

                MelonLogger.Msg(
                    $"[AP Recent] Received: {itemName} | " +
                    $"from {senderName}"
                );

                return;
            }

            // -------------------------------------------------
            // SENT
            // -------------------------------------------------

            if (itemMessage.IsSenderTheActivePlayer)
            {
                string receiverName =
                    itemMessage.Receiver != null &&
                    !string.IsNullOrEmpty(
                        itemMessage.Receiver.Alias
                    )
                        ? itemMessage.Receiver.Alias
                        : "Another Player";

                QueueArchipelagoRecentItem(
                    $"Sent: {itemName}",
                    $"to {receiverName}"
                );

                MelonLogger.Msg(
                    $"[AP Recent] Sent: {itemName} | " +
                    $"to {receiverName}"
                );
            }
        }

        private static void QueueArchipelagoConsoleLine(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string normalized =
                text.Replace("\r", "");

            string[] lines =
                normalized.Split('\n');

            lock (archipelagoConsoleLock)
            {
                foreach (string rawLine in lines)
                {
                    string line =
                        rawLine.Trim();

                    if (string.IsNullOrEmpty(line))
                        continue;

                    archipelagoConsoleHistory.Add(
                        line
                    );
                }

                while (archipelagoConsoleHistory.Count >
                       ArchipelagoConsoleHistoryLimit)
                {
                    archipelagoConsoleHistory.RemoveAt(0);
                }

                archipelagoConsoleHistoryVersion++;
            }
        }


        public static List<string> GetArchipelagoConsoleHistory()
        {
            lock (archipelagoConsoleLock)
            {
                return new List<string>(
                    archipelagoConsoleHistory
                );
            }
        }


        public static int GetArchipelagoConsoleHistoryVersion()
        {
            lock (archipelagoConsoleLock)
            {
                return archipelagoConsoleHistoryVersion;
            }
        }


        public static void ClearArchipelagoConsoleHistory()
        {
            lock (archipelagoConsoleLock)
            {
                archipelagoConsoleHistory.Clear();

                archipelagoConsoleHistoryVersion++;
            }
        }

        private static void ReplaceArchipelagoHints(
            Hint[] hints)
        {
            List<string> formattedEntries =
                new List<string>();

            if (hints != null)
            {
                foreach (Hint hint in hints)
                {
                    string entry =
                        FormatArchipelagoHint(
                            hint
                        );

                    if (!string.IsNullOrEmpty(
                            entry))
                    {
                        formattedEntries.Add(
                            entry
                        );
                    }
                }
            }

            lock (archipelagoHintLock)
            {
                archipelagoHintEntries.Clear();

                archipelagoHintEntries.AddRange(
                    formattedEntries
                );

                archipelagoHintHistoryVersion++;
            }
        }

        public static List<string> GetArchipelagoHintEntries()
        {
            lock (archipelagoHintLock)
            {
                return new List<string>(
                    archipelagoHintEntries
                );
            }
        }


        public static int GetArchipelagoHintHistoryVersion()
        {
            lock (archipelagoHintLock)
            {
                return archipelagoHintHistoryVersion;
            }
        }


        public static void ClearArchipelagoHintHistory()
        {
            lock (archipelagoHintLock)
            {
                archipelagoHintEntries.Clear();

                archipelagoHintHistoryVersion++;
            }
        }

        public static string FormatArchipelagoHint(
            Hint hint)
        {
            if (hint == null)
                return "";

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null)
            {
                return
                    (hint.Found
                        ? "<color=#6BE675>FOUND</color>"
                        : "NOT FOUND") +
                    ": Item " +
                    hint.ItemId +
                    "\nLocation: " +
                    hint.LocationId;
            }


            // -------------------------------------------------
            // Player information
            // -------------------------------------------------

            PlayerInfo receivingPlayer =
                apSession.Players.GetPlayerInfo(
                    hint.ReceivingPlayer
                );

            PlayerInfo findingPlayer =
                apSession.Players.GetPlayerInfo(
                    hint.FindingPlayer
                );


            string receivingPlayerName =
                apSession.Players.GetPlayerAlias(
                    hint.ReceivingPlayer
                );

            if (string.IsNullOrEmpty(
                    receivingPlayerName))
            {
                receivingPlayerName =
                    "Player " +
                    hint.ReceivingPlayer;
            }


            string findingPlayerName =
                apSession.Players.GetPlayerAlias(
                    hint.FindingPlayer
                );

            if (string.IsNullOrEmpty(
                    findingPlayerName))
            {
                findingPlayerName =
                    "Player " +
                    hint.FindingPlayer;
            }


            // -------------------------------------------------
            // Resolve the item using the receiving player's game.
            // -------------------------------------------------

            string itemGame =
                receivingPlayer != null
                    ? receivingPlayer.Game
                    : null;

            string itemName = null;

            if (!string.IsNullOrEmpty(
                    itemGame))
            {
                itemName =
                    apSession.Items.GetItemName(
                        hint.ItemId,
                        itemGame
                    );
            }

            if (string.IsNullOrEmpty(
                    itemName))
            {
                itemName =
                    "Item " +
                    hint.ItemId;
            }


            // -------------------------------------------------
            // Resolve the location using the finding player's game.
            // -------------------------------------------------

            string locationGame =
                findingPlayer != null
                    ? findingPlayer.Game
                    : null;

            string locationName = null;

            if (!string.IsNullOrEmpty(
                    locationGame))
            {
                locationName =
                    apSession.Locations
                        .GetLocationNameFromId(
                            hint.LocationId,
                            locationGame
                        );
            }

            if (string.IsNullOrEmpty(
                    locationName))
            {
                locationName =
                    "Location " +
                    hint.LocationId;
            }

            // -------------------------------------------------
            // Display
            // -------------------------------------------------

            string foundText =
                hint.Found
                    ? "<color=#6BE675>FOUND</color>"
                    : "NOT FOUND";

            string result =
                foundText +
                ": " +
                itemName +
                "  |  " +
                locationName;


            // Only show routing when the item crosses players.
            if (hint.FindingPlayer !=
                hint.ReceivingPlayer)
            {
                result +=
                    "  |  " +
                    findingPlayerName +
                    " -> " +
                    receivingPlayerName;
            }


            // Only append entrance information when present.
            if (!string.IsNullOrEmpty(
                    hint.Entrance))
            {
                result +=
                    "  |  Via: " +
                    hint.Entrance;
            }

            return result;
        }

        private static bool EnsureArchipelagoRecentItemsHud()
        {
            if (archipelagoRecentItemsHudRoot != null &&
                archipelagoRecentItemCards.Count ==
                    ArchipelagoRecentItemsMaxVisible)
            {
                return true;
            }

            if (UIManager.Instance == null)
                return false;

            UI_SystemMessage systemMessage =
                UIManager.Instance
                    .GetElement<UI_SystemMessage>();

            if (systemMessage == null ||
                systemMessage.transform.parent == null)
            {
                return false;
            }

            Transform uiParent =
                systemMessage.transform.parent;

            // If the previous scene destroyed the old UI,
            // throw away the stale card references.
            archipelagoRecentItemCards.Clear();

            archipelagoRecentItemsHudRoot =
                new GameObject(
                    "ArchipelagoRecentItemsHUD",
                    typeof(RectTransform)
                );

            RectTransform rootRect =
                archipelagoRecentItemsHudRoot
                    .GetComponent<RectTransform>();

            rootRect.SetParent(
                uiParent,
                false
            );

            rootRect.anchorMin =
                new Vector2(1f, 1f);

            rootRect.anchorMax =
                new Vector2(1f, 1f);

            rootRect.pivot =
                new Vector2(1f, 1f);

            rootRect.sizeDelta =
                new Vector2(
                    150f,
                    1f
                );

            // Position below the quest display
            rootRect.anchoredPosition =
                new Vector2(
                    -12f,
                    -132f
                );

            for (int i = 0;
                 i < ArchipelagoRecentItemsMaxVisible;
                 i++)
            {
                GameObject cardObject =
                    new GameObject(
                        "RecentItemCard_" + i,
                        typeof(RectTransform),
                        typeof(Image)
                    );

                RectTransform cardRect =
                    cardObject.GetComponent<RectTransform>();

                cardRect.SetParent(
                    rootRect,
                    false
                );

                cardRect.anchorMin =
                    new Vector2(1f, 1f);

                cardRect.anchorMax =
                    new Vector2(1f, 1f);

                cardRect.pivot =
                    new Vector2(1f, 1f);

                cardRect.sizeDelta =
                    new Vector2(
                        150f,
                        20f
                    );

                Image background =
                    cardObject.GetComponent<Image>();

                background.raycastTarget = false;


                // ---------------------------------------------
                // Main line
                // ---------------------------------------------

                GameObject mainTextObject =
                    new GameObject(
                        "MainText",
                        typeof(RectTransform),
                        typeof(TextMeshProUGUI)
                    );

                RectTransform mainRect =
                    mainTextObject.GetComponent<RectTransform>();

                mainRect.SetParent(
                    cardRect,
                    false
                );

                mainRect.anchorMin =
                    new Vector2(0f, 1f);

                mainRect.anchorMax =
                    new Vector2(1f, 1f);

                mainRect.pivot =
                    new Vector2(0.5f, 1f);

                TextMeshProUGUI mainText =
                    mainTextObject
                        .GetComponent<TextMeshProUGUI>();

                mainText.fontSize = 8f;

                mainText.alignment =
                    TextAlignmentOptions.TopRight;

                mainText.enableWordWrapping = true;
                mainText.richText = false;
                mainText.raycastTarget = false;


                // ---------------------------------------------
                // Source line
                // ---------------------------------------------

                GameObject sourceTextObject =
                    new GameObject(
                        "SourceText",
                        typeof(RectTransform),
                        typeof(TextMeshProUGUI)
                    );

                RectTransform sourceRect =
                    sourceTextObject
                        .GetComponent<RectTransform>();

                sourceRect.SetParent(
                    cardRect,
                    false
                );

                sourceRect.anchorMin =
                    new Vector2(0f, 1f);

                sourceRect.anchorMax =
                    new Vector2(1f, 1f);

                sourceRect.pivot =
                    new Vector2(0.5f, 1f);

                TextMeshProUGUI sourceText =
                    sourceTextObject
                        .GetComponent<TextMeshProUGUI>();

                sourceText.fontSize = 6.5f;

                sourceText.alignment =
                    TextAlignmentOptions.TopRight;

                sourceText.enableWordWrapping = true;
                sourceText.richText = false;
                sourceText.raycastTarget = false;


                ArchipelagoRecentItemCard card =
                    new ArchipelagoRecentItemCard
                    {
                        Root = cardObject,
                        Rect = cardRect,
                        Background = background,
                        MainText = mainText,
                        SourceText = sourceText
                    };

                cardObject.SetActive(false);

                archipelagoRecentItemCards.Add(card);
            }

            archipelagoRecentItemsHudRoot.SetActive(false);

            return true;
        }

        private static void UpdateArchipelagoRecentItemsHud()
        {
            bool changed = false;

            bool suppressHud =
                ShouldSuppressArchipelagoRecentItemsHud();


            // -------------------------------------------------
            // Reset / add new notifications
            // -------------------------------------------------

            lock (archipelagoRecentItemsLock)
            {
                if (archipelagoRecentItemsResetPending)
                {
                    pendingArchipelagoRecentItems.Clear();
                    visibleArchipelagoRecentItems.Clear();

                    archipelagoRecentItemsResetPending = false;

                    changed = true;
                }


                // Do not start new Recent Item notifications
                // while the HUD is temporarily hidden.
                //
                // They stay queued and will appear normally
                // once the suppressed UI closes.
                if (!suppressHud)
                {
                    while (pendingArchipelagoRecentItems.Count > 0)
                    {
                        ArchipelagoRecentItemNotification notification =
                            pendingArchipelagoRecentItems.Dequeue();

                        notification.ExpiresAt =
                            Time.unscaledTime +
                            ArchipelagoRecentItemLifetime;

                        // Newest first.
                        visibleArchipelagoRecentItems.Insert(
                            0,
                            notification
                        );

                        while (visibleArchipelagoRecentItems.Count >
                               ArchipelagoRecentItemsMaxVisible)
                        {
                            visibleArchipelagoRecentItems.RemoveAt(
                                visibleArchipelagoRecentItems.Count - 1
                            );
                        }

                        changed = true;
                    }
                }
            }


            // -------------------------------------------------
            // Temporarily hidden
            // -------------------------------------------------

            if (suppressHud)
            {
                // Pause the lifetime of anything that was already
                // visible so it can continue afterward instead of
                // expiring behind the Destiny Inscription.
                for (int i = 0;
                     i < visibleArchipelagoRecentItems.Count;
                     i++)
                {
                    visibleArchipelagoRecentItems[i].ExpiresAt +=
                        Time.unscaledDeltaTime;
                }

                if (archipelagoRecentItemsHudRoot != null)
                {
                    archipelagoRecentItemsHudRoot.SetActive(
                        false
                    );
                }

                return;
            }


            // -------------------------------------------------
            // Remove expired notifications
            // -------------------------------------------------

            for (int i =
                    visibleArchipelagoRecentItems.Count - 1;
                 i >= 0;
                 i--)
            {
                if (Time.unscaledTime >=
                    visibleArchipelagoRecentItems[i].ExpiresAt)
                {
                    visibleArchipelagoRecentItems.RemoveAt(i);

                    changed = true;
                }
            }


            // -------------------------------------------------
            // Nothing visible
            // -------------------------------------------------

            if (visibleArchipelagoRecentItems.Count == 0)
            {
                if (archipelagoRecentItemsHudRoot != null)
                {
                    archipelagoRecentItemsHudRoot.SetActive(false);
                }

                return;
            }


            // -------------------------------------------------
            // Make sure UI exists
            // -------------------------------------------------

            if (!EnsureArchipelagoRecentItemsHud())
                return;

            archipelagoRecentItemsHudRoot.SetActive(true);

            if (!changed)
                return;


            // -------------------------------------------------
            // Build separate cards
            // -------------------------------------------------

            const float cardWidth = 150f;
            const float textWidth = 140f;

            const float topPadding = 3f;
            const float bottomPadding = 3f;
            const float textGap = 1f;
            const float cardGap = 3f;

            float yOffset = 0f;

            for (int i = 0;
                 i < archipelagoRecentItemCards.Count;
                 i++)
            {
                ArchipelagoRecentItemCard card =
                    archipelagoRecentItemCards[i];

                if (i >= visibleArchipelagoRecentItems.Count)
                {
                    card.Root.SetActive(false);
                    continue;
                }

                ArchipelagoRecentItemNotification notification =
                    visibleArchipelagoRecentItems[i];

                card.Root.SetActive(true);

                card.MainText.text =
                    notification.FirstLine ?? "";

                card.SourceText.text =
                    notification.SecondLine ?? "";


                // ---------------------------------------------
                // Measure main text
                // ---------------------------------------------

                Vector2 mainPreferred =
                    card.MainText.GetPreferredValues(
                        card.MainText.text,
                        textWidth,
                        0f
                    );

                float mainHeight =
                    mainPreferred.y;


                // ---------------------------------------------
                // Measure source text
                // ---------------------------------------------

                float sourceHeight = 0f;

                if (!string.IsNullOrEmpty(
                        card.SourceText.text))
                {
                    Vector2 sourcePreferred =
                        card.SourceText.GetPreferredValues(
                            card.SourceText.text,
                            textWidth,
                            0f
                        );

                    sourceHeight =
                        sourcePreferred.y;
                }


                // ---------------------------------------------
                // Card height
                // ---------------------------------------------

                float cardHeight =
                    topPadding +
                    mainHeight +
                    (
                        sourceHeight > 0f
                            ? textGap + sourceHeight
                            : 0f
                    ) +
                    bottomPadding;


                // ---------------------------------------------
                // Card position
                // ---------------------------------------------

                card.Rect.sizeDelta =
                    new Vector2(
                        cardWidth,
                        cardHeight
                    );

                card.Rect.anchoredPosition =
                    new Vector2(
                        0f,
                        -yOffset
                    );


                // ---------------------------------------------
                // Main text position
                // ---------------------------------------------

                RectTransform mainRect =
                    card.MainText.rectTransform;

                mainRect.sizeDelta =
                    new Vector2(
                        -10f,
                        mainHeight
                    );

                mainRect.anchoredPosition =
                    new Vector2(
                        0f,
                        -topPadding
                    );


                // ---------------------------------------------
                // Source text position
                // ---------------------------------------------

                RectTransform sourceRect =
                    card.SourceText.rectTransform;

                sourceRect.sizeDelta =
                    new Vector2(
                        -10f,
                        sourceHeight
                    );

                sourceRect.anchoredPosition =
                    new Vector2(
                        0f,
                        -(
                            topPadding +
                            mainHeight +
                            textGap
                        )
                    );


                // ---------------------------------------------
                // Subtle visual priority
                // ---------------------------------------------

                float backgroundAlpha;
                float mainAlpha;
                float sourceAlpha;

                if (i == 0)
                {
                    backgroundAlpha = 0.42f;
                    mainAlpha = 1f;
                    sourceAlpha = 0.78f;
                }
                else if (i == 1)
                {
                    backgroundAlpha = 0.30f;
                    mainAlpha = 0.82f;
                    sourceAlpha = 0.62f;
                }
                else
                {
                    backgroundAlpha = 0.22f;
                    mainAlpha = 0.68f;
                    sourceAlpha = 0.50f;
                }

                card.Background.color =
                    new UnityEngine.Color(
                        0f,
                        0f,
                        0f,
                        backgroundAlpha
                    );

                card.MainText.color =
                    new UnityEngine.Color(
                        1f,
                        1f,
                        1f,
                        mainAlpha
                    );

                card.SourceText.color =
                    new UnityEngine.Color(
                        0.85f,
                        0.85f,
                        0.85f,
                        sourceAlpha
                    );


                // Next card goes underneath this one.
                yOffset +=
                    cardHeight +
                    cardGap;
            }
        }

        public static async Task<bool> ConnectToArchipelago(
            string host,
            int port,
            string slotName,
            string password)
        {
            ArchipelagoSession apSession = ArchipelagoSessionFactory.CreateSession(host, port);

            session = apSession;

            apSession.MessageLog.OnMessageReceived +=
                HandleArchipelagoMessage;

            apSession.Items.ItemReceived += helper =>
            {
                while (helper.Any())
                {
                    helper.DequeueItem();
                }

                QueueHardModeSync();
                QueuePermanentItemSync();
                QueueTrapSync();
                QueueArchipelagoCintamaniSync();
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

                // =================================================
                // Persistent AP hints
                // =================================================

                ClearArchipelagoHintHistory();

                try
                {
                    apSession.DataStorage.TrackHints(
                        hints =>
                        {
                            // Ignore a callback from an old session
                            // after reconnecting to a new one.
                            if (!object.ReferenceEquals(
                                    session,
                                    apSession))
                            {
                                return;
                            }

                            ReplaceArchipelagoHints(
                                hints
                            );
                        },
                        true
                    );
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning(
                        "[AP Hints] Could not start hint tracking: " +
                        ex.Message
                    );
                }

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

                RandomizeAnvilUnlocks =
                    Convert.ToBoolean(
                        loginSuccess.SlotData[
                            "randomize_anvil_unlocks"
                        ]
                    );

                ProgressiveFloorUnlockCount =
                    Convert.ToInt32(
                        loginSuccess.SlotData[
                            "progressive_floor_unlock_count"
                        ]
                    );

                enabledArchipelagoHardModeSettings = null;

                if (loginSuccess.SlotData.ContainsKey(
                        "enabled_hard_mode_settings"))
                {
                    object rawSettings =
                        loginSuccess.SlotData[
                            "enabled_hard_mode_settings"
                        ];

                    System.Collections.IEnumerable settings =
                        rawSettings as System.Collections.IEnumerable;

                    if (settings != null && !(rawSettings is string))
                    {
                        enabledArchipelagoHardModeSettings =
                            new HashSet<string>(StringComparer.Ordinal);

                        foreach (object setting in settings)
                        {
                            string settingName = Convert.ToString(setting);

                            if (!string.IsNullOrWhiteSpace(settingName))
                            {
                                enabledArchipelagoHardModeSettings.Add(
                                    settingName
                                );
                            }
                        }

                        MelonLogger.Msg(
                            "[AP Hard Mode] Enabled settings: " +
                            enabledArchipelagoHardModeSettings.Count
                        );
                    }
                    else
                    {
                        MelonLogger.Warning(
                            "[AP Hard Mode] Invalid settings list received."
                        );
                    }
                }

                StartingCintamaniStonePoolCount =
                    loginSuccess.SlotData.ContainsKey(
                        "starting_cintamani_stones"
                    )
                        ? Convert.ToInt32(
                            loginSuccess.SlotData[
                                "starting_cintamani_stones"
                            ]
                        )
                        : 0;

                StartingCintamaniStonePoolCount =
                    Math.Max(
                        0,
                        Math.Min(
                            2,
                            StartingCintamaniStonePoolCount
                        )
                    );

                TrapsEnabled =
                    loginSuccess.SlotData.ContainsKey(
                        "traps"
                    ) &&
                    Convert.ToBoolean(
                        loginSuccess.SlotData[
                            "traps"
                        ]
                    );

                SapphireMultiplier =
                    Convert.ToInt32(
                        loginSuccess.SlotData[
                            "sapphire_multiplier"
                        ]
                    );

                SapphireMultiplier =
                    Math.Max(
                        1,
                        Math.Min(
                            10,
                            SapphireMultiplier
                        )
                    );

                StartingCostumeId =
                    Convert.ToString(
                        loginSuccess.SlotData["starting_costume"]
                    );

                MelonLogger.Msg(
                    $"AP Starting Costume: {StartingCostumeId}"
                );

                string startingWeaponName =
                    Convert.ToString(
                        loginSuccess.SlotData["starting_weapon"]
                    );

                if (Enum.TryParse(
                        startingWeaponName,
                        out EWeaponType startingWeaponType))
                {
                    StartingWeaponType = startingWeaponType;

                    MelonLogger.Msg(
                        $"AP Starting Weapon: {StartingWeaponType}"
                    );
                }
                else
                {
                    StartingWeaponType = null;

                    MelonLogger.Error(
                        $"Invalid AP starting weapon: {startingWeaponName}"
                    );
                }

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

                DeathLinkEnabled =
                    Convert.ToBoolean(
                        loginSuccess.SlotData["death_link"]
                    );

                currentArchipelagoSlotName =
                    slotName;

                ConfigureArchipelagoDeathLink(
                    apSession
                );

                MelonLogger.Msg(
                    "[AP DeathLink] " +
                    (DeathLinkEnabled
                        ? "Enabled."
                        : "Disabled.")
                );

                await ScoutArchipelagoSephiriteItems(apSession);
                await ScoutArchipelagoCostumeMissionItems(apSession);
                await ScoutArchipelagoDestinyInscriptionItems(apSession);

                QueueHardModeSync();
                QueuePermanentItemSync();
                QueueTrapSync();
                QueueArchipelagoCintamaniSync();

                return true;
            }

            MelonLogger.Msg("Failed to connect to Archipelago");

            session = null;

            return false;
        }

        public static async Task DisconnectFromArchipelago()
        {
            ArchipelagoSession apSession = session as ArchipelagoSession;

            ArchipelagoSephiriteTooltipVisualPatch.RestoreAllTooltipStates();

            ShutdownArchipelagoDeathLink();

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

        public static void QueuePermanentItemSync()
        {
            if (!IsArchipelagoSave)
                return;

            permanentItemSyncPending = true;
        }

        private static void QueueTrapSync()
        {
            if (!IsArchipelagoSave)
                return;

            if (!TrapsEnabled)
                return;

            trapSyncPending = true;
        }

        public static event Action ArchipelagoPermanentRewardsChanged;

        private static void SetSephiritePanelGroupActive(
            UI_SephiriteRewardPanel panel,
            FieldInfo field,
            bool active)
        {
            if (panel == null ||
                field == null)
            {
                return;
            }

            UnityEngine.Component component =
                field.GetValue(panel)
                    as UnityEngine.Component;

            if (component == null)
                return;

            if (component.gameObject.activeSelf != active)
            {
                component.gameObject.SetActive(active);
            }
        }

        private static bool ShouldSuppressArchipelagoRecentItemsHud()
        {
            if (!IsArchipelagoSave)
                return false;

            // Hide Recent Items while the Destiny
            // Inscription interface is open.
            if (ArchipelagoTreeShopModeController
                .IsTreeShopOpen)
            {
                return true;
            }

            return false;
        }

        public override void OnUpdate()
        {
            UpdateArchipelagoRecentItemsHud();
            UpdateArchipelagoDeathLink();
            UpdateArchipelagoTraps();
            UpdateArchipelagoCintamani();

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

            if (IsArchipelagoSave &&
                UIManager.Instance != null)
            {
                UI_SephiriteRewardPanel panel =
                    UIManager.Instance
                        .GetElement<UI_SephiriteRewardPanel>();

                if (panel != null &&
                    panel.sephirite != null &&
                    (
                        IsActiveArchipelagoSephirite(
                            panel.sephirite
                        ) ||
                        IsBlockedArchipelagoSephirite(
                            panel.sephirite
                        )
                    ))
                {
                    SetSephiritePanelGroupActive(
                        panel,
                        sephiriteRerollButtonGroupField,
                        false
                    );

                    SetSephiritePanelGroupActive(
                        panel,
                        sephiriteSkipSelectButtonGroupField,
                        false
                    );

                    SetSephiritePanelGroupActive(
                        panel,
                        sephiriteConvertRerollDiceButtonGroupField,
                        false
                    );

                    if (panel.skipGuide != null)
                    {
                        panel.skipGuide.SetActive(false);
                    }
                }
            }

            if (permanentItemSyncPending && SaveManager.Current != null)
            {
                permanentItemSyncPending = false;

                ApplyArchipelagoPermanentItemState();
            }


            // -------------------------------------------------
            // Archipelago weapon initialization
            // -------------------------------------------------

            if (startingWeaponInitializationPending)
            {
                if (TryInitializeArchipelagoStartingWeapon())
                {
                    startingWeaponInitializationPending = false;

                    weaponStandSyncPending = true;
                }
            }


            // -------------------------------------------------
            // Archipelago weapon stands
            // -------------------------------------------------

            if (weaponStandSyncPending)
            {
                // This is a one-shot refresh request.
                //
                // If the town weapon stands are not loaded yet,
                // do not retry Resources.FindObjectsOfTypeAll every frame.
                // TownWeaponStand.Awake will queue another refresh when
                // the stands actually load.
                weaponStandSyncPending = false;

                RefreshArchipelagoWeaponStands();
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
            bool declineChanged = false;
            bool mindBurdenChanged = false;

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

                bool modifierEnabled =
                    enabledArchipelagoHardModeSettings == null ||
                    (
                        reductionItemName != null &&
                        enabledArchipelagoHardModeSettings.Contains(
                            reductionItemName.Replace(" Reduction", "")
                        )
                    );

                int desiredLevel = modifierEnabled
                    ? Math.Max(0, shardMaxLevel - reductionCount)
                    : 0;

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

                if (shardKey == "DECLINE")
                {
                    declineChanged = true;
                }

                if (shardKey == "MINDBURDEN")
                {
                    mindBurdenChanged = true;
                }

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

                if (DungeonManager.Instance != null &&
                    HardModeManager.Instance != null)
                {
                    HardModeManager.Instance
                        .ActivateHardModeOnServer(
                            DungeonManager.Instance.raceId
                        );

                    if (declineChanged)
                        RefreshArchipelagoDeclineRuntimeStatus();

                    if (mindBurdenChanged)
                        RefreshArchipelagoMindBurdenRuntimeItem();

                    RefreshArchipelagoHardModeIndicator();

                    MelonLogger.Msg(
                        "[AP Hard Mode] Runtime state refreshed."
                    );
                }
            }
        }

        // Permanent Item Stuff

        private static void ApplyArchipelagoPermanentItemState()
        {
            if (!IsArchipelagoSave)
                return;

            if (SaveManager.Current == null)
                return;

            ArchipelagoSession apSession = session as ArchipelagoSession;

            if (apSession == null)
                return;

            SynchronizeArchipelagoSapphireFiller(apSession);
            SynchronizeArchipelagoTalentPoints(apSession);
            SynchronizeArchipelagoPotionStorage(apSession);
            SynchronizeArchipelagoStartingPotion(apSession);
            SynchronizeArchipelagoWillpower(apSession);
            SynchronizeArchipelagoIngenuity(apSession);
            SynchronizeArchipelagoCostumeDyeing(apSession);
            SynchronizeArchipelagoFruitSkewer(apSession);
            SynchronizeArchipelagoWeapons(apSession);
            SynchronizeArchipelagoDestinyUpgrades(apSession);
            SynchronizeArchipelagoPermanentRewards(apSession);
            SynchronizeArchipelagoMissionUnlocks(apSession);

            ArchipelagoPermanentRewardsChanged?.Invoke();

            HashSet<string> ownedCostumeIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            // Starting costume.
            if (!string.IsNullOrEmpty(StartingCostumeId))
            {
                ownedCostumeIds.Add(
                    StartingCostumeId
                );
            }

            // Received costume items.
            foreach (var receivedItem
                in apSession.Items.AllItemsReceived)
            {
                if (receivedItem == null)
                    continue;

                string costumeId;

                if (!archipelagoCostumeItems.TryGetValue(
                        receivedItem.ItemName,
                        out costumeId))
                {
                    continue;
                }

                ownedCostumeIds.Add(
                    costumeId
                );
            }

            // ---------------------------------------------
            // Runtime ownership
            // ---------------------------------------------

            if (CombatManager.Instance != null)
            {
                foreach (KeyValuePair<string, string> costume
                    in archipelagoCostumeItems)
                {
                    string costumeId =
                        costume.Value;

                    bool shouldBeUnlocked =
                        ownedCostumeIds.Contains(
                            costumeId
                        );

                    bool currentlyUnlocked =
                        CostumeDatabase.IsUnlocked(
                            costumeId
                        );

                    if (shouldBeUnlocked &&
                        !currentlyUnlocked)
                    {
                        CostumeDatabase.UnlockCostume(
                            costumeId
                        );

                        APDebug.Log(
                            $"[AP Costume] {costumeId} = True"
                        );
                    }
                    else if (!shouldBeUnlocked &&
                             currentlyUnlocked)
                    {
                        CostumeDatabase.LockCostume(
                            costumeId
                        );

                        APDebug.Log(
                            $"[AP Costume] {costumeId} = False"
                        );
                    }
                }
            }

            // ---------------------------------------------
            // Selected costume
            // ---------------------------------------------
            // PlayerCostume is Sephiria's saved selected
            // costume. Keep it as long as AP owns it.
            //
            // The starting costume is only a fallback if
            // the saved selection is no longer valid.
            // ---------------------------------------------

            bool saveChanged = false;

            string equippedCostume =
                SaveManager.Current.GetString(
                    "PlayerCostume",
                    ""
                );

            if (!ownedCostumeIds.Contains(equippedCostume))
            {
                equippedCostume = StartingCostumeId;

                if (!string.IsNullOrEmpty(equippedCostume))
                {
                    SaveManager.Current.SetString(
                        "PlayerCostume",
                        equippedCostume
                    );

                    saveChanged = true;

                    MelonLogger.Msg(
                        $"[AP Costume] Saved costume was not owned. " +
                        $"Falling back to starting costume: " +
                        $"{equippedCostume}"
                    );
                }
            }

            if (saveChanged)
            {
                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );
            }

            // ---------------------------------------------
            // Runtime equipped costume
            // ---------------------------------------------
            // Sephiria may temporarily spawn a different
            // costume before AP ownership is synchronized.
            //
            // Restore the player's SAVED selected costume,
            // not automatically the starting costume.
            // ---------------------------------------------

            if (CombatManager.Instance != null &&
                CombatManager.Instance.CurrentPlayer != null &&
                !string.IsNullOrEmpty(equippedCostume) &&
                ownedCostumeIds.Contains(equippedCostume))
            {
                PlayerAvatar currentPlayer =
                    CombatManager.Instance.CurrentPlayer;

                CostumeEntity selectedCostume =
                    CostumeDatabase.FindCostumeByID(
                        equippedCostume
                    );

                if (selectedCostume != null)
                {
                    string selectedSkinId =
                        SaveManager.Current.GetString(
                            "PlayerCostume_CurrentSkin_" +
                            equippedCostume,
                            ""
                        );

                    CostumeSkinEntity selectedSkin = null;

                    if (!string.IsNullOrWhiteSpace(
                            selectedSkinId))
                    {
                        selectedSkin =
                            CostumeDatabase.GetCostumeSkinByID(
                                selectedSkinId
                            );
                    }

                    if (selectedSkin == null)
                    {
                        selectedSkin =
                            CostumeDatabase.FindDefaultCostumeSkin(
                                equippedCostume
                            );

                        selectedSkinId =
                            selectedSkin != null
                                ? selectedSkin.skinID
                                : "";
                    }

                    bool wrongRuntimeCostume =
                        !string.Equals(
                            currentPlayer.currentCostume,
                            equippedCostume,
                            StringComparison.Ordinal
                        );

                    bool wrongRuntimeSkin =
                        !string.Equals(
                            currentPlayer.currentCostumeSkin,
                            selectedSkinId,
                            StringComparison.Ordinal
                        );

                    if (wrongRuntimeCostume ||
                        wrongRuntimeSkin)
                    {
                        currentPlayer.EquipCostume(
                            equippedCostume,
                            selectedSkinId
                        );

                        PlayerSpawner playerSpawner =
                            currentPlayer.GetComponent<PlayerSpawner>();

                        if (playerSpawner != null &&
                            playerSpawner.LocalDataStorage != null)
                        {
                            playerSpawner.LocalDataStorage
                                .NetworkdefaultCostume =
                                    equippedCostume;

                            playerSpawner.LocalDataStorage
                                .NetworkdefaultCostumeSkin =
                                    selectedSkinId;

                            SaveManager.CurrentRun?.SetString(
                                $"Player{playerSpawner.currentPlayerIdxForSave}Costume",
                                equippedCostume
                            );

                            SaveManager.CurrentRun?.SetString(
                                $"Player{playerSpawner.currentPlayerIdxForSave}Skin",
                                selectedSkinId
                            );
                        }

                        APDebug.Log(
                            $"[AP Costume] Runtime restored saved costume: " +
                            $"{equippedCostume}"
                        );
                    }
                }
            }
        }

        public static bool TryGetArchipelagoBossRewardInfo(
            string bossName,
            out string apItemName,
            out TreeShopItemEntity treeShopItem,
            out bool unlocked)
        {
            apItemName =
                "Boss Reward Unlock: " + bossName;

            treeShopItem = null;
            unlocked = false;

            int treeShopItemId;

            if (!archipelagoPermanentRewardItems.TryGetValue(
                    apItemName,
                    out treeShopItemId))
            {
                return false;
            }

            treeShopItem =
                TreeShopItemDatabase.FindById(
                    treeShopItemId
                );

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession != null)
            {
                string rewardItemName =
                    apItemName;

                unlocked =
                    apSession.Items.AllItemsReceived.Any(
                        receivedItem =>
                            receivedItem != null &&
                            string.Equals(
                                receivedItem.ItemName,
                                rewardItemName,
                                StringComparison.Ordinal
                            )
                    );
            }

            return true;
        }

        public static bool TryCompleteArchipelagoBossDefeat(int treeShopRewardId)
        {
            if (!IsArchipelagoSave)
                return false;

            long locationId;

            if (!archipelagoBossDefeatLocations.TryGetValue(
                    treeShopRewardId,
                    out locationId))
            {
                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    $"[AP Boss] Cannot check boss location " +
                    $"{locationId}: Archipelago is disconnected."
                );

                return false;
            }

            // This boss location was already completed.
            if (!apSession.Locations
                    .AllMissingLocations
                    .Contains(locationId))
            {
                return true;
            }

            string locationName =
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
                    $"[AP Boss] Failed to check " +
                    $"{locationName}: {ex.Message}"
                );

                return false;
            }

            MelonLogger.Msg(
                $"[AP Boss] Checked {locationName}."
            );

            return true;
        }

        private static void SynchronizeArchipelagoPermanentRewards(
            ArchipelagoSession apSession)
        {
            if (apSession == null)
                return;

            if (CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return;
            }

            PlayerSpawner playerSpawner =
                CombatManager.Instance.CurrentPlayer
                    .GetComponent<PlayerSpawner>();

            if (playerSpawner == null)
                return;

            foreach (KeyValuePair<string, int> reward
                in archipelagoPermanentRewardItems)
            {
                string apItemName =
                    reward.Key;

                int treeShopItemId =
                    reward.Value;

                bool ownsReward =
                    apSession.Items.AllItemsReceived.Any(
                        receivedItem =>
                            receivedItem != null &&
                            string.Equals(
                                receivedItem.ItemName,
                                apItemName,
                                StringComparison.Ordinal
                            )
                    );

                TreeShopItemEntity treeShopItem =
                    TreeShopItemDatabase.FindById(
                        treeShopItemId
                    );

                if (treeShopItem == null ||
                    treeShopItem.items == null)
                {
                    MelonLogger.Warning(
                        $"[AP Permanent Reward] Could not find " +
                        $"TreeShop reward {treeShopItemId} " +
                        $"for {apItemName}."
                    );

                    continue;
                }

                List<int> itemsToUnlock =
                    new List<int>();

                bool changed = false;

                foreach (ItemEntity itemEntity
                    in treeShopItem.items)
                {
                    if (itemEntity == null)
                        continue;

                    bool currentlyUnlocked;

                    if (itemEntity.type == EItemType.Charm)
                    {
                        currentlyUnlocked =
                            playerSpawner.unlockedCharms.Contains(
                                itemEntity.id
                            );
                    }
                    else if (
                        itemEntity.type ==
                            EItemType.StoneTablet)
                    {
                        currentlyUnlocked =
                            playerSpawner
                                .unlockedStoneTablets
                                .Contains(
                                    itemEntity.id
                                );
                    }
                    else
                    {
                        MelonLogger.Warning(
                            $"[AP Permanent Reward] Unsupported " +
                            $"item type {itemEntity.type} for " +
                            $"{apItemName}, ItemID={itemEntity.id}."
                        );

                        continue;
                    }

                    if (ownsReward &&
                        !currentlyUnlocked)
                    {
                        if (NetworkServer.active)
                        {
                            if (itemEntity.type ==
                                EItemType.Charm)
                            {
                                playerSpawner.unlockedCharms.Add(
                                    itemEntity.id
                                );
                            }
                            else
                            {
                                playerSpawner
                                    .unlockedStoneTablets
                                    .Add(
                                        itemEntity.id
                                    );
                            }
                        }
                        else
                        {
                            itemsToUnlock.Add(
                                itemEntity.id
                            );
                        }

                        changed = true;
                    }
                    else if (!ownsReward &&
                             currentlyUnlocked &&
                             NetworkServer.active)
                    {
                        if (itemEntity.type ==
                            EItemType.Charm)
                        {
                            playerSpawner.unlockedCharms.Remove(
                                itemEntity.id
                            );
                        }
                        else
                        {
                            playerSpawner
                                .unlockedStoneTablets
                                .Remove(
                                    itemEntity.id
                                );
                        }

                        changed = true;
                    }
                }

                if (!NetworkServer.active &&
                    itemsToUnlock.Count > 0)
                {
                    playerSpawner.CmdUnlockNewItems(
                        itemsToUnlock.ToArray()
                    );
                }

                if (changed)
                {
                    APDebug.Log(
                        $"[AP Permanent Reward] " +
                        $"{apItemName} = {ownsReward}"
                    );
                }
            }
        }

        public static string GetArchipelagoCostumeMissionName(
            string costumeId)
        {
            string name;

            if (archipelagoCostumeMissionNames.TryGetValue(
                    costumeId,
                    out name))
            {
                return name;
            }

            return costumeId;
        }

        public static string[] GetArchipelagoCostumeMissionOrder()
        {
            return new string[]
            {
                "FireCat",
                "Fox",
                "Frog",
                "Mole",
                "Otter",
                "WhiteWolf",
                "WizardRabbit",
                "Bird",
                "Turtle",
                "ForestCat",
                "Ghost",
                "Skeleton",
                "Bat"
            };
        }

        public static void PrepareArchipelagoCostumeMissionTrackers()
        {
            if (!IsArchipelagoSave)
                return;

            if (SaveManager.Current == null)
                return;

            // Remaining AP costume missions need their
            // vanilla trackers available.
            foreach (string costumeId
                in archipelagoCostumeMissionLocations.Keys)
            {
                SaveManager.Current.SetBool(
                    "Costume_Unlocked_" + costumeId,
                    false
                );
            }

            // These missions were removed from AP, but their
            // vanilla completed flags must also stay false.
            //
            // A true flag causes Sephiria to directly unlock
            // the costume instead of creating a tracker.
            foreach (string costumeId
                in removedArchipelagoCostumeMissionIds)
            {
                SaveManager.Current.SetBool(
                    "Costume_Unlocked_" + costumeId,
                    false
                );
            }
        }

        public static bool TryCompleteArchipelagoCostumeMission(
            string costumeId)
        {
            if (!IsArchipelagoSave)
                return false;

            long locationId;

            if (!archipelagoCostumeMissionLocations.TryGetValue(
                    costumeId,
                    out locationId))
            {
                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    $"[AP Costume Mission] Cannot check " +
                    $"{costumeId}: Archipelago is disconnected."
                );

                return false;
            }

            // Already completed.
            if (!apSession.Locations
                    .AllMissingLocations
                    .Contains(locationId))
            {
                return true;
            }

            string locationName =
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
                    $"[AP Costume Mission] Failed to check " +
                    $"{locationName}: {ex.Message}"
                );

                return false;
            }

            MelonLogger.Msg(
                $"[AP Costume Mission] Checked {locationName}."
            );

            return true;
        }

        public static bool IsArchipelagoCostumeOwned(
            string costumeId)
        {
            if (!IsArchipelagoSave)
                return false;

            if (string.IsNullOrEmpty(costumeId))
                return false;

            // Random starting costume.
            if (string.Equals(
                    StartingCostumeId,
                    costumeId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null)
                return false;

            // Costumes received from Archipelago.
            foreach (var receivedItem
                in apSession.Items.AllItemsReceived)
            {
                if (receivedItem == null)
                    continue;

                string receivedCostumeId;

                if (!archipelagoCostumeItems.TryGetValue(
                        receivedItem.ItemName,
                        out receivedCostumeId))
                {
                    continue;
                }

                if (string.Equals(
                        receivedCostumeId,
                        costumeId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsArchipelagoPermanentRewardTreeShopItem(
            int treeShopItemId)
        {
            return archipelagoPermanentRewardTreeShopIds.Contains(
                treeShopItemId
            );
        }

        // Skill Tree Stuff

        private const string ArchipelagoTalentPointItemName = "+5 Talent Points";
        private const string ArchipelagoPotionStorageItemName = "Potion Storage";
        private const string ArchipelagoStartingPotionItemName = "Starting Potion";
        private const string ArchipelagoWillpowerItemName = "Willpower Talent Unlock";
        private const string ArchipelagoIngenuityItemName = "Ingenuity Talent Unlock";
        private const string ArchipelagoCostumeDyeingItemName = "Costume Dyeing";
        private const string ArchipelagoFruitSkewerItemName = "Fruit Skewer Unlock";

        public static int GetArchipelagoTalentPointItemCount()
        {
            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null)
                return 0;

            return Math.Min(
                10,
                apSession.Items.AllItemsReceived.Count(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoTalentPointItemName,
                            StringComparison.Ordinal
                        )
                )
            );
        }

        public static int GetProgressiveFloorUnlockItemCount()
        {
            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null)
                return 0;

            return apSession.Items.AllItemsReceived.Count(
                item =>
                    item != null &&
                    string.Equals(
                        item.ItemName,
                        "Progressive Floor Unlock",
                        StringComparison.Ordinal
                    )
            );
        }

        public static bool TryGetRequiredFloorUnlockCount(
            int currentStageProgress,
            out int requiredUnlocks)
        {
            switch (currentStageProgress)
            {
                case 1:
                    requiredUnlocks = 1;
                    return true;

                case 2:
                    requiredUnlocks = 2;
                    return true;

                case 3:
                    requiredUnlocks = 3;
                    return true;

                case 4:
                    requiredUnlocks = 4;
                    return true;

                case 6:
                    requiredUnlocks = 5;
                    return true;

                default:
                    requiredUnlocks = 0;
                    return false;
            }
        }

        public static bool IsMainProgressionStage(
            string stageName)
        {
            if (DungeonManager.Instance == null ||
                DungeonManager.Instance.Race == null ||
                DungeonManager.Instance.Race.stages == null ||
                string.IsNullOrEmpty(stageName))
            {
                return false;
            }

            return DungeonManager.Instance.Race.stages.Any(
                stage =>
                    stage != null &&
                    string.Equals(
                        stage.name,
                        stageName,
                        StringComparison.Ordinal
                    )
            );
        }

        private static readonly Dictionary<int, int>
            archipelagoReceivedTreeShopCounts =
                new Dictionary<int, int>();

        private static int
            archipelagoReceivedTreeShopCachedItemTotal = -1;

        private static EWeaponType?
            archipelagoReceivedTreeShopCachedStartingWeapon = null;


        private static int GetReceivedItemCount(
            Dictionary<string, int> receivedItems,
            string itemName)
        {
            int count;

            if (receivedItems.TryGetValue(
                    itemName,
                    out count))
            {
                return count;
            }

            return 0;
        }


        private static int ClampReceivedTreeCount(
            int count,
            int maximum)
        {
            return Math.Min(
                Math.Max(count, 0),
                maximum
            );
        }


        private static bool HasReceivedWeapon(
            Dictionary<string, int> receivedItems,
            EWeaponType weaponType,
            string itemName)
        {
            // The randomized starting weapon is owned even
            // though it was not placed in the AP item pool.
            if (StartingWeaponType.HasValue &&
                StartingWeaponType.Value == weaponType)
            {
                return true;
            }

            return GetReceivedItemCount(
                receivedItems,
                itemName
            ) > 0;
        }


        private static void
            RebuildArchipelagoReceivedTreeShopCounts(
                ArchipelagoSession apSession)
        {
            archipelagoReceivedTreeShopCounts.Clear();

            Dictionary<string, int> receivedItems =
                new Dictionary<string, int>(
                    StringComparer.Ordinal
                );

            foreach (var receivedItem
                in apSession.Items.AllItemsReceived)
            {
                if (receivedItem == null ||
                    string.IsNullOrEmpty(
                        receivedItem.ItemName))
                {
                    continue;
                }

                int currentCount;

                receivedItems.TryGetValue(
                    receivedItem.ItemName,
                    out currentCount
                );

                receivedItems[
                    receivedItem.ItemName
                ] = currentCount + 1;
            }


            // =================================================
            // Normal Destiny Inscription upgrades
            // =================================================

            archipelagoReceivedTreeShopCounts[0] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Tablet Unlock"
                    ),
                    1
                );

            archipelagoReceivedTreeShopCounts[9] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Dice Acquisition"
                    ),
                    1
                );

            archipelagoReceivedTreeShopCounts[10] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Seed Money"
                    ),
                    2
                );

            archipelagoReceivedTreeShopCounts[11] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Chest of Possibilities"
                    ),
                    1
                );

            archipelagoReceivedTreeShopCounts[14] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Wishing Fountain Upgrade"
                    ),
                    1
                );

            archipelagoReceivedTreeShopCounts[16] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Dice Transformation"
                    ),
                    1
                );

            archipelagoReceivedTreeShopCounts[302] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Level Up Restoration"
                    ),
                    4
                );

            archipelagoReceivedTreeShopCounts[306] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Extra Experience Drop"
                    ),
                    1
                );

            archipelagoReceivedTreeShopCounts[309] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "MP Regeneration"
                    ),
                    2
                );


            // =================================================
            // Progressive Dice
            //
            // 1      -> ID 4
            // 2-3    -> ID 303
            // 4-5    -> ID 313
            // =================================================

            int progressiveDice =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Progressive Dice Upgrade"
                    ),
                    5
                );

            archipelagoReceivedTreeShopCounts[4] =
                Math.Min(
                    progressiveDice,
                    1
                );

            archipelagoReceivedTreeShopCounts[303] =
                Math.Min(
                    Math.Max(
                        progressiveDice - 1,
                        0
                    ),
                    2
                );

            archipelagoReceivedTreeShopCounts[313] =
                Math.Min(
                    Math.Max(
                        progressiveDice - 3,
                        0
                    ),
                    2
                );


            // =================================================
            // Progressive Expanding Pouch
            //
            // 1-3 -> ID 304
            // 4-6 -> ID 305
            // =================================================

            int progressivePouch =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Progressive Expanding Pouch"
                    ),
                    6
                );

            archipelagoReceivedTreeShopCounts[304] =
                Math.Min(
                    progressivePouch,
                    3
                );

            archipelagoReceivedTreeShopCounts[305] =
                Math.Min(
                    Math.Max(
                        progressivePouch - 3,
                        0
                    ),
                    3
                );


            // =================================================
            // Progressive Increase HP
            //
            // 1-2 -> ID 311
            // 3-4 -> ID 312
            // =================================================

            int progressiveHp =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Progressive Increase HP"
                    ),
                    4
                );

            archipelagoReceivedTreeShopCounts[311] =
                Math.Min(
                    progressiveHp,
                    2
                );

            archipelagoReceivedTreeShopCounts[312] =
                Math.Min(
                    Math.Max(
                        progressiveHp - 2,
                        0
                    ),
                    2
                );


            // =================================================
            // Progressive Side Bag
            //
            // 1-3 -> ID 314
            // 4-6 -> ID 315
            // =================================================

            int progressiveSideBag =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Progressive Side Bag"
                    ),
                    6
                );

            archipelagoReceivedTreeShopCounts[314] =
                Math.Min(
                    progressiveSideBag,
                    3
                );

            archipelagoReceivedTreeShopCounts[315] =
                Math.Min(
                    Math.Max(
                        progressiveSideBag - 3,
                        0
                    ),
                    3
                );


            // =================================================
            // Progressive Artifact Unlock
            //
            // Bond Artifact remains its own AP item.
            // =================================================

            int progressiveArtifacts =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Progressive Artifact Unlock"
                    ),
                    8
                );

            int[] progressiveArtifactIds =
            {
        1000,
        1001,
        1002,
        1003,
        1004,
        1005,
        1007,
        1008
    };

            for (int i = 0;
                i < progressiveArtifactIds.Length;
                i++)
            {
                archipelagoReceivedTreeShopCounts[
                    progressiveArtifactIds[i]
                ] =
                    progressiveArtifacts > i
                        ? 1
                        : 0;
            }

            archipelagoReceivedTreeShopCounts[1006] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Bond Artifact Unlock"
                    ),
                    1
                );

            // =================================================
            // Mission / reward nodes
            //
            // For Received Upgrades, show the node as received
            // when the actual reward represented by that node
            // has been received, not merely when the mission
            // itself has been unlocked.
            // =================================================

            // Rylie's Gift -> Starting Potion
            archipelagoReceivedTreeShopCounts[1] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Starting Potion"
                    ),
                    1
                );

            // Magic Workshop -> Potion Storage
            archipelagoReceivedTreeShopCounts[5] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Potion Storage"
                    ),
                    1
                );

            // Home Repairs
            archipelagoReceivedTreeShopCounts[6] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Mission Unlock: Home Repairs"
                    ),
                    1
                );

            // Terrain Recovery -> Willpower
            archipelagoReceivedTreeShopCounts[7] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Willpower Talent Unlock"
                    ),
                    1
                );

            // Basement -> Costume Dyeing
            archipelagoReceivedTreeShopCounts[13] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Costume Dyeing"
                    ),
                    1
                );

            // Hide and Seek -> Ingenuity
            archipelagoReceivedTreeShopCounts[18] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Ingenuity Talent Unlock"
                    ),
                    1
                );

            // Fruit Skewer -> Fruit Skewer Unlock
            archipelagoReceivedTreeShopCounts[20] =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "Fruit Skewer Unlock"
                    ),
                    1
                );

            // =================================================
            // Weapon unlocks
            //
            // The randomized starting weapon also counts as
            // received/owned on this page.
            // =================================================

            archipelagoReceivedTreeShopCounts[2] =
                HasReceivedWeapon(
                    receivedItems,
                    EWeaponType.GreatSword,
                    "Greatsword Unlock"
                )
                    ? 1
                    : 0;

            archipelagoReceivedTreeShopCounts[3] =
                HasReceivedWeapon(
                    receivedItems,
                    EWeaponType.Dagger,
                    "Dagger Unlock"
                )
                    ? 1
                    : 0;

            archipelagoReceivedTreeShopCounts[12] =
                HasReceivedWeapon(
                    receivedItems,
                    EWeaponType.Crossbow,
                    "Crossbow Unlock"
                )
                    ? 1
                    : 0;

            archipelagoReceivedTreeShopCounts[17] =
                HasReceivedWeapon(
                    receivedItems,
                    EWeaponType.Katana,
                    "Katana Unlock"
                )
                    ? 1
                    : 0;

            archipelagoReceivedTreeShopCounts[19] =
                HasReceivedWeapon(
                    receivedItems,
                    EWeaponType.Staff,
                    "Staff Unlock"
                )
                    ? 1
                    : 0;


            // =================================================
            // Talent Points
            //
            // Each +5 Talent Points receipt completely fills
            // one vanilla Talent Point node.
            //
            // There are 5 visual nodes but 10 AP items, so the
            // Received Upgrades board is full after receipt #5.
            // =================================================

            int talentPointReceipts =
                ClampReceivedTreeCount(
                    GetReceivedItemCount(
                        receivedItems,
                        "+5 Talent Points"
                    ),
                    5
                );

            int[] talentPointNodeIds =
            {
                300,
                301,
                307,
                308,
                310
            };

            for (int i = 0;
                i < talentPointNodeIds.Length;
                i++)
            {
                archipelagoReceivedTreeShopCounts[
                    talentPointNodeIds[i]
                ] =
                    talentPointReceipts > i
                        ? 5
                        : 0;
            }


            archipelagoReceivedTreeShopCachedItemTotal =
                apSession.Items.AllItemsReceived.Count();

            archipelagoReceivedTreeShopCachedStartingWeapon =
                StartingWeaponType;
        }


        public static bool
            TryGetArchipelagoReceivedTreeShopCount(
                int treeShopItemId,
                out int receivedCount)
        {
            receivedCount = 0;

            if (!IsArchipelagoSave)
                return false;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null)
                return false;

            int currentItemTotal =
                apSession.Items.AllItemsReceived.Count();

            if (currentItemTotal !=
                    archipelagoReceivedTreeShopCachedItemTotal ||
                StartingWeaponType !=
                    archipelagoReceivedTreeShopCachedStartingWeapon)
            {
                RebuildArchipelagoReceivedTreeShopCounts(
                    apSession
                );
            }

            return archipelagoReceivedTreeShopCounts
                .TryGetValue(
                    treeShopItemId,
                    out receivedCount
                );
        }

        public static bool HasArchipelagoTalentAccess()
        {
            if (!IsArchipelagoSave)
            {
                return SwitchManager.GetDestinySwitch(
                    "RebuildHomeClear",
                    fallback: false
                );
            }

            return GetArchipelagoTalentPointItemCount() > 0;
        }

        private static void SynchronizeArchipelagoTalentPoints(
            ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null ||
                CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return;
            }

            int receivedCount =
                Math.Min(
                    10,
                    apSession.Items.AllItemsReceived.Count(
                        item =>
                            item != null &&
                            string.Equals(
                                item.ItemName,
                                ArchipelagoTalentPointItemName,
                                StringComparison.Ordinal
                            )
                    )
                );

            int desiredMaxPassivePoints =
                receivedCount * 5;

            PlayerAvatar player =
                CombatManager.Instance.CurrentPlayer;

            if (player.maxPassivePoint !=
                desiredMaxPassivePoints)
            {
                if (NetworkServer.active)
                {
                    player.NetworkmaxPassivePoint =
                        desiredMaxPassivePoints;
                }
                else
                {
                    player.maxPassivePoint =
                        desiredMaxPassivePoints;
                }

                MelonLogger.Msg(
                    "[AP Talent] " +
                    $"{receivedCount}/10 +5 Talent Points received. " +
                    $"Maximum Talent Points = " +
                    $"{desiredMaxPassivePoints}."
                );
            }

            // Refresh the HUD button immediately if the HUD exists.
            if (UIManager.Instance != null)
            {
                UI_HUDMenu hudMenu =
                    UIManager.Instance.GetElement<UI_HUDMenu>();

                if (hudMenu != null)
                {
                    MethodInfo updatePassiveButton =
                        AccessTools.Method(
                            typeof(UI_HUDMenu),
                            "UpdatePassiveButton"
                        );

                    updatePassiveButton?.Invoke(
                        hudMenu,
                        null
                    );
                }
            }
        }

        private static void SynchronizeArchipelagoPotionStorage(ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null)
            {
                return;
            }

            bool received =
                apSession.Items.AllItemsReceived.Any(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoPotionStorageItemName,
                            StringComparison.Ordinal
                        )
                );

            if (!received)
                return;

            // Permanently unlock the Potion Storage feature.
            if (!SwitchManager.GetDestinySwitch(
                    "PotionStorageUnlocked",
                    fallback: false))
            {
                SwitchManager.SetDestinySwitch(
                    "PotionStorageUnlocked",
                    true,
                    false
                );

                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );

                MelonLogger.Msg(
                    "[AP Mission Reward] Potion Storage unlocked."
                );
            }

            if (CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null ||
                CombatManager.Instance.CurrentPlayer.Inventory == null ||
                !NetworkServer.active)
            {
                return;
            }

            GridInventory inventory =
                CombatManager.Instance.CurrentPlayer.Inventory;

            // Vanilla Magic Workshop gives 2 potion slots.
            // Only raise it to 2; never reduce a larger belt.
            if (inventory.numberOfPotionStorage < 2)
            {
                inventory.ServerSetDefaultPotionStorage(2);

                MelonLogger.Msg(
                    "[AP Mission Reward] Potion Storage capacity set to 2."
                );
            }
        }

        private static void SynchronizeArchipelagoStartingPotion(ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null)
            {
                return;
            }

            bool received =
                apSession.Items.AllItemsReceived.Any(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoStartingPotionItemName,
                            StringComparison.Ordinal
                        )
                );

            if (!received)
                return;

            bool newlyUnlocked =
                !SwitchManager.GetDestinySwitch(
                    "StartingPotionLV1",
                    fallback: false
                );

            if (newlyUnlocked)
            {
                SwitchManager.SetDestinySwitch(
                    "StartingPotionLV1",
                    true,
                    false
                );

                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );

                MelonLogger.Msg(
                    "[AP Mission Reward] Starting Potion unlocked."
                );
            }

            if (CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return;
            }

            PlayerAvatar player =
                CombatManager.Instance.CurrentPlayer;

            PlayerLocalDataStorage data =
                player.GetComponent<PlayerLocalDataStorage>();

            if (data != null &&
                NetworkServer.active)
            {
                data.UpdateLv1PotionCount();
            }

            // Give the actual potion immediately the first
            // time the AP Starting Potion is received.
            if (newlyUnlocked &&
                NetworkServer.active &&
                player.Inventory != null)
            {
                ItemMetadata potion =
                    new ItemMetadata(
                        ItemDatabase.GenerateInstanceID(
                            new System.Random()
                        ),
                        0,
                        1
                    );

                player.Inventory.AddItem(
                    potion,
                    0,
                    notification: false
                );

                MelonLogger.Msg(
                    "[AP Mission Reward] Starting Potion added to inventory."
                );
            }
        }

        private static void SynchronizeArchipelagoWillpower(ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null)
            {
                return;
            }

            bool received =
                apSession.Items.AllItemsReceived.Any(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoWillpowerItemName,
                            StringComparison.Ordinal
                        )
                );

            if (!received)
                return;

            if (SwitchManager.GetDestinySwitch(
                    "Passive_9_Unlocked",
                    fallback: false))
            {
                return;
            }

            SwitchManager.SetDestinySwitch(
                "Passive_9_Unlocked",
                true,
                false
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );

            MelonLogger.Msg(
                "[AP Mission Reward] Willpower unlocked."
            );
        }

        private static void SynchronizeArchipelagoCostumeDyeing(ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null)
            {
                return;
            }

            bool received =
                apSession.Items.AllItemsReceived.Any(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoCostumeDyeingItemName,
                            StringComparison.Ordinal
                        )
                );

            if (!received)
                return;

            if (SwitchManager.GetDestinySwitch(
                    "CostumeDyeing",
                    fallback: false))
            {
                return;
            }

            SwitchManager.SetDestinySwitch(
                "CostumeDyeing",
                true,
                false
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );

            MelonLogger.Msg(
                "[AP Mission Reward] Costume Dyeing unlocked."
            );
        }

        private static void SynchronizeArchipelagoIngenuity(ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null)
            {
                return;
            }

            bool received =
                apSession.Items.AllItemsReceived.Any(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoIngenuityItemName,
                            StringComparison.Ordinal
                        )
                );

            if (!received)
                return;

            if (SwitchManager.GetDestinySwitch(
                    "Passive_11_Unlocked",
                    fallback: false))
            {
                return;
            }

            SwitchManager.SetDestinySwitch(
                "Passive_11_Unlocked",
                true,
                false
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );

            MelonLogger.Msg(
                "[AP Mission Reward] Ingenuity unlocked."
            );
        }

        private static void SynchronizeArchipelagoFruitSkewer(ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null)
            {
                return;
            }

            bool received =
                apSession.Items.AllItemsReceived.Any(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoFruitSkewerItemName,
                            StringComparison.Ordinal
                        )
                );

            if (received &&
                !SwitchManager.GetDestinySwitch(
                    "FruitSkewerClear",
                    fallback: false))
            {
                SwitchManager.SetDestinySwitch(
                    "FruitSkewerClear",
                    true,
                    false
                );

                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );

                MelonLogger.Msg(
                    "[AP Mission Reward] Fruit Skewer unlocked."
                );
            }

            RefreshArchipelagoFruitSkewerStand();
        }

        private static void SynchronizeArchipelagoWeapons(ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null ||
                SaveManager.Current == null ||
                !StartingWeaponType.HasValue)
            {
                return;
            }

            bool saveChanged = false;

            // These vanilla weapon tutorials are not needed
            // for Archipelago weapon progression.
            if (SaveManager.Current.GetBool(
                    "RandomWeaponTutorial",
                    true))
            {
                SaveManager.Current.SetBool(
                    "RandomWeaponTutorial",
                    false
                );

                saveChanged = true;
            }

            if (SaveManager.Current.GetBool(
                    "TrainingSchoolDoor_Tutorial",
                    true))
            {
                SaveManager.Current.SetBool(
                    "TrainingSchoolDoor_Tutorial",
                    false
                );

                saveChanged = true;
            }

            HashSet<EWeaponType> ownedWeapons =
                new HashSet<EWeaponType>();

            // The randomly selected starting weapon is always owned.
            ownedWeapons.Add(
                StartingWeaponType.Value
            );

            // Add every weapon received from Archipelago.
            foreach (var receivedItem
                in apSession.Items.AllItemsReceived)
            {
                if (receivedItem == null)
                    continue;

                EWeaponType weaponType;

                if (archipelagoWeaponUnlockItems.TryGetValue(
                        receivedItem.ItemName,
                        out weaponType))
                {
                    ownedWeapons.Add(
                        weaponType
                    );
                }
            }

            // Sword & Shield has no vanilla Destiny switch because
            // vanilla normally grants it unconditionally.
            bool swordAndShieldUnlocked =
                ownedWeapons.Contains(
                    EWeaponType.SwordAndShield
                );

            bool currentSwordAndShieldUnlocked =
                SaveManager.Current.GetBool(
                    "ArchipelagoSwordAndShieldUnlocked",
                    false
                );

            if (currentSwordAndShieldUnlocked !=
                swordAndShieldUnlocked)
            {
                SaveManager.Current.SetBool(
                    "ArchipelagoSwordAndShieldUnlocked",
                    swordAndShieldUnlocked
                );

                saveChanged = true;
            }

            Dictionary<EWeaponType, string> weaponSwitches =
                new Dictionary<EWeaponType, string>
                {
            {
                EWeaponType.GreatSword,
                "GreatswordUnlocked"
            },
            {
                EWeaponType.Dagger,
                "DaggerUnlocked"
            },
            {
                EWeaponType.Crossbow,
                "CrossbowUnlocked"
            },
            {
                EWeaponType.Katana,
                "KatanaUnlocked"
            },
            {
                EWeaponType.Staff,
                "StaffUnlocked"
            }
                };

            foreach (KeyValuePair<EWeaponType, string> weapon
                in weaponSwitches)
            {
                bool shouldBeUnlocked =
                    ownedWeapons.Contains(
                        weapon.Key
                    );

                bool currentlyUnlocked =
                    SwitchManager.GetDestinySwitch(
                        weapon.Value,
                        fallback: false
                    );

                if (currentlyUnlocked ==
                    shouldBeUnlocked)
                {
                    continue;
                }

                SwitchManager.SetDestinySwitch(
                    weapon.Value,
                    shouldBeUnlocked,
                    false
                );

                saveChanged = true;
            }

            if (saveChanged)
            {
                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );
            }

            // Refresh the player's runtime weapon list immediately.
            if (CombatManager.Instance != null &&
                CombatManager.Instance.CurrentPlayer != null)
            {
                PlayerLocalDataStorage data =
                    CombatManager.Instance.CurrentPlayer
                        .localDataStorage;

                if (data != null)
                {
                    data.CheckWeaponUnlocked();
                }
            }

            // Only queue the one-time starting weapon setup
            // if this save has not done it yet.
            if (!SaveManager.Current.GetBool(
                    "ArchipelagoStartingWeaponInitialized",
                    false))
            {
                startingWeaponInitializationPending = true;
            }

            // Do not globally scan weapon stands every time
            // an unrelated AP item is received.
            //
            // saveChanged is true here when this synchronization
            // actually changed weapon/tutorial state.
            if (saveChanged)
            {
                weaponStandSyncPending = true;
            }
        }

        private static void SynchronizeArchipelagoDestinyUpgrades(
            ArchipelagoSession apSession)
        {
            if (apSession == null ||
                SaveManager.Current == null ||
                CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return;
            }

            TreeShopItemStorage treeShopStorage = null;

            if (NetworkServer.active &&
                CombatManager.Instance != null &&
                CombatManager.Instance.CurrentPlayer != null)
            {
                treeShopStorage =
                    CombatManager.Instance.CurrentPlayer
                        .GetComponent<TreeShopItemStorage>();
            }

            MethodInfo unlockMethod = null;

            if (treeShopStorage != null)
            {
                unlockMethod =
                    AccessTools.Method(
                        typeof(TreeShopItemStorage),
                        "Unlock"
                    );
            }

            bool saveChanged = false;

            IsSynchronizingArchipelagoDestinyUpgrades = true;

            try
            {
                // =================================================
                // Normal one-node Destiny upgrades
                // =================================================

                foreach (KeyValuePair<string, int> upgrade
                    in archipelagoDestinyUpgradeItems)
                {
                    string apItemName =
                        upgrade.Key;

                    int treeShopItemId =
                        upgrade.Value;

                    int receivedCount =
                        apSession.Items.AllItemsReceived.Count(
                            item =>
                                item != null &&
                                string.Equals(
                                    item.ItemName,
                                    apItemName,
                                    StringComparison.Ordinal
                                )
                        );

                    ApplyArchipelagoDestinyUpgrade(
                        treeShopStorage,
                        unlockMethod,
                        treeShopItemId,
                        receivedCount,
                        apItemName,
                        ref saveChanged
                    );
                }


                // =================================================
                // Progressive Destiny upgrades
                // =================================================

                foreach (KeyValuePair<string, int[]>
                    progressiveUpgrade
                    in archipelagoProgressiveDestinyUpgradeItems)
                {
                    string apItemName =
                        progressiveUpgrade.Key;

                    int[] treeShopItemIds =
                        progressiveUpgrade.Value;

                    int receivedCount =
                        apSession.Items.AllItemsReceived.Count(
                            item =>
                                item != null &&
                                string.Equals(
                                    item.ItemName,
                                    apItemName,
                                    StringComparison.Ordinal
                                )
                        );

                    int remainingCount =
                        receivedCount;

                    foreach (int treeShopItemId
                        in treeShopItemIds)
                    {
                        TreeShopItemEntity shopItem =
                            TreeShopItemDatabase.FindById(
                                treeShopItemId
                            );

                        if (shopItem == null)
                        {
                            MelonLogger.Warning(
                                $"[AP Destiny Upgrade] Could not find " +
                                $"TreeShop item {treeShopItemId} " +
                                $"for {apItemName}."
                            );

                            continue;
                        }

                        int desiredCount =
                            Math.Min(
                                remainingCount,
                                shopItem.maxQuantity
                            );

                        ApplyArchipelagoDestinyUpgrade(
                            treeShopStorage,
                            unlockMethod,
                            treeShopItemId,
                            desiredCount,
                            apItemName,
                            ref saveChanged
                        );

                        remainingCount =
                            Math.Max(
                                remainingCount -
                                shopItem.maxQuantity,
                                0
                            );
                    }
                }


                if (saveChanged)
                {
                    SaveManager.Save(
                        saveCurrent: true,
                        saveCurrentRun: false
                    );
                }
            }
            finally
            {
                IsSynchronizingArchipelagoDestinyUpgrades =
                    false;
            }
        }

        private static void UpdateArchipelagoTraps()
        {
            if (!trapSyncPending ||
                !IsArchipelagoSave ||
                !TrapsEnabled ||
                SaveManager.Current == null)
            {
                return;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return;
            }

            if (CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return;
            }

            PlayerAvatar player =
                CombatManager.Instance.CurrentPlayer;

            // Do not waste traps in town, while dead,
            // during cutscenes, or while the game is paused.
            if (!IsSafeToApplyDeathLink(player))
                return;

            List<string> receivedTraps =
                apSession.Items.AllItemsReceived
                    .Where(item =>
                        item != null &&
                        archipelagoTrapItemNames.Contains(
                            item.ItemName
                        )
                    )
                    .Select(item => item.ItemName)
                    .ToList();

            int processedCount =
                SaveManager.Current.GetInt(
                    ArchipelagoProcessedTrapCountKey,
                    0
                );

            if (processedCount < 0)
            {
                processedCount = 0;
            }

            if (processedCount >= receivedTraps.Count)
            {
                trapSyncPending = false;
                return;
            }

            for (
                int i = processedCount;
                i < receivedTraps.Count;
                i++
            )
            {
                string trapName =
                    receivedTraps[i];

                if (!ApplyArchipelagoTrap(
                        player,
                        trapName))
                {
                    // Leave this trap unprocessed.
                    // We'll try again on a later frame.
                    return;
                }

                processedCount++;

                SaveManager.Current.SetInt(
                    ArchipelagoProcessedTrapCountKey,
                    processedCount
                );

                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );
            }

            trapSyncPending = false;
        }

        private static float GetRandomArchipelagoTrapDuration()
        {
            return 20f +
                (
                    (float)
                    archipelagoTrapRandom.NextDouble()
                    * 10f
                );
        }

        private static float ExtendArchipelagoTrapTimer(
            float currentUntil,
            float duration)
        {
            return Mathf.Max(
                currentUntil,
                Time.time
            ) + duration;
        }

        private static bool ApplyArchipelagoTrap(
            PlayerAvatar player,
            string trapName)
        {
            if (player == null)
                return false;

            float duration =
                GetRandomArchipelagoTrapDuration();

            switch (trapName)
            {
                case "Can't Cast Trap":
                    cannotCastTrapUntil =
                        ExtendArchipelagoTrapTimer(
                            cannotCastTrapUntil,
                            duration
                        );

                    SkillController skillController =
                        player.GetComponent<SkillController>();

                    if (skillController != null)
                    {
                        skillController.CastStop();
                    }

                    break;

                case "Can't Dash Trap":
                    cannotDashTrapUntil =
                        ExtendArchipelagoTrapTimer(
                            cannotDashTrapUntil,
                            duration
                        );
                    break;

                case "Can't Normal Attack Trap":
                    cannotNormalAttackTrapUntil =
                        ExtendArchipelagoTrapTimer(
                            cannotNormalAttackTrapUntil,
                            duration
                        );
                    break;

                case "Flipped Controls Trap":
                    flippedControlsTrapUntil =
                        ExtendArchipelagoTrapTimer(
                            flippedControlsTrapUntil,
                            duration
                        );
                    break;

                case "Bag Shuffle Trap":
                    if (!TryApplyArchipelagoBagShuffle(
                            player))
                    {
                        return false;
                    }

                    duration = 0f;
                    break;

                default:
                    MelonLogger.Warning(
                        "[AP Trap] Unknown trap: " +
                        trapName
                    );

                    return false;
            }

            MelonLogger.Warning(
                "[AP Trap] Activated " +
                trapName +
                (
                    duration > 0f
                        ? " for " +
                          duration.ToString("0.0") +
                          " seconds."
                        : "."
                )
            );

            return true;
        }

        public static bool IsCantCastTrapActive
        {
            get
            {
                return
                    IsArchipelagoSave &&
                    TrapsEnabled &&
                    Time.time < cannotCastTrapUntil;
            }
        }

        public static bool IsCantDashTrapActive
        {
            get
            {
                return
                    IsArchipelagoSave &&
                    TrapsEnabled &&
                    Time.time < cannotDashTrapUntil;
            }
        }

        public static bool IsCantNormalAttackTrapActive
        {
            get
            {
                return
                    IsArchipelagoSave &&
                    TrapsEnabled &&
                    Time.time <
                        cannotNormalAttackTrapUntil;
            }
        }

        public static bool IsFlippedControlsTrapActive
        {
            get
            {
                return
                    IsArchipelagoSave &&
                    TrapsEnabled &&
                    Time.time <
                        flippedControlsTrapUntil;
            }
        }

        private static bool TryApplyArchipelagoBagShuffle(
            PlayerAvatar player)
        {
            if (player == null ||
                player.Inventory == null ||
                !NetworkServer.active)
            {
                return false;
            }

            GridInventory inventory =
                player.Inventory;

            MethodInfo captureMethod =
                AccessTools.Method(
                    typeof(GridInventory),
                    "CaptureAutoArrangeState"
                );

            MethodInfo applyMethod =
                AccessTools.Method(
                    typeof(GridInventory),
                    "ApplyAutoArrangeState"
                );

            if (captureMethod == null ||
                applyMethod == null)
            {
                MelonLogger.Warning(
                    "[AP Trap] Could not find " +
                    "GridInventory shuffle methods."
                );

                return false;
            }

            object state =
                captureMethod.Invoke(
                    inventory,
                    null
                );

            if (state == null)
                return false;

            FieldInfo slotsField =
                AccessTools.Field(
                    state.GetType(),
                    "slots"
                );

            IList slots =
                slotsField?.GetValue(
                    state
                ) as IList;

            if (slots == null)
                return false;

            // Nothing meaningful to shuffle.
            if (slots.Count <= 1)
                return true;

            for (
                int i = slots.Count - 1;
                i > 0;
                i--
            )
            {
                int j =
                    archipelagoTrapRandom.Next(
                        i + 1
                    );

                object temp =
                    slots[i];

                slots[i] =
                    slots[j];

                slots[j] =
                    temp;
            }

            using (
                new GridInventory.Permission(
                    inventory
                )
            )
            {
                applyMethod.Invoke(
                    inventory,
                    new object[]
                    {
                state
                    }
                );
            }

            MelonLogger.Msg(
                "[AP Trap] Inventory shuffled."
            );

            return true;
        }

        private static void SynchronizeArchipelagoSapphireFiller(
            ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave)
                return;

            if (SaveManager.Current == null)
                return;

            if (apSession == null)
                return;

            if (CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return;
            }

            PlayerLocalDataStorage data =
                CombatManager.Instance.CurrentPlayer
                    .GetComponent<PlayerLocalDataStorage>();

            if (data == null)
                return;


            int receivedCount =
                apSession.Items.AllItemsReceived.Count(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoSapphireFillerItemName,
                            StringComparison.Ordinal
                        )
                );

            int processedCount =
                SaveManager.Current.GetInt(
                    ArchipelagoProcessedSapphireFillerCountKey,
                    0
                );


            // Nothing new to award.
            if (receivedCount <= processedCount)
                return;


            int newItemCount =
                receivedCount - processedCount;

            int sapphireToGive =
                newItemCount * 5;


            // Use Sephiria's normal permanent Sapphire update
            // pattern from FunctionNode_GiveSapphire.
            data.Networksapphire =
                data.sapphire +
                sapphireToGive;

            SaveManager.Current.SetInt(
                "Sapphire",
                data.sapphire
            );

            // Record exactly how many AP filler items have
            // already been processed for this save.
            SaveManager.Current.SetInt(
                ArchipelagoProcessedSapphireFillerCountKey,
                receivedCount
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );

            MelonLogger.Msg(
                "[AP Sapphire] Awarded " +
                sapphireToGive +
                " Sapphire from " +
                newItemCount +
                " new Archipelago filler item(s)."
            );
        }

        private static void ApplyArchipelagoDestinyUpgrade(
            TreeShopItemStorage treeShopStorage,
            MethodInfo unlockMethod,
            int treeShopItemId,
            int requestedCount,
            string apItemName,
            ref bool saveChanged)
        {
            TreeShopItemEntity shopItem =
                TreeShopItemDatabase.FindById(
                    treeShopItemId
                );

            if (shopItem == null)
            {
                MelonLogger.Warning(
                    $"[AP Destiny Upgrade] Could not find " +
                    $"TreeShop item {treeShopItemId} " +
                    $"for {apItemName}."
                );

                return;
            }

            int desiredCount =
                Math.Min(
                    Math.Max(
                        requestedCount,
                        0
                    ),
                    shopItem.maxQuantity
                );

            string saveKey =
                $"TreeShopItem_{treeShopItemId}_" +
                $"PurchasedCount";

            int currentCount =
                SaveManager.Current.GetInt(
                    saveKey,
                    0
                );

            // Archipelago grants the Wishing Fountain
            // immediately when its AP item is received.
            // Vanilla normally waits until a game-over
            // screen before setting this flag.
            if (treeShopItemId == 14 &&
                desiredCount > 0 &&
                !SaveManager.Current.GetBool(
                    "PresetSelectorDeathCheck",
                    false
                ))
            {
                SaveManager.Current.SetBool(
                    "PresetSelectorDeathCheck",
                    true
                );

                saveChanged = true;

                APDebug.Log(
                    "[AP Destiny Upgrade] " +
                    "Wishing Fountain preset selector enabled."
                );
            }

            if (currentCount == desiredCount)
                return;

            SaveManager.Current.SetInt(
                saveKey,
                desiredCount
            );

            saveChanged = true;

            APDebug.Log(
                $"[AP Destiny Upgrade] " +
                $"{apItemName} -> TreeShop " +
                $"{treeShopItemId}: " +
                $"{currentCount} -> {desiredCount}"
            );

            // Apply Sephiria's real vanilla effect immediately
            // when the runtime player exists.
            //
            // Unlock(..., false) is the same path vanilla Load()
            // uses for already-owned Destiny upgrades.
            if (desiredCount > 0 &&
                treeShopStorage != null &&
                unlockMethod != null)
            {
                unlockMethod.Invoke(
                    treeShopStorage,
                    new object[]
                    {
                shopItem,
                desiredCount,
                false
                    }
                );
            }
        }

        public static void RefreshArchipelagoFruitSkewerStand()
        {
            if (!IsArchipelagoSave ||
                SaveManager.Current == null)
            {
                return;
            }

            bool missionCompleted =
                SaveManager.Current.GetBool(
                    "ArchipelagoFruitSkewerMissionComplete",
                    false
                );

            // Also recognize saves where the AP location was
            // already checked before this visibility system existed.
            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            long fruitSkewerLocationId;

            if (!missionCompleted &&
                apSession != null &&
                archipelagoMissionLocations.TryGetValue(
                    "Fruit Skewer",
                    out fruitSkewerLocationId) &&
                !apSession.Locations.AllMissingLocations.Contains(
                    fruitSkewerLocationId))
            {
                missionCompleted = true;
            }

            bool fruitSkewerUnlocked =
                SwitchManager.GetDestinySwitch(
                    "FruitSkewerClear",
                    fallback: false
                );

            // Before the mission is completed, leave vanilla
            // visibility alone.
            if (!missionCompleted &&
                !fruitSkewerUnlocked)
            {
                return;
            }

            bool shouldBeVisible =
                fruitSkewerUnlocked;

            FruitSkewerSelector[] selectors =
                Resources.FindObjectsOfTypeAll<FruitSkewerSelector>();

            foreach (FruitSkewerSelector selector
                in selectors)
            {
                if (selector == null ||
                    selector.gameObject == null ||
                    !selector.gameObject.scene.IsValid() ||
                    !selector.gameObject.scene.isLoaded)
                {
                    continue;
                }

                if (selector.gameObject.activeSelf !=
                    shouldBeVisible)
                {
                    selector.gameObject.SetActive(
                        shouldBeVisible
                    );

                    MelonLogger.Msg(
                        "[AP Fruit Skewer] Stand = " +
                        (shouldBeVisible
                            ? "VISIBLE."
                            : "HIDDEN.")
                    );
                }
            }
        }

        public static bool IsArchipelagoWeaponOwned(EWeaponType weaponType)
        {
            if (!IsArchipelagoSave ||
                SaveManager.Current == null)
            {
                return false;
            }

            switch (weaponType)
            {
                case EWeaponType.SwordAndShield:
                    return SaveManager.Current.GetBool(
                        "ArchipelagoSwordAndShieldUnlocked",
                        false
                    );

                case EWeaponType.GreatSword:
                    return SwitchManager.GetDestinySwitch(
                        "GreatswordUnlocked",
                        false
                    );

                case EWeaponType.Dagger:
                    return SwitchManager.GetDestinySwitch(
                        "DaggerUnlocked",
                        false
                    );

                case EWeaponType.Crossbow:
                    return SwitchManager.GetDestinySwitch(
                        "CrossbowUnlocked",
                        false
                    );

                case EWeaponType.Staff:
                    return SwitchManager.GetDestinySwitch(
                        "StaffUnlocked",
                        false
                    );

                case EWeaponType.Katana:
                    return SwitchManager.GetDestinySwitch(
                        "KatanaUnlocked",
                        false
                    );

                default:
                    return false;
            }
        }

        private static bool TryGetArchipelagoWeaponEntity(
            EWeaponType weaponType,
            out WeaponEntity weaponEntity)
        {
            weaponEntity = null;

            IEnumerable<WeaponEntity> weapons =
                WeaponDatabase.GetDefaultWeapons();

            if (weapons == null)
                return false;

            foreach (WeaponEntity weapon in weapons)
            {
                if (weapon == null ||
                    weapon.mainWeaponPrefab == null)
                {
                    continue;
                }

                WeaponSimple weaponSimple =
                    weapon.mainWeaponPrefab
                        .GetComponent<WeaponSimple>();

                if (weaponSimple == null)
                    continue;

                if (weaponSimple.weaponType != weaponType)
                    continue;

                weaponEntity = weapon;
                return true;
            }

            return false;
        }

        private static bool TryInitializeArchipelagoStartingWeapon()
        {
            if (!IsArchipelagoSave ||
                SaveManager.Current == null ||
                !StartingWeaponType.HasValue)
            {
                return false;
            }

            // This AP save has already had its randomized
            // starting weapon applied. From this point on,
            // Sephiria's normal weapon selection should win.
            if (SaveManager.Current.GetBool(
                    "ArchipelagoStartingWeaponInitialized",
                    false))
            {
                return true;
            }

            if (CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return false;
            }

            PlayerAvatar player = CombatManager.Instance.CurrentPlayer;

            WeaponControllerSimple weaponController = player.GetComponent<WeaponControllerSimple>();

            PlayerLocalDataStorage localDataStorage = player.GetComponent<PlayerLocalDataStorage>();

            if (weaponController == null ||
                localDataStorage == null)
            {
                return false;
            }

            // Wait until Sephiria has completely finished equipping
            // its vanilla starting weapon.
            //
            // WeaponSimple.Initialize() assigns:
            //     Networkowner.currentWeapon = this;
            //
            // If we replace the weapon before that happens, the
            // vanilla weapon can finish initializing afterward and
            // overwrite the AP starting weapon.
            if (weaponController.currentWeapon == null)
            {
                return false;
            }

            WeaponEntity startingWeapon;

            if (!TryGetArchipelagoWeaponEntity(
                    StartingWeaponType.Value,
                    out startingWeapon))
            {
                MelonLogger.Warning(
                    $"[AP Weapons] Could not find WeaponEntity for " +
                    $"{StartingWeaponType.Value}."
                );

                return false;
            }

            // Only perform another equip if Sephiria spawned us
            // with a different weapon.
            if (weaponController.currentWeapon.weaponType !=
                StartingWeaponType.Value)
            {
                APDebug.Log(
                    $"[AP Weapons] Replacing vanilla starting weapon " +
                    $"{weaponController.currentWeapon.weaponType} with " +
                    $"{StartingWeaponType.Value}."
                );

                weaponController.EquipWeapon(
                    fromTownObject: false,
                    startingWeapon.id
                );
            }

            localDataStorage.NetworkdefaultWeapon =
                startingWeapon.id;

            SaveManager.Current.SetInt(
                "StartingWeaponID",
                startingWeapon.id
            );

            // IMPORTANT:
            // Once this is true, we never force the AP starter
            // again. The player may freely choose another owned
            // weapon from its stand.
            SaveManager.Current.SetBool(
                "ArchipelagoStartingWeaponInitialized",
                true
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );

            APDebug.Log(
                $"[AP Weapons] Initial starting weapon equipped: " +
                $"{StartingWeaponType.Value} " +
                $"(Weapon ID {startingWeapon.id})."
            );

            return true;
        }

        private static bool RefreshArchipelagoWeaponStands()
        {
            if (!IsArchipelagoSave ||
                SaveManager.Current == null)
            {
                return false;
            }

            TownWeaponStand[] stands =
                Resources.FindObjectsOfTypeAll<TownWeaponStand>();

            if (stands == null ||
                stands.Length == 0)
            {
                return false;
            }

            int recognizedStandCount = 0;

            foreach (TownWeaponStand stand in stands)
            {
                if (stand == null ||
                    stand.gameObject == null ||
                    !stand.gameObject.scene.IsValid() ||
                    !stand.gameObject.scene.isLoaded ||
                    stand.weapon == null ||
                    stand.weapon.mainWeaponPrefab == null)
                {
                    continue;
                }

                WeaponSimple weaponSimple =
                    stand.weapon.mainWeaponPrefab
                        .GetComponent<WeaponSimple>();

                if (weaponSimple == null)
                    continue;

                EWeaponType weaponType =
                    weaponSimple.weaponType;

                switch (weaponType)
                {
                    case EWeaponType.SwordAndShield:
                    case EWeaponType.GreatSword:
                    case EWeaponType.Dagger:
                    case EWeaponType.Crossbow:
                    case EWeaponType.Staff:
                    case EWeaponType.Katana:
                        break;

                    default:
                        continue;
                }

                recognizedStandCount++;

                bool shouldBeVisible =
                    IsArchipelagoWeaponOwned(
                        weaponType
                    );

                if (stand.gameObject.activeSelf !=
                    shouldBeVisible)
                {
                    stand.gameObject.SetActive(
                        shouldBeVisible
                    );

                    APDebug.Log(
                        $"[AP Weapons] {weaponType} stand = " +
                        $"{(shouldBeVisible ? "VISIBLE" : "HIDDEN")}."
                    );
                }
            }

            return recognizedStandCount > 0;
        }

        public static void QueueArchipelagoWeaponStandSync()
        {
            if (!IsArchipelagoSave)
                return;

            weaponStandSyncPending = true;
        }

        public static int GetProgressiveAnvilUpgradeCount()
        {
            if (!IsArchipelagoSave)
                return 0;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return 0;
            }

            // If Anvil unlocks were not randomized,
            // both uses are available from the beginning.
            if (!RandomizeAnvilUnlocks)
                return 2;

            return Math.Min(
                2,
                apSession.Items.AllItemsReceived.Count(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            "Progressive Anvil Unlock",
                            StringComparison.Ordinal
                        )
                )
            );
        }

        public static int GetArchipelagoAnvilUsesThisRun()
        {
            if (!IsArchipelagoSave ||
                SaveManager.CurrentRun == null)
            {
                return 0;
            }

            return SaveManager.CurrentRun.GetInt(
                ArchipelagoAnvilUsesThisRunKey,
                0
            );
        }


        public static bool CanUseArchipelagoRunAnvil()
        {
            if (!IsArchipelagoSave)
                return true;

            int unlockedAnvils =
                GetProgressiveAnvilUpgradeCount();

            int usedAnvils =
                GetArchipelagoAnvilUsesThisRun();

            return usedAnvils < unlockedAnvils;
        }


        public static void RecordArchipelagoRunAnvilUse()
        {
            if (!IsArchipelagoSave ||
                SaveManager.CurrentRun == null)
            {
                return;
            }

            int unlockedAnvils =
                GetProgressiveAnvilUpgradeCount();

            int usedAnvils =
                GetArchipelagoAnvilUsesThisRun();

            if (usedAnvils >= unlockedAnvils)
                return;

            SaveManager.CurrentRun.SetInt(
                ArchipelagoAnvilUsesThisRunKey,
                usedAnvils + 1
            );

            SaveManager.Save(
                saveCurrent: false,
                saveCurrentRun: true
            );
        }


        public static void ResetArchipelagoAnvilRunState()
        {
            if (!IsArchipelagoSave ||
                SaveManager.CurrentRun == null)
            {
                return;
            }

            SaveManager.CurrentRun.SetInt(
                ArchipelagoAnvilUsesThisRunKey,
                0
            );
        }

        public static void QueueArchipelagoCintamaniSync()
        {
            if (!IsArchipelagoSave)
                return;

            cintamaniSyncPending = true;
        }


        public static int GetUnlockedStartingCintamaniCount()
        {
            if (!IsArchipelagoSave)
                return 0;

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                return 0;
            }

            int receivedCount =
                apSession.Items.AllItemsReceived.Count(
                    item =>
                        item != null &&
                        string.Equals(
                            item.ItemName,
                            ArchipelagoStartingCintamaniStoneItemName,
                            StringComparison.Ordinal
                        )
                );

            return Math.Min(
                StartingCintamaniStonePoolCount,
                receivedCount
            );
        }


        public static void ResetArchipelagoCintamaniRunState()
        {
            if (!IsArchipelagoSave)
                return;

            if (SaveManager.CurrentRun != null)
            {
                SaveManager.CurrentRun.SetInt(
                    ArchipelagoCintamaniGrantedThisRunKey,
                    0
                );
            }

            cintamaniSyncPending = true;
        }

        private static void UpdateArchipelagoCintamani()
        {
            if (!cintamaniSyncPending)
                return;

            // Never grant another Cintamani Stone while
            // a locked-floor forced death is ending the run.
            if (suppressOutgoingDeathLinkForForcedFloorDeath)
                return;

            if (!IsArchipelagoSave ||
                SaveManager.CurrentRun == null)
            {
                return;
            }

            int unlockedCount =
                GetUnlockedStartingCintamaniCount();

            if (unlockedCount <= 0)
            {
                cintamaniSyncPending = false;
                return;
            }

            if (!NetworkServer.active ||
                CombatManager.Instance == null ||
                CombatManager.Instance.CurrentPlayer == null)
            {
                return;
            }

            PlayerAvatar player =
                CombatManager.Instance.CurrentPlayer;

            if (player.isInDungeon <= 0 ||
                player.Inventory == null)
            {
                return;
            }

            int grantedThisRun =
                SaveManager.CurrentRun.GetInt(
                    ArchipelagoCintamaniGrantedThisRunKey,
                    0
                );

            if (grantedThisRun >= unlockedCount)
            {
                cintamaniSyncPending = false;
                return;
            }

            ItemEntity cintamani =
                ItemDatabase.FindItemById(
                    ArchipelagoCintamaniEntityId
                );

            if (cintamani == null)
            {
                MelonLogger.Warning(
                    "[AP Cintamani] Item 1146 could not be found."
                );

                return;
            }

            sbyte itemX;
            sbyte itemY;
            sbyte itemQuantity;

            // Only keep one physical Cintamani in the
            // inventory at a time.
            if (player.Inventory.HasItem(
                    cintamani,
                    out itemX,
                    out itemY,
                    out itemQuantity))
            {
                return;
            }

            int instanceId =
                ItemDatabase.GenerateInstanceID(
                    new System.Random()
                );

            bool added;

            using (
                new GridInventory.Permission(
                    player.Inventory
                )
            )
            {
                added =
                    player.Inventory.LocalAddItem(
                        instanceId,
                        ArchipelagoCintamaniEntityId,
                        1,
                        0,
                        false,
                        false
                    );
            }

            if (!added)
                return;

            grantedThisRun++;

            SaveManager.CurrentRun.SetInt(
                ArchipelagoCintamaniGrantedThisRunKey,
                grantedThisRun
            );

            SaveManager.Save(
                saveCurrent: false,
                saveCurrentRun: true
            );

            MelonLogger.Msg(
                "[AP Cintamani] Starting Cintamani granted. " +
                $"Charge {grantedThisRun}/{unlockedCount}."
            );

            if (grantedThisRun >= unlockedCount)
            {
                cintamaniSyncPending = false;
            }
        }

        public static bool GetArchipelagoPassiveAccess(
            string switchName,
            bool fallback)
        {
            if (IsArchipelagoSave &&
                string.Equals(
                    switchName,
                    "RebuildHomeClear",
                    StringComparison.Ordinal
                ))
            {
                return HasArchipelagoTalentAccess();
            }

            return SwitchManager.GetDestinySwitch(
                switchName,
                fallback
            );
        }

        public static IEnumerable<CodeInstruction>
            ReplacePassiveAccessCheck(
                IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo originalMethod =
                AccessTools.Method(
                    typeof(SwitchManager),
                    nameof(SwitchManager.GetDestinySwitch),
                    new Type[]
                    {
                typeof(string),
                typeof(bool)
                    }
                );

            MethodInfo replacementMethod =
                AccessTools.Method(
                    typeof(SephiriaMod),
                    nameof(GetArchipelagoPassiveAccess)
                );

            foreach (CodeInstruction instruction
                in instructions)
            {
                MethodInfo calledMethod =
                    instruction.operand as MethodInfo;

                if (calledMethod == originalMethod)
                {
                    instruction.operand =
                        replacementMethod;
                }

                yield return instruction;
            }
        }

        private static void SynchronizeArchipelagoMissionUnlocks(
            ArchipelagoSession apSession)
        {
            if (!IsArchipelagoSave ||
                apSession == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> mission
                in archipelagoMissionUnlockItems)
            {
                string apItemName = mission.Key;
                string questId = mission.Value;

                bool received =
                    apSession.Items.AllItemsReceived.Any(
                        item =>
                            item != null &&
                            string.Equals(
                                item.ItemName,
                                apItemName,
                                StringComparison.Ordinal
                            )
                    );

                if (!received)
                    continue;

                QuestDatabase.SetDestinyQuestAcceptable(
                    questId,
                    false
                );
            }
        }

        public static bool TryCompleteArchipelagoMission(string missionName)
        {
            if (!IsArchipelagoSave)
                return false;

            long locationId;

            if (!archipelagoMissionLocations.TryGetValue(
                    missionName,
                    out locationId))
            {
                MelonLogger.Warning(
                    $"[AP Mission] Unknown mission: {missionName}"
                );

                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    $"[AP Mission] Cannot check {missionName}: " +
                    "Archipelago is disconnected."
                );

                return false;
            }

            // Already checked.
            if (!apSession.Locations
                    .AllMissingLocations
                    .Contains(locationId))
            {
                return true;
            }

            string locationName =
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
                    $"[AP Mission] Failed to check " +
                    $"{locationName}: {ex.Message}"
                );

                return false;
            }

            MelonLogger.Msg(
                $"[AP Mission] Checked {locationName}."
            );

            return true;
        }

        public static bool TryCompleteArchipelagoDestinyInscription(int treeShopItemId)
        {
            if (!IsArchipelagoSave)
                return false;

            long locationId;

            if (!archipelagoDestinyInscriptionLocationsByTreeShopId
                    .TryGetValue(
                        treeShopItemId,
                        out locationId))
            {
                MelonLogger.Warning(
                    $"[AP Tree] No AP location mapped to " +
                    $"TreeShop item {treeShopItemId}."
                );

                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                MelonLogger.Warning(
                    $"[AP Tree] Cannot check TreeShop item " +
                    $"{treeShopItemId}: Archipelago is disconnected."
                );

                return false;
            }

            // Already sent.
            if (!apSession.Locations
                    .AllMissingLocations
                    .Contains(locationId))
            {
                return true;
            }

            string locationName =
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
                    $"[AP Tree] Failed to check " +
                    $"{locationName}: {ex.Message}"
                );

                return false;
            }

            MelonLogger.Msg(
                $"[AP Tree] Checked {locationName}."
            );

            return true;
        }

        private static void RefreshArchipelagoDeclineRuntimeStatus()
        {
            if (!IsArchipelagoSave)
                return;

            if (!Mirror.NetworkServer.active)
                return;

            if (DungeonManager.Instance == null)
                return;

            int declineValue = 0;

            DungeonManager.Instance
                .hardModeEnvironment
                .TryGetValue(
                    "DECLINE",
                    out declineValue
                );

            foreach (PlayerSpawner playerSpawner
                in PlayerSpawner.MultiplayerList)
            {
                if (playerSpawner == null ||
                    playerSpawner.PlayerAvatar == null)
                {
                    continue;
                }

                PlayerAvatar player =
                    playerSpawner.PlayerAvatar;

                for (int i =
                        player.orphanedStatusInstancesServerside.Count - 1;
                     i >= 0;
                     i--)
                {
                    StatusInstance status =
                        player.orphanedStatusInstancesServerside[i];

                    if (status == null ||
                        status.ID !=
                            "HARDMODE_HEALING_PENALTY")
                    {
                        continue;
                    }

                    status.RemoveStatus();
                    status.ClearTarget();

                    player.orphanedStatusInstancesServerside
                        .RemoveAt(i);
                }

                if (declineValue > 0)
                {
                    StatusInstance replacement =
                        StatusDatabase.CreateStatusEntity(
                            "HARDMODE_HEALING_PENALTY",
                            declineValue
                        );

                    if (replacement != null)
                    {
                        player.AddOrphanedStatusInstance(
                            replacement
                        );
                    }
                }
            }

            MelonLogger.Msg(
                "[AP Hard Mode] Decline runtime status refreshed " +
                "to " + declineValue + "."
            );
        }

        private static void RefreshArchipelagoMindBurdenRuntimeItem()
        {
            if (!IsArchipelagoSave)
                return;

            if (!Mirror.NetworkServer.active)
                return;

            if (DungeonManager.Instance == null)
                return;

            int mindBurdenValue = 0;

            DungeonManager.Instance
                .hardModeEnvironment
                .TryGetValue(
                    "MINDBURDEN",
                    out mindBurdenValue
                );

            // Heavy Heart should still exist while
            // MINDBURDEN is active.
            if (mindBurdenValue > 0)
                return;

            foreach (PlayerSpawner playerSpawner
                in PlayerSpawner.MultiplayerList)
            {
                if (playerSpawner == null ||
                    playerSpawner.PlayerAvatar == null)
                {
                    continue;
                }

                PlayerAvatar player =
                    playerSpawner.PlayerAvatar;

                GridInventory inventory =
                    player.Inventory;

                if (inventory == null)
                    continue;

                HashSet<int> heavyHeartInstanceIds =
                    new HashSet<int>();

                foreach (NewItemOwnInstance item
                    in inventory.inventoryMatrix.Values)
                {
                    if (item == null ||
                        item.EntityID != 1304)
                    {
                        continue;
                    }

                    int instanceId =
                        item.InstanceID;

                    string ownRestriction =
                        DungeonManager.Instance
                            .GetGlobalItemStatValue(
                                instanceId,
                                "OwnRestriction"
                            );

                    string boundPlayer =
                        DungeonManager.Instance
                            .GetGlobalItemStatValue(
                                instanceId,
                                "Bound"
                            );

                    if (ownRestriction !=
                        ((int)ERestrictedOwnType.StartingItem)
                            .ToString())
                    {
                        continue;
                    }

                    if (boundPlayer !=
                        playerSpawner.currentPlayerIdx
                            .ToString())
                    {
                        continue;
                    }

                    heavyHeartInstanceIds.Add(
                        instanceId
                    );
                }

                if (heavyHeartInstanceIds.Count == 0)
                    continue;

                using (new GridInventory.Permission(inventory))
                {
                    foreach (int instanceId
                        in heavyHeartInstanceIds)
                    {
                        inventory.RemoveStartingItem(
                            instanceId
                        );

                        DungeonManager.Instance
                            .UnboundOnServer(
                                instanceId
                            );

                        string restrictionKey =
                            DungeonManager.Instance
                                .BuildGlobalItemStatKey(
                                    instanceId,
                                    "OwnRestriction"
                                );

                        DungeonManager.Instance
                            .globalItemStatTable
                            .Remove(restrictionKey);

                        MelonLogger.Msg(
                            "[AP Hard Mode] Heavy Heart " +
                            "runtime item removed. " +
                            "InstanceId=" + instanceId
                        );
                    }
                }
            }
        }

        private static void RefreshArchipelagoHardModeIndicator()
        {
            if (!IsArchipelagoSave)
                return;

            if (UIManager.Instance == null)
                return;

            UI_HardModeIndicator indicator =
                UIManager.Instance
                    .GetElement<UI_HardModeIndicator>();

            if (indicator == null)
                return;

            MethodInfo refreshMethod =
                AccessTools.Method(
                    typeof(UI_HardModeIndicator),
                    "CheckHUDMapVisible"
                );

            if (refreshMethod == null)
            {
                MelonLogger.Warning(
                    "[AP Hard Mode] Could not find " +
                    "UI_HardModeIndicator.CheckHUDMapVisible."
                );

                return;
            }

            refreshMethod.Invoke(
                indicator,
                null
            );
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

            // AP Fairy of Fortune mission:
            // clear the game on any chapter.
            //
            // This happens before the unique-weapon check intentionally.
            // Even a clear using an already-used weapon still satisfies
            // the costume mission.
            TryCompleteArchipelagoCostumeMission(
                "Luck"
            );

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

        private static bool IsAllowedArchipelagoConsoleCommand(
            string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return false;

            string lower =
                command.Trim()
                    .ToLowerInvariant();

            return
                lower == "!hint" ||
                lower.StartsWith(
                    "!hint ",
                    StringComparison.Ordinal
                ) ||

                lower == "!hint_location" ||
                lower.StartsWith(
                    "!hint_location ",
                    StringComparison.Ordinal
                ) ||

                lower == "!missing" ||
                lower.StartsWith(
                    "!missing ",
                    StringComparison.Ordinal
                ) ||

                lower == "!checked" ||
                lower.StartsWith(
                    "!checked ",
                    StringComparison.Ordinal
                ) ||

                lower == "!remaining" ||

                lower == "!players" ||

                lower == "!status" ||
                lower.StartsWith(
                    "!status ",
                    StringComparison.Ordinal
                );
        }


        public static bool TrySendArchipelagoConsoleCommand(
            string command,
            out string error)
        {
            error = null;

            if (!IsArchipelagoSave)
            {
                error =
                    "This console is only available on " +
                    "Archipelago saves.";

                return false;
            }

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null ||
                !apSession.Socket.Connected)
            {
                error =
                    "Archipelago is not connected.";

                return false;
            }

            if (string.IsNullOrWhiteSpace(command))
                return false;

            command =
                command.Trim();

            if (!IsAllowedArchipelagoConsoleCommand(
                    command))
            {
                error =
                    "Command not allowed. Type help " +
                    "for the supported commands.";

                return false;
            }

            try
            {
                QueueArchipelagoConsoleLine(
                    "> " + command
                );

                apSession.Socket.SendPacket(
                    new SayPacket
                    {
                        Text = command
                    }
                );

                return true;
            }
            catch (Exception ex)
            {
                error =
                    "Failed to send command: " +
                    ex.Message;

                return false;
            }
        }

        public static void AddArchipelagoLocalConsoleLine(
            string line)
        {
            QueueArchipelagoConsoleLine(
                line
            );
        }

        private static void ConfigureArchipelagoDeathLink(
            ArchipelagoSession apSession)
        {
            if (apSession == null)
                return;

            if (!DeathLinkEnabled)
                return;

            DeathLinkService service =
                apSession.CreateDeathLinkService();

            service.OnDeathLinkReceived +=
                HandleArchipelagoDeathLinkReceived;

            service.EnableDeathLink();

            deathLinkService = service;

            MelonLogger.Msg(
                "[AP DeathLink] Registered DeathLink service."
            );
        }


        private static void ShutdownArchipelagoDeathLink()
        {
            DeathLinkService service = deathLinkService as DeathLinkService;

            if (service == null)
                return;

            try
            {
                service.OnDeathLinkReceived -= HandleArchipelagoDeathLinkReceived;
                service.DisableDeathLink();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "[AP DeathLink] Could not cleanly disable: " +
                    ex.Message
                );
            }

            deathLinkService = null;
            pendingDeathLink = null;
            suppressNextOutgoingDeathLink = false;

            object ignoredDeathLink;

            while (receivedDeathLinks.TryDequeue(
                out ignoredDeathLink))
            {
            }
        }

        private static void HandleArchipelagoDeathLinkReceived(
            DeathLink deathLink)
        {
            if (!DeathLinkEnabled)
                return;

            if (deathLink == null)
                return;

            // Do not touch Unity/Sephiria objects here.
            // Process it from OnUpdate on the main thread.
            receivedDeathLinks.Enqueue(
                deathLink
            );
        }

        private static bool IsSafeToApplyDeathLink(
            PlayerAvatar player)
        {
            if (player == null)
                return false;

            if (player.isInDungeon <= 0)
                return false;

            if (player.IsDead)
                return false;

            if (UIManager.Instance != null &&
                UIManager.Instance.CurrentControlStack != null)
            {
                return false;
            }

            if (!player.CanMove)
                return false;

            if (Time.timeScale <= 0f)
                return false;

            return true;
        }
        private static void UpdateArchipelagoDeathLink()
        {
            if (!DeathLinkEnabled ||
                !IsArchipelagoSave)
            {
                pendingDeathLink = null;

                object ignored;

                while (receivedDeathLinks.TryDequeue(
                    out ignored))
                {
                }

                return;
            }

            // Once this run has been successfully completed,
            // DeathLink no longer applies until the next run.
            //
            // This prevents an incoming DeathLink from killing
            // the player during a victory cutscene/reset.
            if (suppressOutgoingDeathLinkForRunVictory)
            {
                pendingDeathLink = null;

                object ignoredAfterVictory;

                while (receivedDeathLinks.TryDequeue(
                    out ignoredAfterVictory))
                {
                }

                return;
            }

            // Ignore incoming DeathLinks while a locked-floor
            // forced death is ending the current run.
            if (suppressOutgoingDeathLinkForForcedFloorDeath)
            {
                pendingDeathLink = null;

                object ignoredAfterForcedDeath;

                while (receivedDeathLinks.TryDequeue(
                    out ignoredAfterForcedDeath))
                {
                }

                return;
            }

            PlayerAvatar player = null;

            if (CombatManager.Instance != null)
            {
                player =
                    CombatManager.Instance.CurrentPlayer
                        as PlayerAvatar;
            }


            object receivedObject;

            while (receivedDeathLinks.TryDequeue(
                out receivedObject))
            {
                DeathLink received =
                    receivedObject as DeathLink;

                if (received == null)
                    continue;


                // Extra protection against our own bounce.
                if (!string.IsNullOrEmpty(
                        currentArchipelagoSlotName) &&
                    string.Equals(
                        received.Source,
                        currentArchipelagoSlotName,
                        StringComparison.Ordinal))
                {
                    continue;
                }


                // No player object yet.
                // Hold it temporarily until we know whether
                // we're in a run or in town.
                if (player == null)
                {
                    pendingDeathLink = received;
                    continue;
                }


                // Explicit design choice:
                // DeathLinks received outside a run are discarded.
                if (player.isInDungeon <= 0)
                {
                    MelonLogger.Msg(
                        "[AP DeathLink] Discarded DeathLink from " +
                        received.Source +
                        " because the player is outside a run."
                    );

                    continue;
                }


                // Don't carry a DeathLink into the next life.
                if (player.IsDead)
                {
                    MelonLogger.Msg(
                        "[AP DeathLink] Discarded DeathLink because " +
                        "the player is already dead."
                    );

                    continue;
                }


                if (IsSafeToApplyDeathLink(player))
                {
                    ApplyArchipelagoDeathLink(
                        player,
                        received
                    );
                }
                else
                {
                    pendingDeathLink = received;

                    MelonLogger.Msg(
                        "[AP DeathLink] DeathLink is pending until " +
                        "the player is safely controllable."
                    );
                }
            }


            if (pendingDeathLink == null)
                return;


            if (player == null)
                return;


            // The player left/finished the run before the
            // pending DeathLink could safely fire.
            if (player.isInDungeon <= 0)
            {
                MelonLogger.Msg(
                    "[AP DeathLink] Discarded pending DeathLink " +
                    "after leaving the run."
                );

                pendingDeathLink = null;
                return;
            }


            if (player.IsDead)
            {
                pendingDeathLink = null;
                return;
            }


            if (!IsSafeToApplyDeathLink(player))
                return;


            DeathLink deathLinkToApply = pendingDeathLink as DeathLink;

            pendingDeathLink = null;

            if (deathLinkToApply == null)
                return;

            ApplyArchipelagoDeathLink(
                player,
                deathLinkToApply
            );
        }

        private static void ApplyArchipelagoDeathLink(
            PlayerAvatar player,
            DeathLink deathLink)
        {
            if (player == null ||
                deathLink == null)
            {
                return;
            }

            if (!NetworkServer.active)
            {
                pendingDeathLink = deathLink;
                return;
            }

            MelonLogger.Warning(
                 "[AP DeathLink] Received from " +
                 deathLink.Source +
                 (string.IsNullOrEmpty(deathLink.Cause)
                     ? "."
                     : ": " + deathLink.Cause)
             );

            QueueArchipelagoRecentItem(
                "DeathLink Received",
                !string.IsNullOrEmpty(deathLink.Cause)
                    ? deathLink.Cause
                    : "from " + deathLink.Source
            );

            suppressNextOutgoingDeathLink = true;

            try
            {
                player.LocalApplySystemDamage(
                    player.MaxHp * 100f
                );

                if (!player.IsDead)
                {
                    suppressNextOutgoingDeathLink = false;
                }
            }
            catch
            {
                suppressNextOutgoingDeathLink = false;

                throw;
            }
        }

        private static string GetArchipelagoDeathLinkCause(
            string source)
        {
            if (archipelagoDeathLinkMessages == null ||
                archipelagoDeathLinkMessages.Length == 0)
            {
                return source + " died in Sephiria.";
            }

            int index =
                archipelagoDeathLinkMessageRandom.Next(
                    archipelagoDeathLinkMessages.Length
                );

            return string.Format(
                archipelagoDeathLinkMessages[index],
                source
            );
        }

        public static void HandleLocalPlayerDeathForDeathLink(
            PlayerSpawner spawner)
        {
            if (!IsArchipelagoSave)
                return;

            if (!DeathLinkEnabled)
                return;

            if (spawner == null ||
                spawner.PlayerAvatar == null)
            {
                return;
            }

            if (!spawner.isLocalPlayer)
                return;

            // A chapter/run was successfully completed.
            // Sephiria may technically kill the player as part of
            // returning to town or playing the ending.
            //
            // This is not a gameplay death and must never send
            // an Archipelago DeathLink.
            if (suppressOutgoingDeathLinkForRunVictory)
            {
                // If an incoming DeathLink happened to be pending too,
                // don't let that suppression leak into the next run.
                suppressNextOutgoingDeathLink = false;

                MelonLogger.Msg(
                    "[AP DeathLink] Suppressed outgoing DeathLink " +
                    "because the run ended in victory."
                );

                return;
            }

            // The floor progression gate intentionally ended this run.
            // This is not a normal gameplay death and must not send
            // an Archipelago DeathLink.
            if (suppressOutgoingDeathLinkForForcedFloorDeath)
            {
                suppressNextOutgoingDeathLink = false;

                MelonLogger.Msg(
                    "[AP DeathLink] Suppressed outgoing DeathLink " +
                    "for a locked-floor run-ending death."
                );

                return;
            }

            // Incoming DeathLink caused this death.
            // Do not bounce it back out.
            if (suppressNextOutgoingDeathLink)
            {
                suppressNextOutgoingDeathLink = false;

                MelonLogger.Msg(
                    "[AP DeathLink] Suppressed outgoing DeathLink " +
                    "for an incoming DeathLink death."
                );

                return;
            }


            // Only deaths during actual runs participate.
            if (spawner.PlayerAvatar.isInDungeon <= 0)
                return;

            DeathLinkService service = deathLinkService as DeathLinkService;

            if (service == null ||
                !IsArchipelagoConnected)
            {
                return;
            }

            string source =
                !string.IsNullOrEmpty(
                    currentArchipelagoSlotName)
                    ? currentArchipelagoSlotName
                    : "Sephiria Player";

            string cause =
                GetArchipelagoDeathLinkCause(
                    source
                );

            try
            {
                service.SendDeathLink(
                    new DeathLink(
                        source,
                        cause
                    )
                );

                QueueArchipelagoRecentItem(
                    "DeathLink Sent",
                    cause
                );

                MelonLogger.Msg(
                    "[AP DeathLink] Sent DeathLink."
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "[AP DeathLink] Failed to send: " +
                    ex.Message
                );
            }
        }

        public static void MarkArchipelagoRunVictory()
        {
            if (!IsArchipelagoSave)
                return;

            if (!suppressOutgoingDeathLinkForRunVictory)
            {
                MelonLogger.Msg(
                    "[AP DeathLink] Run victory reached. " +
                    "Outgoing DeathLink suppressed until the next run."
                );
            }

            suppressOutgoingDeathLinkForRunVictory = true;
        }

        public static void MarkArchipelagoForcedFloorDeath()
        {
            if (!IsArchipelagoSave)
                return;

            if (!suppressOutgoingDeathLinkForForcedFloorDeath)
            {
                MelonLogger.Msg(
                    "[AP Floor Gate] Forced run-ending death. " +
                    "Outgoing DeathLink will be suppressed."
                );
            }

            suppressOutgoingDeathLinkForForcedFloorDeath = true;
        }

        public static void ResetArchipelagoRunVictory()
        {
            suppressOutgoingDeathLinkForRunVictory = false;
            suppressOutgoingDeathLinkForForcedFloorDeath = false;

            // Nothing from the previous life/run should
            // carry into the new run.
            suppressNextOutgoingDeathLink = false;
            pendingDeathLink = null;

            object ignored;

            while (receivedDeathLinks.TryDequeue(out ignored)) { }
        }
    }

    public class ArchipelagoCostumeTabController : MonoBehaviour
    {
        private UI_CostumePanel panel;

        private RectTransform tabRoot;

        private UI_HorayButton costumesButton;
        private UI_HorayButton missionsButton;

        private TextMeshProUGUI costumesText;
        private TextMeshProUGUI missionsText;

        private Image costumesUnderline;
        private Image missionsUnderline;

        private readonly List<UI_CostumeListElement>
            missionRows =
                new List<UI_CostumeListElement>();

        private readonly Dictionary<
            UI_CostumeListElement,
            string>
            missionIds =
                new Dictionary<
                    UI_CostumeListElement,
                    string>();

        private bool showingMissions;

        private static readonly UnityEngine.Color ActiveTabColor =
            new Color32(245, 228, 142, 255);

        private static readonly UnityEngine.Color InactiveTabColor =
            UnityEngine.Color.white;

        // ---------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------

        public void Initialize(
            UI_CostumePanel costumePanel)
        {
            panel = costumePanel;

            MoveCostumeHeader();

            if (tabRoot == null)
            {
                CreateTabBar();
            }

            EnsureMissionRows();
            RefreshMissionRows();

            // Always open on Costumes.
            ShowCostumes();
        }

        // ---------------------------------------------------------
        // Update
        // ---------------------------------------------------------

        private void Update()
        {
            if (!SephiriaMod.IsArchipelagoSave)
                return;

            if (panel == null ||
                !panel.gameObject.activeInHierarchy)
            {
                return;
            }

            if (!UIInputModule.current)
                return;

            bool previousTab = false;
            bool nextTab = false;

            if (ControlsChangeHandler.Current.IsUsingKeyboardAndMouse)
            {
                previousTab =
                    UIInputModule.current
                        .prevTabAction
                        .action
                        .WasPressedThisFrame();

                nextTab =
                    UIInputModule.current
                        .nextTabAction
                        .action
                        .WasPressedThisFrame();
            }
            else
            {
                Gamepad gamepad =
                    Gamepad.current;

                previousTab =
                    gamepad?.leftShoulder.wasPressedThisFrame
                    ?? false;

                nextTab =
                    gamepad?.rightShoulder.wasPressedThisFrame
                    ?? false;
            }

            if (previousTab)
            {
                ShowCostumes();
            }

            if (nextTab)
            {
                ShowMissions();
            }
        }

        // ---------------------------------------------------------
        // Header
        // ---------------------------------------------------------

        private void MoveCostumeHeader()
        {
            TextMeshProUGUI[] texts =
                panel.GetComponentsInChildren<TextMeshProUGUI>(
                    true
                );

            foreach (TextMeshProUGUI text in texts)
            {
                if (text == null ||
                    text.text != "COSTUME SELECTION")
                {
                    continue;
                }

                RectTransform titleLabel =
                    text.transform.parent as RectTransform;

                if (titleLabel == null)
                    return;

                titleLabel.anchoredPosition =
                    new Vector2(
                        titleLabel.anchoredPosition.x,
                        -16f
                    );

                return;
            }
        }

        // ---------------------------------------------------------
        // Tab bar
        // ---------------------------------------------------------

        private void CreateTabBar()
        {
            GameObject rootObject =
                new GameObject(
                    "AP Costume Tabs",
                    typeof(RectTransform)
                );

            tabRoot =
                rootObject.GetComponent<RectTransform>();

            tabRoot.SetParent(
                panel.transform,
                false
            );

            tabRoot.anchorMin =
                new Vector2(0.5f, 1f);

            tabRoot.anchorMax =
                new Vector2(0.5f, 1f);

            tabRoot.pivot =
                new Vector2(0.5f, 0.5f);

            // The large panels start at about Y=300
            // in the 360-high CostumePanel.
            //
            // This places the tabs in the gap directly
            // beneath the title.
            tabRoot.anchoredPosition =
                new Vector2(0f, -50f);

            tabRoot.sizeDelta =
                new Vector2(190f, 18f);

            costumesButton =
                CreateTabButton(
                    "Costumes Tab",
                    "COSTUMES",
                    -48f,
                    out costumesText,
                    out costumesUnderline
                );

            missionsButton =
                CreateTabButton(
                    "Missions Tab",
                    "MISSIONS",
                    48f,
                    out missionsText,
                    out missionsUnderline
                );

            costumesButton.onClick.AddListener(
                ShowCostumes
            );

            missionsButton.onClick.AddListener(
                ShowMissions
            );
        }

        private UI_HorayButton CreateTabButton(
            string objectName,
            string label,
            float x,
            out TextMeshProUGUI text,
            out Image underline)
        {
            GameObject buttonObject =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(Image)
                );

            RectTransform rect =
                buttonObject.GetComponent<RectTransform>();

            rect.SetParent(
                tabRoot,
                false
            );

            rect.anchorMin =
                new Vector2(0.5f, 0.5f);

            rect.anchorMax =
                new Vector2(0.5f, 0.5f);

            rect.pivot =
                new Vector2(0.5f, 0.5f);

            rect.anchoredPosition =
                new Vector2(x, 0f);

            rect.sizeDelta =
                new Vector2(88f, 18f);

            Image image =
                buttonObject.GetComponent<Image>();

            // Invisible clickable area.
            image.color =
                new UnityEngine.Color(0f, 0f, 0f, 0f);

            image.raycastTarget = true;

            UI_HorayButton button =
                buttonObject.AddComponent<UI_HorayButton>();

            button.targetGraphic = image;
            button.transition =
                Selectable.Transition.None;

            // Clone an existing TMP object so we use
            // Sephiria's actual font/material.
            text =
                UnityEngine.Object.Instantiate(
                    panel.tooltipNameText,
                    buttonObject.transform
                );

            text.name = "Text";
            text.text = label;
            text.alignment =
                TextAlignmentOptions.Center;

            text.enableAutoSizing = false;
            text.fontSize = 7f;
            text.color = InactiveTabColor;
            text.raycastTarget = false;

            RectTransform textRect =
                text.rectTransform;

            textRect.anchorMin =
                Vector2.zero;

            textRect.anchorMax =
                Vector2.one;

            textRect.offsetMin =
                Vector2.zero;

            textRect.offsetMax =
                Vector2.zero;

            button.text = text;

            GameObject underlineObject =
                new GameObject(
                    "Underline",
                    typeof(RectTransform),
                    typeof(Image)
                );

            RectTransform underlineRect =
                underlineObject
                    .GetComponent<RectTransform>();

            underlineRect.SetParent(
                buttonObject.transform,
                false
            );

            underlineRect.anchorMin =
                new Vector2(0.5f, 0f);

            underlineRect.anchorMax =
                new Vector2(0.5f, 0f);

            underlineRect.pivot =
                new Vector2(0.5f, 0.5f);

            underlineRect.anchoredPosition =
                new Vector2(0f, 1f);

            underlineRect.sizeDelta =
                new Vector2(68f, 1f);

            underline =
                underlineObject.GetComponent<Image>();

            underline.color = ActiveTabColor;
            underline.raycastTarget = false;

            return button;
        }

        // ---------------------------------------------------------
        // Mission rows
        // ---------------------------------------------------------

        private void EnsureMissionRows()
        {
            missionRows.RemoveAll(
                row => row == null
            );

            if (missionRows.Count > 0)
                return;

            missionIds.Clear();

            foreach (string costumeId
                in SephiriaMod
                    .GetArchipelagoCostumeMissionOrder())
            {
                string description;
                bool completed;
                string sentItem;

                if (!SephiriaMod
                        .TryGetArchipelagoCostumeMissionInfo(
                            costumeId,
                            out description,
                            out completed,
                            out sentItem))
                {
                    continue;
                }

                UI_CostumeListElement row =
                    UnityEngine.Object.Instantiate(
                        panel.costumeListElementPrefab,
                        panel.contentsZone
                    );

                row.name =
                    "AP Mission - " + costumeId;

                string costumeName = SephiriaMod.GetArchipelagoCostumeMissionName(costumeId);

                row.nameText.text = description + "\n(" + costumeName + " Mission)";
                row.nameText.enableAutoSizing = true;
                row.nameText.fontSizeMin = 4.5f;
                row.nameText.fontSizeMax = 7.5f;

                UnityEngine.Sprite apLogo =
                    SephiriaMod.GetArchipelagoSephiriteSprite(
                        panel.tooltipIconImage.sprite
                    );

                if (apLogo != null)
                {
                    row.iconImage.sprite = apLogo;
                    row.iconImage.preserveAspect = true;

                    row.iconImage.rectTransform.sizeDelta =
                        new Vector2(20f, 20f);
                }

                // Hidden until the mission is completed.
                row.iconImage.enabled = false;

                row.backgroundImage.enabled = false;

                row.hardModeBackgroundImage.enabled =
                    false;

                row.SetEquipCursor(false);

                string capturedId = costumeId;

                row.OnSelected += delegate
                {
                    ShowMissionInfo(
                        capturedId
                    );
                };

                row.OnClicked += delegate
                {
                    ShowMissionInfo(
                        capturedId
                    );
                };

                missionRows.Add(row);

                missionIds[row] =
                    costumeId;

                // Costumes is the default tab.
                row.gameObject.SetActive(false);
            }
        }

        private void RefreshMissionRows()
        {
            foreach (UI_CostumeListElement row
                in missionRows)
            {
                if (row == null)
                    continue;

                string costumeId;

                if (!missionIds.TryGetValue(
                        row,
                        out costumeId))
                {
                    continue;
                }

                string description;
                bool completed;
                string sentItem;

                if (!SephiriaMod
                        .TryGetArchipelagoCostumeMissionInfo(
                            costumeId,
                            out description,
                            out completed,
                            out sentItem))
                {
                    continue;
                }

                string costumeName = SephiriaMod.GetArchipelagoCostumeMissionName(costumeId);

                row.nameText.text = description + "\n(" + costumeName + " Mission)";

                row.nameText.color =
                    completed
                        ? ActiveTabColor
                        : UnityEngine.Color.white;

                row.iconImage.enabled =
                    completed &&
                    row.iconImage.sprite != null;
            }
        }

        // ---------------------------------------------------------
        // Switching tabs
        // ---------------------------------------------------------

        public void ShowCostumes()
        {
            if (panel == null)
                return;

            showingMissions = false;

            SetRowVisibility(
                missions: false
            );

            UpdateTabVisuals();

            panel.skinContentsZone
                .gameObject
                .SetActive(true);

            panel.tooltipIconImage
                .gameObject
                .SetActive(true);

            ResetListScroll();

            // Restore the normal tooltip using the
            // player's currently equipped costume.
            if (CombatManager.Instance != null &&
                CombatManager.Instance.CurrentPlayer != null)
            {
                PlayerAvatar player =
                    CombatManager.Instance.CurrentPlayer;

                CostumeEntity costume =
                    CostumeDatabase.FindCostumeByID(
                        player.currentCostume
                    );

                if (costume != null)
                {
                    AccessTools
                        .Method(
                            typeof(UI_CostumePanel),
                            "UpdateData"
                        )
                        .Invoke(
                            panel,
                            new object[]
                            {
                            costume
                            }
                        );
                }
            }

            if (!ControlsChangeHandler.Current
                .IsUsingKeyboardAndMouse)
            {
                foreach (Transform child
                    in panel.contentsZone)
                {
                    UI_CostumeListElement row =
                        child.GetComponent<
                            UI_CostumeListElement>();

                    if (row == null ||
                        missionRows.Contains(row) ||
                        !row.gameObject.activeInHierarchy ||
                        row.button == null)
                    {
                        continue;
                    }

                    row.button.Select();
                    break;
                }
            }
        }

        public void ShowMissions()
        {
            if (panel == null)
                return;

            showingMissions = true;

            RefreshMissionRows();

            SetRowVisibility(
                missions: true
            );

            UpdateTabVisuals();

            ResetListScroll();

            if (missionRows.Count > 0)
            {
                UI_CostumeListElement first =
                    missionRows[0];

                string costumeId;

                if (first != null &&
                    missionIds.TryGetValue(
                        first,
                        out costumeId))
                {
                    ShowMissionInfo(
                        costumeId
                    );

                    if (first.button != null)
                    {
                        first.button.Select();
                    }
                }
            }
        }

        private void SetRowVisibility(
            bool missions)
        {
            foreach (Transform child
                in panel.contentsZone)
            {
                UI_CostumeListElement row =
                    child.GetComponent<
                        UI_CostumeListElement>();

                if (row == null)
                    continue;

                bool isMission =
                    missionRows.Contains(row);

                row.gameObject.SetActive(
                    missions
                        ? isMission
                        : !isMission
                );
            }
        }

        private void UpdateTabVisuals()
        {
            costumesText.color =
                showingMissions
                    ? InactiveTabColor
                    : ActiveTabColor;

            missionsText.color =
                showingMissions
                    ? ActiveTabColor
                    : InactiveTabColor;

            costumesUnderline.gameObject.SetActive(
                !showingMissions
            );

            missionsUnderline.gameObject.SetActive(
                showingMissions
            );
        }

        private void ResetListScroll()
        {
            ScrollRect scrollRect =
                panel.contentsZone
                    .GetComponentInParent<ScrollRect>();

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition =
                    1f;
            }
        }

        // ---------------------------------------------------------
        // Mission details
        // ---------------------------------------------------------

        private void ShowMissionInfo(
            string costumeId)
        {
            string description;
            bool completed;
            string sentItem;

            if (!SephiriaMod
                    .TryGetArchipelagoCostumeMissionInfo(
                        costumeId,
                        out description,
                        out completed,
                        out sentItem))
            {
                return;
            }

            panel.tooltipBackgroundImage.sprite =
                panel.normalSprite;

            string costumeName = SephiriaMod.GetArchipelagoCostumeMissionName(costumeId);

            panel.tooltipNameText.text = "Costume Mission";

            panel.tooltipNameText.color = UnityEngine.Color.white;

            panel.tooltipFlavortextText.text = description + "\n\n(" + costumeName + " Mission)";

            panel.tooltipFlavortextText.color = UnityEngine.Color.white;

            if (completed)
            {
                panel.tooltipEffectText.text =
                    !string.IsNullOrEmpty(sentItem)
                        ? "Completed\n\nSent: " +
                          sentItem
                        : "Completed";
            }
            else
            {
                // IMPORTANT:
                // Do not expose the scouted reward.
                panel.tooltipEffectText.text =
                    "Incomplete";
            }

            panel.tooltipEffectText.color =
                UnityEngine.Color.white;

            panel.tooltipIconImage
                .gameObject
                .SetActive(false);

            panel.tooltipEquippedCursor
                .gameObject
                .SetActive(false);

            panel.tooltipLocked.SetActive(false);

            panel.tooltipUnlockConditionText.text =
                "";

            panel.hardModeValue.SetActive(false);

            panel.startingItem
                .gameObject
                .SetActive(false);

            panel.skinContentsZone
                .gameObject
                .SetActive(false);
        }
    }
}

public class ArchipelagoJournalTabState : MonoBehaviour
{
    public UI_TabButton[] OriginalButtons;
    public UI_TabContent[] OriginalContents;

    public UI_TabButton ArchipelagoButton;
    public UI_TabContent ArchipelagoContent;

    public ArchipelagoJournalContentController ArchipelagoController;

    public HorizontalLayoutGroup TabLayout;
    public float OriginalTabSpacing;

    public TextMeshProUGUI[] ArchipelagoButtonTexts;

    public bool Initialized;

    private void LateUpdate()
    {
        if (ArchipelagoButtonTexts == null)
            return;

        foreach (TextMeshProUGUI text in ArchipelagoButtonTexts)
        {
            if (text != null && text.text != "AP")
            {
                text.text = "AP";
            }
        }
    }
}

public class ArchipelagoJournalContentController : MonoBehaviour
{
    private UI_JournalContent_Lore layout;

    // Keep this name because the AP-tab controller
    // navigation already points to this first button.
    private readonly List<UI_JournalPanel_SearchOptionButton>
        chapterButtons =
            new List<UI_JournalPanel_SearchOptionButton>();

    private readonly List<GameObject>
        bossButtons =
            new List<GameObject>();

    private UI_JournalPanel_SearchOptionButton selectedFloorButton;

    private enum ArchipelagoJournalSubTab
    {
        BossRewards,
        Anvil,
        Console,
        Hints
    }

    private string selectedBossName;

    // Single AP row:
    // Boss Rewards | Anvil | Console | Hints
    private GameObject subTabRoot;
    private GameObject bossRewardsContentTemplateRoot;

    private UI_HorayButton bossRewardsTabButton;
    private UI_HorayButton anvilTabButton;
    private UI_HorayButton consoleTabButton;
    private UI_HorayButton hintsTabButton;

    private Button bossRewardsClickable;
    private Button anvilClickable;
    private Button consoleClickable;
    private Button hintsClickable;

    // Large content area underneath the tabs.
    // Used by Anvil, Console, and Hints.
    private GameObject secondaryContentRoot;
    private TextMeshProUGUI secondaryContentTitleText;
    private TextMeshProUGUI secondaryContentBodyText;
    private GameObject consoleContentRoot;
    private ScrollRect consoleScrollRect;
    private RectTransform consoleScrollContentRect;
    private TextMeshProUGUI consoleOutputText;

    private TMP_InputField consoleInputField;

    private int lastConsoleHistoryVersion = -1;
    private int lastHintHistoryVersion = -1;

    private readonly HashSet<GameObject>
        bossRewardsSectionRoots =
            new HashSet<GameObject>();

    private ArchipelagoJournalSubTab currentSubTab =
        ArchipelagoJournalSubTab.BossRewards;

    public string SelectedFloor { get; private set; } =
        "Mole Stronghold";


    // =========================================================
    // Floor -> Boss layout
    // =========================================================

    private static readonly Dictionary<string, string[]>
        bossesByFloor =
            new Dictionary<string, string[]>(
                StringComparer.Ordinal
            )
            {
                {
                    "Mole Stronghold",
                    new[]
                    {
                        "Mogdi",
                        "Odner",
                        "Haz"
                    }
                },
                {
                    "Woodlands",
                    new[]
                    {
                        "Rataka",
                        "Lerid"
                    }
                },
                {
                    "Library",
                    new[]
                    {
                        "Erma",
                        "Pentaxis"
                    }
                },
                {
                    "Wasteland",
                    new[]
                    {
                        "Askard"
                    }
                },
                {
                    "Rooted Cave",
                    new[]
                    {
                        "Kovess",
                        "Kraz"
                    }
                }
            };

    private static readonly Dictionary<string, int>
        bossFirstChapter =
            new Dictionary<string, int>(
                StringComparer.Ordinal
            )
            {
                // Chapter 1
                { "Mogdi", 1 },
                { "Rataka", 1 },
                { "Erma", 1 },
                { "Askard", 1 },

                // Chapter 2
                { "Odner", 2 },
                { "Lerid", 2 },
                { "Pentaxis", 2 },

                // Chapter 3
                { "Kovess", 3 },

                // Chapter 4
                { "Kraz", 4 },
                { "Haz", 4 }
            };


    // =========================================================
    // Initialization
    // =========================================================

    public void Initialize(
        UI_JournalContent_Lore loreLayout,
        Transform itemJournalRoot)
    {
        layout = loreLayout;

        SephiriaMod.ArchipelagoPermanentRewardsChanged -=
            RefreshRewardStates;

        SephiriaMod.ArchipelagoPermanentRewardsChanged +=
            RefreshRewardStates;

        if (layout == null ||
            layout.locationIconContainer == null ||
            layout.locationIconOriginal == null ||
            layout.loreIconContainer == null ||
            layout.loreIconOriginal == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not initialize " +
                "floor/boss journal."
            );

            return;
        }

        if (layout.loreInfo != null)
        {
            layout.loreInfo.SetActive(false);
        }

        BuildSubTabs(itemJournalRoot);

        BuildFloorButtons();

        ShowBossRewards();
    }

    private void Update()
    {
        if (SephiriaMod.IsArchipelagoSave &&
            SephiriaMod.GoalChapter >= 1 &&
            chapterButtons.Count == 0 &&
            layout != null &&
            layout.locationIconOriginal != null &&
            layout.locationIconContainer != null)
        {
            BuildFloorButtons();
        }

        if (currentSubTab ==
            ArchipelagoJournalSubTab.Console)
        {
            // Keep Console presentation correct even if
            // Sephiria reapplies cloned UI values.
            ApplyConsolePresentation();

            int version =
                SephiriaMod
                    .GetArchipelagoConsoleHistoryVersion();

            if (version !=
                lastConsoleHistoryVersion)
            {
                RefreshConsoleOutput();
            }

            return;
        }


        if (currentSubTab ==
            ArchipelagoJournalSubTab.Hints)
        {
            // Keep Hints presentation correct even after
            // closing/reopening the Journal.
            ApplyHintsPresentation();

            int version =
                SephiriaMod
                    .GetArchipelagoHintHistoryVersion();

            if (version !=
                lastHintHistoryVersion)
            {
                RefreshHintsOutput();
            }
        }
    }

    // =========================================================
    // Controller navigation
    // =========================================================

    // Keep this method name so the working AP-tab
    // controller-navigation code does not need to change.
    public Button GetFirstSubTabButton()
    {
        if (bossRewardsClickable != null)
        {
            return bossRewardsClickable;
        }

        return GetFirstFloorButton();
    }

    private Button GetFirstFloorButton()
    {
        if (chapterButtons.Count == 0 ||
            chapterButtons[0] == null)
        {
            return null;
        }

        Button button =
            chapterButtons[0]
                .GetComponent<Button>();

        if (button == null)
        {
            button =
                chapterButtons[0]
                    .GetComponentInChildren<Button>(
                        true
                    );
        }

        return button;
    }

    // =========================================================
    // AP subtabs
    // =========================================================

    private void BuildSubTabs(
        Transform itemJournalRoot)
    {
        if (itemJournalRoot == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Item journal was null. " +
                "AP subtabs were not created."
            );

            return;
        }

        Transform sourceTabRoot =
            itemJournalRoot.Find(
                "InnerTab_Button"
            );

        if (sourceTabRoot == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not find " +
                "Item journal InnerTab_Button."
            );

            return;
        }

        // Vanilla order:
        //
        // Charm
        // StoneTablet
        // Combo
        // Misc
        //
        // AP order:
        //
        // Boss Rewards
        // Anvil
        // Console
        // Hints

        Transform sourceBossTransform =
            sourceTabRoot.Find(
                "InnerTab_Button_Charm"
            );

        Transform sourceAnvilTransform =
            sourceTabRoot.Find(
                "InnerTab_Button_StoneTablet"
            );

        Transform sourceConsoleTransform =
            sourceTabRoot.Find(
                "InnerTab_Button_Combo"
            );

        if (sourceBossTransform == null ||
            sourceAnvilTransform == null ||
            sourceConsoleTransform == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not find required " +
                "Item inner-tab objects."
            );

            return;
        }

        subTabRoot =
            UnityEngine.Object.Instantiate(
                sourceTabRoot.gameObject
            );

        subTabRoot.name =
            "ArchipelagoSubTabBar";

        RectTransform sourceRect =
            sourceTabRoot.GetComponent<RectTransform>();

        RectTransform clonedRect =
            subTabRoot.GetComponent<RectTransform>();

        subTabRoot.transform.SetParent(
            layout.transform,
            false
        );

        if (sourceRect != null &&
            clonedRect != null)
        {
            clonedRect.anchorMin =
                sourceRect.anchorMin;

            clonedRect.anchorMax =
                sourceRect.anchorMax;

            clonedRect.pivot =
                sourceRect.pivot;

            clonedRect.anchoredPosition =
                sourceRect.anchoredPosition +
                new Vector2(
                    0f,
                    -8f
                );

            clonedRect.sizeDelta =
                sourceRect.sizeDelta;

            clonedRect.localScale =
                sourceRect.localScale;

            clonedRect.localRotation =
                sourceRect.localRotation;
        }


        Transform bossTransform =
            subTabRoot.transform.Find(
                "InnerTab_Button_Charm"
            );

        Transform anvilTransform =
            subTabRoot.transform.Find(
                "InnerTab_Button_StoneTablet"
            );

        Transform consoleTransform =
            subTabRoot.transform.Find(
                "InnerTab_Button_Combo"
            );

        Transform miscTransform =
            subTabRoot.transform.Find(
                "InnerTab_Button_Misc"
            );

        if (bossTransform == null ||
            anvilTransform == null ||
            consoleTransform == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not resolve cloned " +
                "Boss Rewards / Anvil / Console buttons."
            );

            UnityEngine.Object.Destroy(
                subTabRoot
            );

            subTabRoot = null;

            return;
        }

        // The vanilla Misc tab is an icon-style button,
        // so don't use it as our text-based Hints tab.
        if (miscTransform != null)
        {
            miscTransform.gameObject.SetActive(false);
        }


        // Clone the normal Console tab so Hints has the
        // same visual structure as the other text tabs.
        GameObject hintsObject =
            UnityEngine.Object.Instantiate(
                consoleTransform.gameObject,
                subTabRoot.transform
            );

        hintsObject.name =
            "InnerTab_Button_Hints";

        Transform hintsTransform =
            hintsObject.transform;

        RectTransform anvilButtonRect =
            anvilTransform.GetComponent<RectTransform>();

        RectTransform consoleButtonRect =
            consoleTransform.GetComponent<RectTransform>();

        RectTransform hintsButtonRect =
            hintsTransform.GetComponent<RectTransform>();

        if (anvilButtonRect == null ||
            consoleButtonRect == null ||
            hintsButtonRect == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not position Hints tab."
            );

            UnityEngine.Object.Destroy(
                hintsObject
            );

            return;
        }

        // Use the exact spacing already used between
        // Anvil and Console.
        Vector3 tabStep =
            consoleTransform.localPosition -
            anvilTransform.localPosition;

        hintsTransform.localPosition =
            consoleTransform.localPosition +
            tabStep;

        bossRewardsTabButton = bossTransform.GetComponent<UI_HorayButton>();
        anvilTabButton = anvilTransform.GetComponent<UI_HorayButton>();
        consoleTabButton = consoleTransform.GetComponent<UI_HorayButton>();
        hintsTabButton = hintsTransform.GetComponent<UI_HorayButton>();

        if (bossRewardsTabButton == null ||
            anvilTabButton == null ||
            consoleTabButton == null ||
            hintsTabButton == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Cloned AP tabs did not " +
                "contain UI_HorayButton."
            );

            UnityEngine.Object.Destroy(
                subTabRoot
            );

            subTabRoot = null;

            return;
        }

        SetTabLabel(
            bossRewardsTabButton,
            "Boss Rewards"
        );

        SetTabLabel(
            anvilTabButton,
            "Anvil"
        );

        SetTabLabel(
            consoleTabButton,
            "Console"
        );

        SetTabLabel(
            hintsTabButton,
            "Hints"
        );

        bossRewardsClickable = bossRewardsTabButton;
        anvilClickable = anvilTabButton;
        consoleClickable = consoleTabButton;
        hintsClickable = hintsTabButton;

        bossRewardsClickable.onClick = new Button.ButtonClickedEvent();
        anvilClickable.onClick = new Button.ButtonClickedEvent();
        consoleClickable.onClick = new Button.ButtonClickedEvent();
        hintsClickable.onClick = new Button.ButtonClickedEvent();


        bossRewardsClickable.onClick.AddListener(
            ShowBossRewards
        );

        anvilClickable.onClick.AddListener(
            ShowAnvil
        );

        consoleClickable.onClick.AddListener(
            ShowConsole
        );

        hintsClickable.onClick.AddListener(
            ShowHints
        );

        // Determine the height of the tab row.

        float tabHeight = 0f;

        RectTransform bossRect =
            sourceBossTransform
                .GetComponent<RectTransform>();

        RectTransform anvilRect =
            sourceAnvilTransform
                .GetComponent<RectTransform>();

        RectTransform consoleRect =
            sourceConsoleTransform
                .GetComponent<RectTransform>();

        if (bossRect != null)
        {
            tabHeight =
                Mathf.Max(
                    tabHeight,
                    bossRect.rect.height
                );
        }

        if (anvilRect != null)
        {
            tabHeight =
                Mathf.Max(
                    tabHeight,
                    anvilRect.rect.height
                );
        }

        if (consoleRect != null)
        {
            tabHeight =
                Mathf.Max(
                    tabHeight,
                    consoleRect.rect.height
                );
        }


        if (tabHeight > 0f)
        {
            ReserveBossRewardsSpace(
                tabHeight
            );
        }
        else
        {
            MelonLogger.Warning(
                "[AP Journal] Could not determine " +
                "subtab height."
            );
        }


        // Create the large area used by Anvil/Console.
        BuildSecondaryContentRoot();

        UpdateSubTabVisuals();
    }

    private void BuildSecondaryContentRoot()
    {
        if (layout == null ||
            bossRewardsContentTemplateRoot == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not build secondary " +
                "content root because the Boss Rewards " +
                "panel template was not found."
            );

            return;
        }

        if (secondaryContentRoot != null)
        {
            UnityEngine.Object.Destroy(
                secondaryContentRoot
            );

            secondaryContentRoot = null;
            secondaryContentTitleText = null;
            secondaryContentBodyText = null;
        }

        secondaryContentRoot =
            UnityEngine.Object.Instantiate(
                bossRewardsContentTemplateRoot
            );

        secondaryContentRoot.name =
            "ArchipelagoSecondaryContent";

        secondaryContentRoot.transform.SetParent(
            layout.transform,
            false
        );

        DisableBossRewardsPanelContents(
            secondaryContentRoot.transform
        );

        HideSecondaryContentDivider(
            secondaryContentRoot.transform
        );

        GameObject textRoot =
            new GameObject(
                "ArchipelagoSecondaryTextRoot",
                typeof(RectTransform)
            );

        RectTransform textRootRect =
            textRoot.GetComponent<RectTransform>();

        textRoot.transform.SetParent(
            secondaryContentRoot.transform,
            false
        );

        textRootRect.anchorMin =
            new Vector2(0f, 0f);

        textRootRect.anchorMax =
            new Vector2(1f, 1f);

        textRootRect.pivot =
            new Vector2(0.5f, 0.5f);

        textRootRect.offsetMin =
            new Vector2(24f, 24f);

        textRootRect.offsetMax =
            new Vector2(-24f, -24f);


        // -----------------------------------------
        // Title
        // -----------------------------------------
        if (layout.loreInfo_Title != null)
        {
            GameObject titleObject =
                UnityEngine.Object.Instantiate(
                    layout.loreInfo_Title.gameObject,
                    textRoot.transform
                );

            titleObject.name =
                "ArchipelagoSecondaryTitle";

            DisableJournalLocalization(
                titleObject
            );

            secondaryContentTitleText =
                titleObject.GetComponent<TextMeshProUGUI>();

            RectTransform titleRect =
                titleObject.GetComponent<RectTransform>();

            if (titleRect != null)
            {
                titleRect.anchorMin =
                    new Vector2(0f, 1f);

                titleRect.anchorMax =
                    new Vector2(1f, 1f);

                titleRect.pivot =
                    new Vector2(0.5f, 1f);

                titleRect.offsetMin =
                    new Vector2(
                        16f,
                        -38f
                    );

                titleRect.offsetMax =
                    new Vector2(
                        -16f,
                        -6f
                    );
            }

            if (secondaryContentTitleText != null)
            {
                secondaryContentTitleText.alignment =
                    TextAlignmentOptions.Top;

                secondaryContentTitleText.fontSize =
                    9f;

                secondaryContentTitleText.fontStyle =
                    FontStyles.Bold;

                secondaryContentTitleText.characterSpacing =
                    1f;
            }
        }

        // -----------------------------------------
        // Header background
        // -----------------------------------------

        GameObject headerBackgroundObject =
            new GameObject(
                "ArchipelagoSecondaryHeaderBackground",
                typeof(RectTransform),
                typeof(Image)
            );

        RectTransform headerBackgroundRect =
            headerBackgroundObject
                .GetComponent<RectTransform>();

        headerBackgroundRect.SetParent(
            textRoot.transform,
            false
        );

        headerBackgroundRect.anchorMin =
            new Vector2(0f, 1f);

        headerBackgroundRect.anchorMax =
            new Vector2(1f, 1f);

        headerBackgroundRect.pivot =
            new Vector2(0.5f, 1f);

        headerBackgroundRect.offsetMin =
            new Vector2(
                0f,
                -42f
            );

        headerBackgroundRect.offsetMax =
            new Vector2(
                0f,
                0f
            );

        Image headerBackground =
            headerBackgroundObject
                .GetComponent<Image>();

        headerBackground.color =
            new UnityEngine.Color(
                0f,
                0f,
                0f,
                0.20f
            );

        headerBackground.raycastTarget =
            false;


        // Keep the background behind the title.
        headerBackgroundObject.transform.SetAsFirstSibling();


        // -----------------------------------------
        // Thin divider under header
        // -----------------------------------------

        GameObject headerDividerObject =
            new GameObject(
                "ArchipelagoSecondaryHeaderDivider",
                typeof(RectTransform),
                typeof(Image)
            );

        RectTransform headerDividerRect =
            headerDividerObject
                .GetComponent<RectTransform>();

        headerDividerRect.SetParent(
            textRoot.transform,
            false
        );

        headerDividerRect.anchorMin =
            new Vector2(0f, 1f);

        headerDividerRect.anchorMax =
            new Vector2(1f, 1f);

        headerDividerRect.pivot =
            new Vector2(0.5f, 1f);

        headerDividerRect.offsetMin =
            new Vector2(
                8f,
                -43f
            );

        headerDividerRect.offsetMax =
            new Vector2(
                -8f,
                -42f
            );

        Image headerDivider =
            headerDividerObject
                .GetComponent<Image>();

        headerDivider.color =
            new UnityEngine.Color(
                1f,
                1f,
                1f,
                0.18f
            );

        headerDivider.raycastTarget =
            false;

        // -----------------------------------------
        // Body
        // -----------------------------------------
        if (layout.loreInfo_Description != null)
        {
            GameObject bodyObject =
                UnityEngine.Object.Instantiate(
                    layout.loreInfo_Description.gameObject,
                    textRoot.transform
                );

            bodyObject.name =
                "ArchipelagoSecondaryBody";

            DisableJournalLocalization(
                bodyObject
            );

            secondaryContentBodyText =
                bodyObject.GetComponent<TextMeshProUGUI>();

            RectTransform bodyRect =
                bodyObject.GetComponent<RectTransform>();

            if (bodyRect != null)
            {
                bodyRect.anchorMin =
                    new Vector2(0f, 0f);

                bodyRect.anchorMax =
                    new Vector2(1f, 1f);

                bodyRect.pivot =
                    new Vector2(0.5f, 0.5f);

                bodyRect.offsetMin =
                    new Vector2(16f, 16f);

                bodyRect.offsetMax =
                    new Vector2(-16f, -72f);
            }

            if (secondaryContentTitleText != null)
            {
                secondaryContentTitleText.alignment =
                    TextAlignmentOptions.Center;

                secondaryContentTitleText.fontSize =
                    9.5f;

                secondaryContentTitleText.fontStyle =
                    FontStyles.Bold;

                secondaryContentTitleText.characterSpacing =
                    1f;

                secondaryContentTitleText.color =
                    new UnityEngine.Color(
                        1f,
                        0.88f,
                        0.62f,
                        1f
                    );
            }
        }

        BuildConsoleContent();

        secondaryContentRoot.SetActive(
            false
        );
    }

    private void SubmitConsoleCommand(
        string rawCommand)
    {
        string command =
            rawCommand == null
                ? ""
                : rawCommand.Trim();

        if (string.IsNullOrEmpty(command))
        {
            ReactivateConsoleInput();
            return;
        }


        // Local-only command.
        if (string.Equals(
                command,
                "clear",
                StringComparison.OrdinalIgnoreCase))
        {
            SephiriaMod
                .ClearArchipelagoConsoleHistory();

            RefreshConsoleOutput();

            ReactivateConsoleInput();

            return;
        }


        // Local-only command.
        if (string.Equals(
            command,
            "help",
            StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
            command,
            "!help",
            StringComparison.OrdinalIgnoreCase))
        {
            AddLocalConsoleHelp();

            RefreshConsoleOutput();

            ReactivateConsoleInput();

            return;
        }


        string error;

        if (!SephiriaMod
                .TrySendArchipelagoConsoleCommand(
                    command,
                    out error))
        {
            AddLocalConsoleLine(
                "[Local] " + error
            );
        }

        RefreshConsoleOutput();

        ReactivateConsoleInput();
    }


    private void AddLocalConsoleHelp()
    {
        AddLocalConsoleLine(
            "Allowed commands:"
        );

        AddLocalConsoleLine(
            "!hint [item]"
        );

        AddLocalConsoleLine(
            "!hint_location [location]"
        );

        AddLocalConsoleLine(
            "!missing [filter]"
        );

        AddLocalConsoleLine(
            "!checked [filter]"
        );

        AddLocalConsoleLine(
            "!remaining"
        );

        AddLocalConsoleLine(
            "!players"
        );

        AddLocalConsoleLine(
            "!status [tag]"
        );

        AddLocalConsoleLine(
            "clear"
        );
    }


    private void AddLocalConsoleLine(
        string line)
    {
        // Local console messages can use the same global
        // history without ever being sent to the server.
        SephiriaMod
            .AddArchipelagoLocalConsoleLine(
                line
            );
    }


    private void ReactivateConsoleInput()
    {
        if (consoleInputField == null)
            return;

        consoleInputField.text = "";

        consoleInputField.Select();

        consoleInputField.ActivateInputField();
    }

    private void RefreshConsoleOutput()
    {
        if (consoleOutputText == null ||
            consoleScrollContentRect == null ||
            consoleScrollRect == null)
        {
            return;
        }

        ApplyConsolePresentation();

        List<string> history =
            SephiriaMod
                .GetArchipelagoConsoleHistory();

        string output =
            history.Count > 0
                ? string.Join(
                    "\n",
                    history.ToArray()
                )
                : "Type help for available commands.";

        consoleOutputText.text =
            output;

        lastConsoleHistoryVersion =
            SephiriaMod
                .GetArchipelagoConsoleHistoryVersion();


        float availableWidth =
            consoleScrollRect
                .viewport
                .rect
                .width;

        if (availableWidth <= 0f)
            return;

        Vector2 preferred =
            consoleOutputText.GetPreferredValues(
                output,
                availableWidth,
                0f
            );

        float minimumHeight =
            consoleScrollRect
                .viewport
                .rect
                .height;

        float contentHeight =
            Mathf.Max(
                minimumHeight,
                preferred.y + 8f
            );


        consoleScrollContentRect.sizeDelta =
            new Vector2(
                0f,
                contentHeight
            );

        consoleOutputText.rectTransform.sizeDelta =
            new Vector2(
                0f,
                contentHeight
            );


        // 0 = bottom for a vertical ScrollRect.
        consoleScrollRect
            .verticalNormalizedPosition = 0f;
    }

    private void BuildConsoleContent()
    {
        if (secondaryContentRoot == null)
            return;

        UI_MessageBox_InputYesNo inputPopup =
            UIManager.Instance
                .GetElement<UI_MessageBox_InputYesNo>();

        if (inputPopup == null ||
            inputPopup.input == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not find a TMP input " +
                "template for the AP Console."
            );

            return;
        }


        // =================================================
        // Console root
        // =================================================

        consoleContentRoot =
            new GameObject(
                "ArchipelagoConsoleContent",
                typeof(RectTransform)
            );

        RectTransform consoleRect =
            consoleContentRoot
                .GetComponent<RectTransform>();

        consoleRect.SetParent(
            secondaryContentRoot.transform,
            false
        );

        consoleRect.anchorMin =
            new Vector2(0f, 0f);

        consoleRect.anchorMax =
            new Vector2(1f, 1f);

        consoleRect.offsetMin =
            Vector2.zero;

        consoleRect.offsetMax =
            Vector2.zero;


        // =================================================
        // Scroll area
        // =================================================

        GameObject scrollObject =
            new GameObject(
                "ConsoleScroll",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScrollRect)
            );

        RectTransform scrollRectTransform =
            scrollObject
                .GetComponent<RectTransform>();

        scrollRectTransform.SetParent(
            consoleContentRoot.transform,
            false
        );

        scrollRectTransform.anchorMin =
            new Vector2(0f, 0f);

        scrollRectTransform.anchorMax =
            new Vector2(1f, 1f);

        // Leave room for title above and input below.
        scrollRectTransform.offsetMin =
            new Vector2(
                18f,
                30f
            );

        scrollRectTransform.offsetMax =
            new Vector2(
                -18f,
                -78f
            );

        Image scrollImage =
            scrollObject.GetComponent<Image>();

        scrollImage.color =
            new UnityEngine.Color(
                0f,
                0f,
                0f,
                0f
            );

        scrollImage.raycastTarget =
            true;

        consoleScrollRect =
            scrollObject.GetComponent<ScrollRect>();

        consoleScrollRect.horizontal =
            false;

        consoleScrollRect.vertical =
            true;

        consoleScrollRect.movementType =
            ScrollRect.MovementType.Clamped;

        consoleScrollRect.scrollSensitivity =
            1f;


        // =================================================
        // Viewport
        // =================================================

        GameObject viewportObject =
            new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(RectMask2D)
            );

        RectTransform viewportRect =
            viewportObject
                .GetComponent<RectTransform>();

        viewportRect.SetParent(
            scrollObject.transform,
            false
        );

        viewportRect.anchorMin =
            new Vector2(0f, 0f);

        viewportRect.anchorMax =
            new Vector2(1f, 1f);

        viewportRect.offsetMin =
            Vector2.zero;

        viewportRect.offsetMax =
            Vector2.zero;


        // =================================================
        // Scroll content
        // =================================================

        GameObject contentObject =
            new GameObject(
                "Content",
                typeof(RectTransform)
            );

        consoleScrollContentRect =
            contentObject
                .GetComponent<RectTransform>();

        consoleScrollContentRect.SetParent(
            viewportObject.transform,
            false
        );

        consoleScrollContentRect.anchorMin =
            new Vector2(0f, 1f);

        consoleScrollContentRect.anchorMax =
            new Vector2(1f, 1f);

        consoleScrollContentRect.pivot =
            new Vector2(0.5f, 1f);

        consoleScrollContentRect.anchoredPosition =
            Vector2.zero;


        // =================================================
        // Output text
        // =================================================

        GameObject outputObject =
            UnityEngine.Object.Instantiate(
                layout.loreInfo_Description.gameObject,
                contentObject.transform
            );

        outputObject.name =
            "ArchipelagoConsoleOutput";

        DisableJournalLocalization(
            outputObject
        );

        consoleOutputText =
            outputObject
                .GetComponent<TextMeshProUGUI>();

        RectTransform outputRect =
            outputObject
                .GetComponent<RectTransform>();

        outputRect.anchorMin =
            new Vector2(0f, 1f);

        outputRect.anchorMax =
            new Vector2(1f, 1f);

        outputRect.pivot =
            new Vector2(0.5f, 1f);

        outputRect.anchoredPosition =
            new Vector2(
                0f,
                -3f
            );

        consoleOutputText.alignment =
            TextAlignmentOptions.TopLeft;

        consoleOutputText.fontSize = 7f;

        consoleOutputText.margin =
            new Vector4(
                2f,
                2f,
                2f,
                2f
            );

        consoleOutputText.enableWordWrapping =
            true;

        consoleOutputText.richText =
            false;

        consoleOutputText.raycastTarget =
            false;


        consoleScrollRect.viewport =
            viewportRect;

        consoleScrollRect.content =
            consoleScrollContentRect;


        // =================================================
        // Input
        // =================================================

        GameObject inputObject =
            UnityEngine.Object.Instantiate(
                inputPopup.input.gameObject,
                consoleContentRoot.transform
            );

        inputObject.name =
            "ArchipelagoConsoleInput";

        consoleInputField =
            inputObject.GetComponent<TMP_InputField>();

        RectTransform inputRect =
            inputObject.GetComponent<RectTransform>();

        inputRect.anchorMin =
            new Vector2(0f, 0f);

        inputRect.anchorMax =
            new Vector2(0.65f, 0f);

        inputRect.pivot =
            new Vector2(0.5f, 0f);

        inputRect.offsetMin =
            new Vector2(
                18f,
                7f
            );

        inputRect.offsetMax =
            new Vector2(
                -6f,
                29f
            );


        consoleInputField.text = "";

        consoleInputField.characterLimit =
            200;

        consoleInputField.contentType =
            TMP_InputField.ContentType.Standard;

        consoleInputField.lineType =
            TMP_InputField.LineType.SingleLine;

        if (consoleInputField.textComponent != null)
        {
            consoleInputField.textComponent.fontSize =
                7f;
        }

        TMP_Text placeholder = consoleInputField.placeholder as TMP_Text;

        if (placeholder != null)
        {
            placeholder.text =
                "Type a command...  (help for commands)";

            placeholder.fontSize =
                6.5f;
        }

        // Remove anything inherited from the popup field.
        consoleInputField
            .onSubmit
            .RemoveAllListeners();

        consoleInputField
            .onEndEdit
            .RemoveAllListeners();

        consoleInputField
            .onSubmit
            .AddListener(
                SubmitConsoleCommand
            );


        consoleContentRoot.SetActive(false);
    }

    private void DisableBossRewardsPanelContents(
        Transform root)
    {
        if (root == null)
            return;

        Button[] buttons =
            root.GetComponentsInChildren<Button>(
                true
            );

        foreach (Button button in buttons)
        {
            if (button != null)
            {
                button.gameObject.SetActive(
                    false
                );
            }
        }

        Scrollbar[] scrollbars =
            root.GetComponentsInChildren<Scrollbar>(
                true
            );

        foreach (Scrollbar scrollbar in scrollbars)
        {
            if (scrollbar != null)
            {
                scrollbar.gameObject.SetActive(
                    false
                );
            }
        }

        TextMeshProUGUI[] texts =
            root.GetComponentsInChildren<
                TextMeshProUGUI>(
                    true
                );

        foreach (TextMeshProUGUI text in texts)
        {
            if (text != null)
            {
                text.gameObject.SetActive(
                    false
                );
            }
        }
    }

    private void HideSecondaryContentDivider(
        Transform clonedRoot)
    {
        if (clonedRoot == null ||
            bossRewardsContentTemplateRoot == null ||
            layout == null ||
            layout.locationIconContainer == null ||
            layout.loreIconContainer == null)
        {
            return;
        }

        RectTransform templateRootRect =
            bossRewardsContentTemplateRoot
                .GetComponent<RectTransform>();

        RectTransform clonedRootRect =
            clonedRoot
                .GetComponent<RectTransform>();

        RectTransform leftContentRect =
            layout.locationIconContainer
                .GetComponent<RectTransform>();

        RectTransform rightContentRect =
            layout.loreIconContainer
                .GetComponent<RectTransform>();

        if (templateRootRect == null ||
            clonedRootRect == null ||
            leftContentRect == null ||
            rightContentRect == null)
        {
            return;
        }


        // -------------------------------------------------
        // Work out where Boss Rewards actually splits its
        // left and right columns.
        // -------------------------------------------------

        Bounds leftBounds =
            GetJournalBoundsRelativeTo(
                leftContentRect,
                templateRootRect
            );

        Bounds rightBounds =
            GetJournalBoundsRelativeTo(
                rightContentRect,
                templateRootRect
            );

        float dividerX =
            (
                leftBounds.max.x +
                rightBounds.min.x
            ) / 2f;


        float rootWidth =
            clonedRootRect.rect.width;

        float rootHeight =
            clonedRootRect.rect.height;


        // -------------------------------------------------
        // Find the narrow/tall image located at that split.
        //
        // This avoids relying on a guessed GameObject name.
        // -------------------------------------------------

        Image[] images =
            clonedRoot.GetComponentsInChildren<Image>(
                true
            );

        foreach (Image image in images)
        {
            if (image == null)
                continue;

            RectTransform imageRect =
                image.rectTransform;

            Bounds imageBounds =
                GetJournalBoundsRelativeTo(
                    imageRect,
                    clonedRootRect
                );

            float imageWidth =
                imageBounds.size.x;

            float imageHeight =
                imageBounds.size.y;

            float distanceFromDivider =
                Mathf.Abs(
                    imageBounds.center.x -
                    dividerX
                );

            bool isNarrow =
                imageWidth <=
                rootWidth * 0.08f;

            bool isTall =
                imageHeight >=
                rootHeight * 0.50f;

            bool isAtDivider =
                distanceFromDivider <=
                rootWidth * 0.08f;

            if (isNarrow &&
                isTall &&
                isAtDivider)
            {
                image.gameObject.SetActive(
                    false
                );
            }
        }
    }


    private Bounds GetJournalBoundsRelativeTo(
        RectTransform rect,
        Transform relativeTo)
    {
        Vector3[] worldCorners =
            new Vector3[4];

        rect.GetWorldCorners(
            worldCorners
        );

        Vector3 firstPoint =
            relativeTo.InverseTransformPoint(
                worldCorners[0]
            );

        Bounds bounds =
            new Bounds(
                firstPoint,
                Vector3.zero
            );

        for (int i = 1;
             i < worldCorners.Length;
             i++)
        {
            Vector3 point =
                relativeTo.InverseTransformPoint(
                    worldCorners[i]
                );

            bounds.Encapsulate(
                point
            );
        }

        return bounds;
    }

    private void ReserveBossRewardsSpace(
        float tabHeight)
    {
        bossRewardsSectionRoots.Clear();

        RegisterBossRewardsSection(
            layout.locationIconContainer
        );

        RegisterBossRewardsSection(
            layout.loreIconContainer
        );


        // -------------------------------------------------
        // Save the large Boss Rewards panel as the visual
        // template for Anvil / Console.
        //
        // locationIconContainer and loreIconContainer should
        // both resolve to the same top-level content panel.
        // -------------------------------------------------

        bossRewardsContentTemplateRoot = null;

        if (bossRewardsSectionRoots.Count == 1)
        {
            foreach (GameObject section
                in bossRewardsSectionRoots)
            {
                bossRewardsContentTemplateRoot =
                    section;

                break;
            }
        }
        else
        {
            MelonLogger.Warning(
                "[AP Journal] Expected one shared Boss Rewards " +
                "content root, but found " +
                bossRewardsSectionRoots.Count +
                ". Anvil/Console background was not assigned."
            );
        }


        foreach (GameObject section
            in bossRewardsSectionRoots)
        {
            if (section == null)
                continue;

            RectTransform rect =
                section.GetComponent<RectTransform>();

            if (rect == null)
                continue;

            Vector2 offsetMax =
                rect.offsetMax;

            offsetMax.y -=
                tabHeight;

            rect.offsetMax =
                offsetMax;
        }
    }

    private void DisableJournalLocalization(
        GameObject root)
    {
        if (root == null)
            return;

        MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<
                MonoBehaviour>(
                    true
                );

        foreach (MonoBehaviour behaviour
            in behaviours)
        {
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name ==
                "UI_LocalizationStringText")
            {
                behaviour.enabled = false;
            }
        }
    }

    public void ConfigureNavigation(
        Button topTabButton)
    {
        if (topTabButton == null)
            return;

        Button firstFloorButton =
            GetFirstFloorButton();

        Button firstSelectable =
            bossRewardsClickable != null
                ? bossRewardsClickable
                : firstFloorButton;


        // AP top button.

        Selectable left =
            topTabButton.FindSelectableOnLeft();

        Selectable right =
            topTabButton.FindSelectableOnRight();

        Selectable up =
            topTabButton.FindSelectableOnUp();

        Navigation topNavigation =
            topTabButton.navigation;

        topNavigation.mode =
            Navigation.Mode.Explicit;

        topNavigation.selectOnLeft = left;
        topNavigation.selectOnRight = right;
        topNavigation.selectOnUp = up;
        topNavigation.selectOnDown =
            firstSelectable;

        topTabButton.navigation =
            topNavigation;


        if (bossRewardsClickable == null ||
            anvilClickable == null ||
            consoleClickable == null ||
            hintsClickable == null)
        {
            return;
        }


        // Boss Rewards

        Navigation bossNavigation =
            bossRewardsClickable.navigation;

        bossNavigation.mode =
            Navigation.Mode.Explicit;

        bossNavigation.selectOnLeft = null;

        bossNavigation.selectOnRight =
            anvilClickable;

        bossNavigation.selectOnUp =
            topTabButton;

        bossNavigation.selectOnDown =
            firstFloorButton;

        bossRewardsClickable.navigation =
            bossNavigation;


        // Anvil

        Navigation anvilNavigation =
            anvilClickable.navigation;

        anvilNavigation.mode =
            Navigation.Mode.Explicit;

        anvilNavigation.selectOnLeft =
            bossRewardsClickable;

        anvilNavigation.selectOnRight =
            consoleClickable;

        anvilNavigation.selectOnUp =
            topTabButton;

        anvilNavigation.selectOnDown =
            null;

        anvilClickable.navigation =
            anvilNavigation;


        // Console

        Navigation consoleNavigation =
            consoleClickable.navigation;

        consoleNavigation.mode =
            Navigation.Mode.Explicit;

        consoleNavigation.selectOnLeft =
            anvilClickable;

        consoleNavigation.selectOnRight =
            hintsClickable;

        consoleNavigation.selectOnUp =
            topTabButton;

        // Later this points to the command input.
        consoleNavigation.selectOnDown =
            consoleInputField;

        consoleClickable.navigation =
            consoleNavigation;

        if (consoleInputField != null)
        {
            Navigation inputNavigation =
                consoleInputField.navigation;

            inputNavigation.mode =
                Navigation.Mode.Explicit;

            inputNavigation.selectOnUp =
                consoleClickable;

            inputNavigation.selectOnDown =
                null;

            consoleInputField.navigation =
                inputNavigation;
        }

        if (hintsClickable != null)
        {
            Navigation hintsNavigation =
                hintsClickable.navigation;

            hintsNavigation.mode =
                Navigation.Mode.Explicit;

            hintsNavigation.selectOnLeft =
                consoleClickable;

            hintsNavigation.selectOnRight =
                null;

            hintsNavigation.selectOnUp =
                topTabButton;

            hintsNavigation.selectOnDown =
                null;

            hintsClickable.navigation =
                hintsNavigation;
        }

        // First floor button back to Boss Rewards.

        if (firstFloorButton != null)
        {
            Selectable floorLeft =
                firstFloorButton.FindSelectableOnLeft();

            Selectable floorRight =
                firstFloorButton.FindSelectableOnRight();

            Selectable floorDown =
                firstFloorButton.FindSelectableOnDown();

            Navigation floorNavigation =
                firstFloorButton.navigation;

            floorNavigation.mode =
                Navigation.Mode.Explicit;

            floorNavigation.selectOnLeft =
                floorLeft;

            floorNavigation.selectOnRight =
                floorRight;

            floorNavigation.selectOnUp =
                bossRewardsClickable;

            floorNavigation.selectOnDown =
                floorDown;

            firstFloorButton.navigation =
                floorNavigation;
        }
    }

    private void SetTabLabel(
        UI_HorayButton tabButton,
        string label)
    {
        if (tabButton == null)
            return;

        // Disable the vanilla localization component on
        // this cloned button. Otherwise Sephiria could
        // replace our custom text with Artifact/Tablet
        // again when localization refreshes.
        MonoBehaviour[] behaviours =
            tabButton
                .GetComponentsInChildren<MonoBehaviour>(
                    true
                );

        foreach (MonoBehaviour behaviour
            in behaviours)
        {
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name ==
                "UI_LocalizationStringText")
            {
                behaviour.enabled = false;
            }
        }

        TextMeshProUGUI[] texts =
            tabButton
                .GetComponentsInChildren<
                    TextMeshProUGUI>(
                        true
                    );

        foreach (TextMeshProUGUI text
            in texts)
        {
            if (text != null)
            {
                text.text = label;
            }
        }
    }

    private void RegisterBossRewardsSection(
        Transform child)
    {
        if (child == null ||
            layout == null)
        {
            return;
        }

        Transform current =
            child;

        // Find the highest object belonging to this
        // section without leaving the AP content root.
        while (current.parent != null &&
               current.parent != layout.transform)
        {
            current =
                current.parent;
        }

        if (current.parent != layout.transform)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not resolve a " +
                "Boss Rewards section root."
            );

            return;
        }

        bossRewardsSectionRoots.Add(
            current.gameObject
        );
    }

    private void SetBossRewardsSectionsVisible(
        bool visible)
    {
        foreach (GameObject section
            in bossRewardsSectionRoots)
        {
            if (section != null)
            {
                section.SetActive(
                    visible
                );
            }
        }
    }

    private void ShowBossRewards()
    {
        currentSubTab =
            ArchipelagoJournalSubTab.BossRewards;

        SetBossRewardsSectionsVisible(
            true
        );

        if (secondaryContentRoot != null)
        {
            secondaryContentRoot.SetActive(
                false
            );
        }

        UpdateSubTabVisuals();

        if (!string.IsNullOrEmpty(
                selectedBossName))
        {
            ShowBossInfo(
                selectedBossName
            );
        }
        else if (layout != null &&
                 layout.loreInfo != null)
        {
            layout.loreInfo.SetActive(false);
        }
    }

    private void ShowAnvil()
    {
        currentSubTab =
            ArchipelagoJournalSubTab.Anvil;

        SetBossRewardsSectionsVisible(
            false
        );

        HideBossInfoPanel();

        RefreshAnvilContent();

        UpdateSubTabVisuals();
    }

    private void ApplyConsolePresentation()
    {
        if (consoleOutputText != null)
        {
            consoleOutputText.fontSize =
                7f;

            consoleOutputText.richText =
                false;
        }

        if (consoleScrollRect != null)
        {
            RectTransform scrollRect =
                consoleScrollRect
                    .GetComponent<RectTransform>();

            if (scrollRect != null)
            {
                Vector2 offsetMin =
                    scrollRect.offsetMin;

                // Leave room for the Console input box.
                offsetMin.y =
                    30f;

                scrollRect.offsetMin =
                    offsetMin;
            }
        }
    }


    private void ApplyHintsPresentation()
    {
        if (consoleOutputText != null)
        {
            consoleOutputText.fontSize =
                6.5f;

            // Required for <color=...>FOUND</color>.
            consoleOutputText.richText =
                true;
        }

        if (consoleScrollRect != null)
        {
            RectTransform scrollRect =
                consoleScrollRect
                    .GetComponent<RectTransform>();

            if (scrollRect != null)
            {
                Vector2 offsetMin =
                    scrollRect.offsetMin;

                // Hints has no input field, so use
                // nearly all of the bottom space.
                offsetMin.y =
                    8f;

                scrollRect.offsetMin =
                    offsetMin;
            }
        }
    }

    private void ShowConsole()
    {
        currentSubTab =
            ArchipelagoJournalSubTab.Console;

        SetBossRewardsSectionsVisible(
            false
        );

        HideBossInfoPanel();


        if (secondaryContentRoot != null)
        {
            secondaryContentRoot.SetActive(
                true
            );
        }


        if (secondaryContentTitleText != null)
        {
            secondaryContentTitleText
                .gameObject
                .SetActive(true);

            secondaryContentTitleText.text =
                "Console";
        }


        // Anvil's normal body isn't used here.
        if (secondaryContentBodyText != null)
        {
            secondaryContentBodyText
                .gameObject
                .SetActive(false);
        }


        if (consoleContentRoot != null)
        {
            consoleContentRoot.SetActive(true);
        }

        if (consoleInputField != null)
        {
            consoleInputField
                .gameObject
                .SetActive(true);
        }

        ApplyConsolePresentation();

        RefreshConsoleOutput();

        UpdateSubTabVisuals();

        ReactivateConsoleInput();
    }

    private void ShowHints()
    {
        currentSubTab =
            ArchipelagoJournalSubTab.Hints;

        SetBossRewardsSectionsVisible(
            false
        );

        HideBossInfoPanel();


        if (secondaryContentRoot != null)
        {
            secondaryContentRoot.SetActive(
                true
            );
        }


        if (secondaryContentTitleText != null)
        {
            secondaryContentTitleText
                .gameObject
                .SetActive(true);

            secondaryContentTitleText.text =
                "Hints";
        }


        if (secondaryContentBodyText != null)
        {
            secondaryContentBodyText
                .gameObject
                .SetActive(false);
        }


        if (consoleContentRoot != null)
        {
            consoleContentRoot.SetActive(
                true
            );
        }


        if (consoleInputField != null)
        {
            consoleInputField
                .gameObject
                .SetActive(false);
        }

        ApplyHintsPresentation();

        RefreshHintsOutput();

        UpdateSubTabVisuals();
    }

    private void RefreshHintsOutput()
    {
        if (consoleOutputText == null ||
            consoleScrollContentRect == null ||
            consoleScrollRect == null)
        {
            return;
        }

        ApplyHintsPresentation();

        List<string> entries = SephiriaMod
            .GetArchipelagoHintEntries();

        string output =
            entries.Count > 0
                ? string.Join(
                    "\n\n",
                    entries.ToArray()
                )
                : "No hints have been unlocked yet.";


        consoleOutputText.text =
            output;

        lastHintHistoryVersion =
            SephiriaMod
                .GetArchipelagoHintHistoryVersion();


        float availableWidth =
            consoleScrollRect
                .viewport
                .rect
                .width;

        if (availableWidth <= 0f)
            return;

        Vector2 preferred =
            consoleOutputText.GetPreferredValues(
                output,
                availableWidth,
                0f
            );

        float minimumHeight =
            consoleScrollRect
                .viewport
                .rect
                .height;

        float contentHeight =
            Mathf.Max(
                minimumHeight,
                preferred.y + 8f
            );


        consoleScrollContentRect.sizeDelta =
            new Vector2(
                0f,
                contentHeight
            );

        consoleOutputText.rectTransform.sizeDelta =
            new Vector2(
                0f,
                contentHeight
            );

        consoleScrollRect
            .verticalNormalizedPosition = 1f;
    }

    private void HideBossInfoPanel()
    {
        if (layout != null &&
            layout.loreInfo != null)
        {
            layout.loreInfo.SetActive(
                false
            );
        }
    }


    private void ShowSecondaryContent(
        string title,
        string body)
    {
        if (consoleContentRoot != null)
        {
            consoleContentRoot.SetActive(false);
        }

        if (secondaryContentTitleText != null)
        {
            secondaryContentTitleText
                .gameObject
                .SetActive(true);
        }

        if (secondaryContentBodyText != null)
        {
            secondaryContentBodyText
                .gameObject
                .SetActive(true);
        }

        if (secondaryContentRoot != null)
        {
            secondaryContentRoot.SetActive(
                true
            );
        }

        if (secondaryContentTitleText != null)
        {
            secondaryContentTitleText.text =
                title ?? "";
        }

        if (secondaryContentBodyText != null)
        {
            secondaryContentBodyText.text =
                body ?? "";
        }
    }


    private void RefreshAnvilContent()
    {
        if (!SephiriaMod.IsArchipelagoConnected)
        {
            ShowSecondaryContent(
                "Progressive Anvil Unlock",
                "Archipelago is disconnected."
            );

            return;
        }

        int anvilUnlockCount =
            SephiriaMod
                .GetProgressiveAnvilUpgradeCount();

        string firstState =
            anvilUnlockCount >= 1
                ? "<color=#00FF00>UNLOCKED</color>"
                : "LOCKED";

        string secondState =
            anvilUnlockCount >= 2
                ? "<color=#00FF00>UNLOCKED</color>"
                : "LOCKED";

        string body =
            "Progressive Anvil Unlocks: " +
            anvilUnlockCount +
            "/2" +
            "\n\n" +
            "Anvil Unlock I" +
            "\n" +
            firstState +
            "\n\n" +
            "Anvil Unlock II" +
            "\n" +
            secondState;

        ShowSecondaryContent(
            "Progressive Anvil Unlock",
            body
        );
    }


    private void UpdateSubTabVisuals()
    {
        SetInnerTabActive(
            bossRewardsTabButton,
            currentSubTab ==
                ArchipelagoJournalSubTab.BossRewards
        );

        SetInnerTabActive(
            anvilTabButton,
            currentSubTab ==
                ArchipelagoJournalSubTab.Anvil
        );

        SetInnerTabActive(
            consoleTabButton,
            currentSubTab ==
                ArchipelagoJournalSubTab.Console
        );

        SetInnerTabActive(
            hintsTabButton,
            currentSubTab ==
                ArchipelagoJournalSubTab.Hints
        );
    }

    private void SetInnerTabActive(
        UI_HorayButton tabButton,
        bool active)
    {
        if (tabButton == null)
            return;

        Transform activeIndicator =
            tabButton.transform.Find(
                "innerTabActive"
            );

        if (activeIndicator != null)
        {
            activeIndicator.gameObject.SetActive(
                active
            );
        }
    }

    // =========================================================
    // Floor list
    // =========================================================

    private void BuildFloorButtons()
    {
        if (chapterButtons.Count > 0)
            return;

        string[] floors =
        {
            "Mole Stronghold",
            "Woodlands",
            "Library",
            "Wasteland",
            "Rooted Cave"
        };

        foreach (string floor in floors)
        {
            bool hasAvailableBoss =
                bossesByFloor[floor].Any(
                    bossName =>
                        bossFirstChapter[bossName] <=
                        SephiriaMod.GoalChapter
                );

            if (!hasAvailableBoss)
                continue;

            UI_JournalPanel_SearchOptionButton button =
                UnityEngine.Object.Instantiate(
                    layout.locationIconOriginal,
                    layout.locationIconContainer
                );

            button.gameObject.name =
                "APFloor_" +
                floor.Replace(" ", "");

            button.gameObject.SetActive(true);

            button.Initialize(
                floor,
                floor,
                OnFloorClicked
            );

            Transform redDot =
                button.transform.Find("RedDot");

            if (redDot != null)
            {
                redDot.gameObject.SetActive(false);
            }

            button.SetSelected(false);

            chapterButtons.Add(button);
        }

        if (chapterButtons.Count > 0)
        {
            SelectFloor(
                chapterButtons[0]
            );
        }
    }


    // =========================================================
    // Floor selection
    // =========================================================

    private void OnFloorClicked(
        UI_JournalPanel_SearchOptionButton button)
    {
        SelectFloor(button);
    }

    private void SelectFloor(
        UI_JournalPanel_SearchOptionButton button)
    {
        if (button == null)
            return;

        if (selectedFloorButton != null &&
            selectedFloorButton != button)
        {
            selectedFloorButton.SetSelected(false);
        }

        selectedFloorButton = button;

        selectedFloorButton.SetSelected(true);

        SelectedFloor =
            selectedFloorButton.data;

        BuildBossButtons(
            SelectedFloor
        );
    }


    // =========================================================
    // Boss list
    // =========================================================

    private void BuildBossButtons(
        string floorName)
    {
        ClearBossButtons();

        string[] bosses;

        if (!bossesByFloor.TryGetValue(
                floorName,
                out bosses))
        {
            if (layout.loreInfo != null)
            {
                layout.loreInfo.SetActive(false);
            }

            return;
        }

        bosses = bosses
            .Where(
                bossName =>
                    bossFirstChapter[bossName] <=
                    SephiriaMod.GoalChapter
            )
            .ToArray();

        foreach (string bossName in bosses)
        {
            CreateBossButton(
                bossName
            );
        }

        // Show the first boss's information immediately,
        // just like Lore shows content for the selected
        // category.
        if (bosses.Length > 0)
        {
            ShowBossInfo(
                bosses[0]
            );
        }
        else if (layout.loreInfo != null)
        {
            layout.loreInfo.SetActive(false);
        }
    }


    private void CreateBossButton(
        string bossName)
    {
        GameObject bossObject =
            UnityEngine.Object.Instantiate(
                layout.loreIconOriginal.gameObject,
                layout.loreIconContainer
            );

        bossObject.name =
            "APBoss_" + bossName;

        UI_JournalPanel_LoreIcon loreIcon =
            bossObject.GetComponent<
                UI_JournalPanel_LoreIcon>();

        TextMeshProUGUI label = null;

        if (loreIcon != null)
        {
            label = loreIcon.text;
        }

        if (label == null)
        {
            label =
                bossObject
                    .GetComponentInChildren<
                        TextMeshProUGUI>(
                            true
                        );
        }

        string apItemName;
        TreeShopItemEntity treeShopItem;
        bool unlocked;

        bool foundReward =
            SephiriaMod
                .TryGetArchipelagoBossRewardInfo(
                    bossName,
                    out apItemName,
                    out treeShopItem,
                    out unlocked
                );

        if (label != null)
        {
            label.text =
                bossName +
                "\n" +
                (
                    foundReward && unlocked
                        ? "<color=#00FF00>UNLOCKED</color>"
                        : "LOCKED"
                );
        }

        // We only want the Lore prefab's visuals.
        // Its normal Lore behavior must not run.
        if (loreIcon != null)
        {
            UnityEngine.Object.Destroy(
                loreIcon
            );
        }

        Transform redDot =
            bossObject.transform.Find(
                "RedDot"
            );

        if (redDot != null)
        {
            redDot.gameObject.SetActive(false);
        }

        Button bossButton =
            bossObject.GetComponent<Button>();

        if (bossButton == null)
        {
            bossButton =
                bossObject.AddComponent<Button>();
        }

        bossButton.onClick =
            new Button.ButtonClickedEvent();

        string capturedBossName =
            bossName;

        bossButton.onClick.AddListener(
            delegate
            {
                ShowBossInfo(
                    capturedBossName
                );
            }
        );

        // Also update the right panel when a controller
        // moves onto this boss. The player should not
        // have to press the button just to read it.
        ArchipelagoJournalBossSelectionHandler
            selectionHandler =
                bossObject.AddComponent<
                    ArchipelagoJournalBossSelectionHandler>();

        selectionHandler.Initialize(
            delegate
            {
                ShowBossInfo(
                    capturedBossName
                );
            }
        );

        bossObject.SetActive(true);

        bossButtons.Add(
            bossObject
        );
    }


    private void ClearBossButtons()
    {
        foreach (GameObject bossButton
            in bossButtons)
        {
            if (bossButton == null)
                continue;

            bossButton.SetActive(false);

            UnityEngine.Object.Destroy(
                bossButton
            );
        }

        bossButtons.Clear();
    }


    // =========================================================
    // Right-side permanent reward panel
    // =========================================================

    private void ShowBossInfo(
        string bossName)
    {
        if (layout == null ||
            layout.loreInfo == null)
        {
            return;
        }

        selectedBossName = bossName;

        string apItemName;
        TreeShopItemEntity treeShopItem;
        bool unlocked;

        bool foundReward =
            SephiriaMod
                .TryGetArchipelagoBossRewardInfo(
                    bossName,
                    out apItemName,
                    out treeShopItem,
                    out unlocked
                );

        if (layout.loreInfo_Title != null)
        {
            layout.loreInfo_Title.text =
                bossName;
        }

        if (layout.loreImageSlot != null)
        {
            layout.loreImageSlot.SetActive(
                false
            );
        }

        if (layout.loreInfo_Description != null)
        {
            string description;

            if (!foundReward)
            {
                description =
                    "Permanent reward data " +
                    "could not be found.";
            }
            else
            {
                description =
                    unlocked
                        ? "<color=#00FF00>UNLOCKED</color>"
                        : "LOCKED\nReceive \"" +
                          apItemName +
                          "\" to unlock.";

                description +=
                    "\n\nPermanent Rewards";

                if (treeShopItem != null &&
                    treeShopItem.items != null)
                {
                    foreach (ItemEntity itemEntity
                        in treeShopItem.items)
                    {
                        if (itemEntity == null)
                            continue;

                        description +=
                            "\n• " +
                            itemEntity.Name;
                    }
                }
            }

            layout.loreInfo_Description.text =
                description;
        }

        layout.loreInfo.SetActive(true);
    }

    private void RefreshRewardStates()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        const string bossPrefix = "APBoss_";

        foreach (GameObject bossObject in bossButtons)
        {
            if (bossObject == null)
                continue;

            if (!bossObject.name.StartsWith(
                    bossPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string bossName =
                bossObject.name.Substring(
                    bossPrefix.Length
                );

            string apItemName;
            TreeShopItemEntity treeShopItem;
            bool unlocked;

            bool foundReward =
                SephiriaMod
                    .TryGetArchipelagoBossRewardInfo(
                        bossName,
                        out apItemName,
                        out treeShopItem,
                        out unlocked
                    );

            TextMeshProUGUI label =
                bossObject
                    .GetComponentInChildren<
                        TextMeshProUGUI>(
                            true
                        );

            if (label != null)
            {
                label.text =
                    bossName +
                    "\n" +
                    (
                        foundReward && unlocked
                            ? "<color=#00FF00>UNLOCKED</color>"
                            : "LOCKED"
                    );
            }
        }

        // Refresh the currently displayed boss too.
        if (currentSubTab ==
            ArchipelagoJournalSubTab.BossRewards && !string.IsNullOrEmpty(
                selectedBossName))
        {
            ShowBossInfo(
                selectedBossName
            );
        }
        else if (currentSubTab ==
                 ArchipelagoJournalSubTab.Anvil)
        {
            RefreshAnvilContent();
        }
    }

    private void OnDestroy()
    {
        SephiriaMod.ArchipelagoPermanentRewardsChanged -=
            RefreshRewardStates;
    }
}

public class ArchipelagoJournalBossSelectionHandler :
    MonoBehaviour,
    UnityEngine.EventSystems.ISelectHandler
{
    private Action onSelected;

    public void Initialize(
        Action callback)
    {
        onSelected = callback;
    }

    public void OnSelect(
        UnityEngine.EventSystems.BaseEventData eventData)
    {
        onSelected?.Invoke();
    }
}

public class ArchipelagoTreeShopModeController : MonoBehaviour
{
    public enum TreeMode
    {
        ArchipelagoTree,
        ReceivedUpgrades
    }

    public static TreeMode CurrentMode { get; private set; }
        = TreeMode.ArchipelagoTree;

    public static bool IsTreeShopOpen { get; private set; }

    private UI_TreeShopPanel panel;

    private TextMeshProUGUI modeText;

    public void Initialize(UI_TreeShopPanel treeShopPanel)
    {
        panel = treeShopPanel;
        IsTreeShopOpen = true;

        Transform modeTextTransform =
            panel.transform.Find(
                "Text (TMP) (3)"
            );

        if (modeTextTransform != null)
        {
            modeText =
                modeTextTransform
                    .GetComponent<TextMeshProUGUI>();
        }

        if (modeText == null)
        {
            MelonLogger.Warning(
                "[AP Tree] Could not find " +
                "Destiny Inscription mode text."
            );

            return;
        }

        SephiriaMod.ArchipelagoPermanentRewardsChanged -= HandleReceivedUpgradesChanged;

        SephiriaMod.ArchipelagoPermanentRewardsChanged += HandleReceivedUpgradesChanged;

        RefreshModeHeader();
        StartCoroutine(RefreshAfterOpen());
    }

    private IEnumerator RefreshAfterOpen()
    {
        // Wait until vanilla has completely finished
        // opening and rebuilding the Destiny Inscription.
        yield return null;

        if (!SephiriaMod.IsArchipelagoSave)
            yield break;

        if (!IsTreeShopOpen)
            yield break;

        if (panel == null ||
            !panel.gameObject.activeInHierarchy)
        {
            yield break;
        }

        RefreshModeHeader();
        RefreshTreeDisplay();
        RefreshCurrentSelection();
    }

    private void RefreshModeHeader()
    {
        if (modeText == null)
            return;

        modeText.alignment =
            TextAlignmentOptions.Center;

        switch (CurrentMode)
        {
            case TreeMode.ArchipelagoTree:
                modeText.text =
                    "<alpha=#00>Q / LB  <  " +
                    "<alpha=#FF>ARCHIPELAGO TREE" +
                    "  >  E / RB";
                break;

            case TreeMode.ReceivedUpgrades:
                modeText.text =
                    "Q / LB  <  " +
                    "RECEIVED UPGRADES" +
                    "<alpha=#00>  >  E / RB" +
                    "<alpha=#FF>";
                break;
        }
    }

    private void RefreshTreeDisplay()
    {
        if (panel == null)
        {
            MelonLogger.Warning(
                "[AP Tree Refresh] panel is null."
            );

            return;
        }

        if (panel.treeShopContents == null)
        {
            MelonLogger.Warning(
                "[AP Tree Refresh] treeShopContents is null."
            );

            return;
        }

        if (SaveManager.Current == null)
        {
            MelonLogger.Warning(
                "[AP Tree Refresh] SaveManager.Current is null."
            );

            return;
        }

        // ---------------------------------------------
        // PREVIEW GUIDE
        // ---------------------------------------------
        // AP Tree locations should not reveal the
        // vanilla items contained inside UnlockItem nodes.
        // Received Upgrades keeps vanilla preview behavior.
        UI_TreeShopItem[] previewItems =
            panel.treeShopContents
                .GetComponentsInChildren<UI_TreeShopItem>(true);

        foreach (UI_TreeShopItem item in previewItems)
        {
            if (item == null ||
                item.connected == null ||
                item.unlockItemPreviewerGuide == null)
            {
                continue;
            }

            bool showPreview =
                CurrentMode ==
                    TreeMode.ReceivedUpgrades &&
                item.connected.behaviour ==
                    TreeShopItemEntity.EBehaviour.UnlockItem;

            item.unlockItemPreviewerGuide.SetActive(
                showPreview
            );
        }

        // ---------------------------------------------
        // RECEIVED UPGRADES
        // ---------------------------------------------
        if (CurrentMode ==
            TreeMode.ReceivedUpgrades)
        {
            UI_TreeShopItem[] items =
                panel.treeShopContents
                    .GetComponentsInChildren<
                        UI_TreeShopItem
                    >(true);

            float contentHeight = 0f;

            foreach (UI_TreeShopItem item in items)
            {
                if (item == null ||
                    item.connected == null)
                {
                    continue;
                }

                // Root's Retreat belongs to the
                // separate Hard Mode system.
                if (item.connected.id == 3000)
                    continue;

                // We are in Received Upgrades mode,
                // so the AP save-key redirect is OFF.
                // This reads the REAL vanilla upgrade
                // count granted by Archipelago.
                int receivedCount =
                    SaveManager.Current.GetInt(
                        $"TreeShopItem_{item.connected.id}_PurchasedCount",
                        0
                    );

                if (receivedCount > 0)
                {
                    // Received / partially received.
                    item.SetVisible(1);
                }
                else
                {
                    // Not received.
                    // Keep it visible, but grayscale it.
                    item.SetVisible(2);
                }

                contentHeight =
                    Mathf.Max(
                        contentHeight,
                        item.rectTransform
                            .anchoredPosition.y + 90f
                    );
            }

            // The vanilla tree normally sizes this based
            // only on progression. Since Received Upgrades
            // reveals the whole tree, make sure the entire
            // tree can be scrolled through.
            panel.treeShopContents.sizeDelta =
                new Vector2(
                    panel.treeShopContents.sizeDelta.x,
                    contentHeight
                );

            return;
        }

        // ---------------------------------------------
        // ARCHIPELAGO TREE
        // ---------------------------------------------
        // Restore Sephiria's normal visibility logic.
        // Because CurrentMode is ArchipelagoTree here,
        // its TreeShopItem save reads are automatically
        // redirected to our AP-tree purchase state.
        AccessTools.Method(
            typeof(UI_TreeShopPanel),
            "RefreshItemsVisibleFromUnlockValues"
        )?.Invoke(panel, null);
    }

    private void HandleReceivedUpgradesChanged()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!IsTreeShopOpen)
            return;

        if (CurrentMode !=
            TreeMode.ReceivedUpgrades)
        {
            return;
        }

        RefreshTreeDisplay();
        RefreshCurrentSelection();
    }

    private void OnDestroy()
    {
        SephiriaMod.ArchipelagoPermanentRewardsChanged -=
            HandleReceivedUpgradesChanged;
    }

    private void RefreshCurrentSelection()
    {
        if (panel == null)
            return;

        UI_TreeShopItem selectedItem =
            AccessTools.Field(
                typeof(UI_TreeShopPanel),
                "currentSelected"
            )
            .GetValue(panel)
            as UI_TreeShopItem;

        if (selectedItem == null)
            return;

        AccessTools.Method(
            typeof(UI_TreeShopPanel),
            "HandleItemSelected"
        )
        .Invoke(
            panel,
            new object[]
            {
            selectedItem
            }
        );
    }

    public static void PrepareForOpen()
    {
        CurrentMode =
            TreeMode.ArchipelagoTree;

        IsTreeShopOpen = true;
    }


    public static void MarkClosed()
    {
        IsTreeShopOpen = false;
    }


    private void Update()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (panel == null ||
            !panel.gameObject.activeInHierarchy)
        {
            return;
        }

        if (!UIInputModule.current)
            return;

        bool previousTab = false;
        bool nextTab = false;

        if (ControlsChangeHandler.Current
            .IsUsingKeyboardAndMouse)
        {
            previousTab =
                UIInputModule.current
                    .prevTabAction
                    .action
                    .WasPressedThisFrame();

            nextTab =
                UIInputModule.current
                    .nextTabAction
                    .action
                    .WasPressedThisFrame();
        }
        else
        {
            Gamepad gamepad =
                Gamepad.current;

            previousTab =
                gamepad?.leftShoulder
                    .wasPressedThisFrame
                ?? false;

            nextTab =
                gamepad?.rightShoulder
                    .wasPressedThisFrame
                ?? false;
        }

        if (previousTab)
        {
            ShowArchipelagoTree();
        }

        if (nextTab)
        {
            ShowReceivedUpgrades();
        }
    }


    private void ShowArchipelagoTree()
    {
        if (CurrentMode ==
            TreeMode.ArchipelagoTree)
        {
            return;
        }

        CurrentMode =
            TreeMode.ArchipelagoTree;

        RefreshModeHeader();
        RefreshTreeDisplay();
        RefreshCurrentSelection();

        APDebug.Log(
            "[AP Tree] Mode: Archipelago Tree"
        );
    }


    private void ShowReceivedUpgrades()
    {
        if (CurrentMode ==
            TreeMode.ReceivedUpgrades)
        {
            return;
        }

        CurrentMode =
            TreeMode.ReceivedUpgrades;

        RefreshModeHeader();
        RefreshTreeDisplay();
        RefreshCurrentSelection();

        APDebug.Log(
            "[AP Tree] Mode: Received Upgrades"
        );
    }
}

public static class ArchipelagoTreeShopSaveRedirect
{
    public static string RedirectKey(
        string key)
    {
        if (SephiriaMod.IsSynchronizingArchipelagoDestinyUpgrades)
            return key;

        if (!SephiriaMod.IsArchipelagoSave)
            return key;

        if (!ArchipelagoTreeShopModeController
            .IsTreeShopOpen)
        {
            return key;
        }

        if (ArchipelagoTreeShopModeController
            .CurrentMode !=
            ArchipelagoTreeShopModeController
                .TreeMode.ArchipelagoTree)
        {
            return key;
        }

        if (string.IsNullOrEmpty(key))
            return key;

        if (!key.StartsWith(
            "TreeShopItem_"))
        {
            return key;
        }

        // Root's Retreat belongs to our Hard Mode
        // system, not the randomized AP tree.
        if (key.StartsWith(
            "TreeShopItem_3000_"))
        {
            return key;
        }

        return "AP" + key;
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

    public static void OpenArchipelagoSetup(
        UI_TitleLobby titleLobby,
        SaveData existingSave = null,
        UI_SaveProfileButton existingButton = null)
    {
        bool editingExistingSave = existingSave != null;

        string defaultHost =
            editingExistingSave
                ? existingSave.GetString(
                    "ArchipelagoHost",
                    "localhost"
                )
                : "localhost";

        int defaultPort =
            editingExistingSave
                ? existingSave.GetInt(
                    "ArchipelagoPort",
                    38281
                )
                : 38281;

        string defaultSlot =
            editingExistingSave
                ? existingSave.GetString(
                    "ArchipelagoSlot",
                    ""
                )
                : "";

        string defaultPassword =
            editingExistingSave
                ? existingSave.GetString(
                    "ArchipelagoPassword",
                    ""
                )
                : "";

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
                                         titleLobby,
                                         existingSave,
                                         existingButton
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
                                         titleLobby,
                                         existingSave,
                                         existingButton
                                     )
                                 );
                             }
                         );

                     return;
                 }

                 if (editingExistingSave)
                 {
                     existingSave.SetString("ArchipelagoHost", hostValue);
                     existingSave.SetInt("ArchipelagoPort", port);
                     existingSave.SetString("ArchipelagoSlot", slotName);
                     existingSave.SetString("ArchipelagoPassword", password);
                     existingSave.Save();

                     MelonLogger.Msg(
                        "[AP Save] Connection settings updated."
                    );

                     // AP is already connected successfully.
                     // Allow Sephiria's normal CheckAndStart to continue
                     // without trying to reconnect to AP a second time.
                     continueAfterChoice = true;

                     // Use Sephiria's normal save-button flow to load
                     // this exact existing save.
                     existingButton.SelectProfile();

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
                     "[AP Save] Archipelago save configured."
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
             defaultHost,
             "",
             allowEmpty: false,
             100
        );

        // Give the AP setup popup clearer button labels.
        TMPro.TextMeshProUGUI yesButtonText =
            popup.yesButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

        TMPro.TextMeshProUGUI noButtonText =
            popup.noButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

        string originalYesButtonText =
            yesButtonText != null ? yesButtonText.text : null;

        string originalNoButtonText =
            noButtonText != null ? noButtonText.text : null;

        if (yesButtonText != null)
            yesButtonText.text = "Connect";

        if (noButtonText != null)
            noButtonText.text = "Cancel";

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

        portInput.text = defaultPort.ToString();
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

        slotInput.text = defaultSlot;
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

        passwordInput.text = defaultPassword;
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

            // Restore Sephiria's original button labels.
            if (yesButtonText != null &&
                originalYesButtonText != null)
            {
                yesButtonText.text = originalYesButtonText;
            }

            if (noButtonText != null &&
                originalNoButtonText != null)
            {
                noButtonText.text = originalNoButtonText;
            }

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
        UI_TitleLobby titleLobby,
        SaveData existingSave = null,
        UI_SaveProfileButton existingButton = null)
    {
        yield return null;

        OpenArchipelagoSetup(
            titleLobby,
            existingSave,
            existingButton
        );
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
    typeof(UI_SaveProfileButton),
    nameof(UI_SaveProfileButton.InitializeProfile)
)]
public static class ArchipelagoSaveProfileButtonPatch
{
    public static void RemoveArchipelagoIcon(
        UI_SaveProfileButton button)
    {
        if (button == null)
            return;

        Transform existingIcon =
            button.transform.Find("ArchipelagoSaveIcon");

        if (existingIcon != null)
        {
            UnityEngine.Object.Destroy(
                existingIcon.gameObject
            );

            APDebug.Log(
                $"[AP Save Icon] Removed icon from " +
                $"{button.slotName}"
            );
        }
    }

    public static void Postfix(
        UI_SaveProfileButton __instance,
        SaveData data)
    {
        if (data == null)
            return;

        bool isArchipelagoSave =
            data.GetBool(
                "ArchipelagoSave",
                false
            );

        if (!isArchipelagoSave)
        {
            RemoveArchipelagoIcon(__instance);
            return;
        }

        APDebug.Log(
            $"[AP Save Icon] Found AP save: " +
            $"{__instance.slotName}"
        );

        // Don't create the icon twice.
        Transform existingIcon =
            __instance.transform.Find("ArchipelagoSaveIcon");

        if (existingIcon != null)
            return;

        // Use the AP logo we already load for Sephirite rewards.
        UnityEngine.Sprite originalSprite =
            __instance.selectedImage != null
                ? __instance.selectedImage.sprite
                : null;

        UnityEngine.Sprite apSprite =
            SephiriaMod.GetArchipelagoSaveProfileSprite(
                originalSprite
            );

        if (apSprite == null)
        {
            MelonLogger.Warning(
                "[AP Save Icon] Could not load AP icon."
            );

            return;
        }

        UnityEngine.Sprite apOutlineSprite =
        SephiriaMod.GetArchipelagoSaveProfileOutlineSprite(
            originalSprite
        );

        if (apOutlineSprite == null)
        {
            MelonLogger.Warning(
                "[AP Save Icon] Could not load AP outline icon."
            );
        }

        GameObject iconObject =
            new GameObject(
                "ArchipelagoSaveIcon",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );

        iconObject.transform.SetParent(
            __instance.transform,
            false
        );

        RectTransform iconRect =
            iconObject.GetComponent<RectTransform>();

        // Anchor to upper-right corner of this save box.
        iconRect.anchorMin = new Vector2(1f, 1f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.pivot = new Vector2(1f, 1f);

        iconRect.sizeDelta =
            new Vector2(26f, 26f);

        iconRect.anchoredPosition =
            new Vector2(-8f, -7f);

        UnityEngine.UI.Image iconImage =
            iconObject.GetComponent<UnityEngine.UI.Image>();

        iconImage.sprite = apSprite;
        iconImage.preserveAspect = true;

        // Allow this icon to receive mouse clicks.
        iconImage.raycastTarget = true;

        // Swap to the white-outline version while hovering.
        UnityEngine.EventSystems.EventTrigger eventTrigger =
            iconObject.AddComponent<
                UnityEngine.EventSystems.EventTrigger
            >();

        UnityEngine.EventSystems.EventTrigger.Entry enterEntry =
            new UnityEngine.EventSystems.EventTrigger.Entry();

        enterEntry.eventID =
            UnityEngine.EventSystems.EventTriggerType.PointerEnter;

        enterEntry.callback.AddListener(
            delegate (UnityEngine.EventSystems.BaseEventData _)
            {
                if (apOutlineSprite != null)
                {
                    iconImage.sprite =
                        apOutlineSprite;
                }
            }
        );

        eventTrigger.triggers.Add(enterEntry);

        // Controller/keyboard selection.
        UnityEngine.EventSystems.EventTrigger.Entry selectEntry =
            new UnityEngine.EventSystems.EventTrigger.Entry();

        selectEntry.eventID =
            UnityEngine.EventSystems.EventTriggerType.Select;

        selectEntry.callback.AddListener(
            delegate (UnityEngine.EventSystems.BaseEventData _)
            {
                if (apOutlineSprite != null)
                {
                    iconImage.sprite =
                        apOutlineSprite;
                }
            }
        );

        eventTrigger.triggers.Add(selectEntry);

        UnityEngine.EventSystems.EventTrigger.Entry deselectEntry =
            new UnityEngine.EventSystems.EventTrigger.Entry();

        deselectEntry.eventID =
            UnityEngine.EventSystems.EventTriggerType.Deselect;

        deselectEntry.callback.AddListener(
            delegate (UnityEngine.EventSystems.BaseEventData _)
            {
                iconImage.sprite =
                    apSprite;
            }
        );

        eventTrigger.triggers.Add(deselectEntry);

        UnityEngine.EventSystems.EventTrigger.Entry exitEntry =
            new UnityEngine.EventSystems.EventTrigger.Entry();

        exitEntry.eventID =
            UnityEngine.EventSystems.EventTriggerType.PointerExit;

        exitEntry.callback.AddListener(
            delegate (UnityEngine.EventSystems.BaseEventData _)
            {
                iconImage.sprite =
                    apSprite;
            }
        );

        eventTrigger.triggers.Add(exitEntry);

        UnityEngine.UI.Button iconButton =
            iconObject.AddComponent<UnityEngine.UI.Button>();

        iconButton.targetGraphic = iconImage;

        // Don't add any automatic Unity button tinting yet.
        iconButton.transition =
            UnityEngine.UI.Selectable.Transition.None;

        iconButton.onClick.AddListener(
            delegate
            {
                iconImage.sprite = apSprite;

                MelonLogger.Msg(
                    $"[AP Save Icon] Opening AP setup for " +
                    $"{__instance.slotName}"
                );

                ArchipelagoSaveChoicePatch.OpenArchipelagoSetup(
                    __instance.parentPanel.titleLobby,
                    data,
                    __instance
                );
            }
        );

        // Keep it above the other contents of the save box.
        iconObject.transform.SetAsLastSibling();

        APDebug.Log(
            $"[AP Save Icon] Created icon for " +
            $"{__instance.slotName}"
        );
    }
}

[HarmonyPatch(
    typeof(UI_SaveProfileButton),
    "PrintEmpty"
)]
public static class ArchipelagoSaveProfileEmptyPatch
{
    public static void Postfix(
        UI_SaveProfileButton __instance)
    {
        ArchipelagoSaveProfileButtonPatch
            .RemoveArchipelagoIcon(__instance);
    }
}

[HarmonyPatch(
    typeof(UI_SaveProfileButton),
    "PrintLoadError"
)]
public static class ArchipelagoSaveProfileLoadErrorPatch
{
    public static void Postfix(
        UI_SaveProfileButton __instance)
    {
        ArchipelagoSaveProfileButtonPatch
            .RemoveArchipelagoIcon(__instance);
    }
}

[HarmonyPatch(
    typeof(UI_TitleLobby),
    nameof(UI_TitleLobby.Join)
)]
public static class ArchipelagoJoinContextPatch
{
    public static bool IsJoinInProgress { get; private set; }

    public static void Prefix()
    {
        IsJoinInProgress = true;
    }

    public static void Postfix()
    {
        IsJoinInProgress = false;
    }
}

[HarmonyPatch(
    typeof(NetworkManager),
    nameof(NetworkManager.StartClient),
    new Type[] { }
)]
public static class ArchipelagoJoinConnectionGatePatch
{
    private static bool reconnecting = false;

    public static bool Prefix(
        NetworkManager __instance)
    {
        // Only interfere with StartClient when it came specifically
        // from UI_TitleLobby.Join().
        if (!ArchipelagoJoinContextPatch.IsJoinInProgress)
            return true;

        // Normal saves behave exactly as vanilla.
        if (SaveManager.Current == null ||
            !SaveManager.Current.GetBool(
                "ArchipelagoSave",
                false))
        {
            return true;
        }

        // Prevent a second Join click while AP is connecting.
        if (reconnecting)
            return false;

        reconnecting = true;

        _ = ReconnectAndStartClient(__instance);

        // Do NOT let Sephiria connect yet.
        return false;
    }

    private static async Task ReconnectAndStartClient(
        NetworkManager networkManager)
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
            $"Reconnecting AP save before Join: " +
            $"{host}:{port}, Slot={slotName}"
        );

        bool connected =
            await SephiriaMod.ConnectToArchipelago(
                host,
                port,
                slotName,
                password
            );

        reconnecting = false;

        if (!connected)
        {
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

        MelonLogger.Msg(
            "Archipelago reconnected successfully. " +
            "Continuing Join."
        );

        // Join() has already returned by this point, so our
        // Join-context gate is no longer active.
        networkManager.StartClient();
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

        SephiriaMod.ResetArchipelagoRunVictory();

        // Hard Mode is already enabled and controlled by Archipelago.
        // Mark Root's Retreat as purchased so the player is never
        // prompted to purchase the free vanilla Hard Mode unlock.
        SaveManager.Current.SetInt(
            "TreeShopItem_3000_PurchasedCount",
            1
        );

        SaveManager.Current.SetInt(
            "TreeShopItem_3000_PaidPrice",
            0
        );

        SaveManager.Current.SetBool(
            "HardModeUnlockTutorial",
            true
        );

        SaveManager.Current.SetBool(
            "MapTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "SephiriteOpenVeryFirst",
            false
        );

        SaveManager.CurrentRun.SetInt(
            "Library_FirstEventIdx",
            4
        );

        SaveManager.Current.SetBool(
            "AltarOfEnchantTutorialEnd",
            true
        );

        SaveManager.Current.SetBool(
            "MysticPotTutorialEnd",
            true
        );

        SaveManager.Current.SetBool(
            "TabletMixTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "Chapter3Tutorial_DungeonChange",
            false
        );

        SaveManager.Current.SetBool(
            "StoneTableTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "CostumeTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "SapphireTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "MagicTutorial_New",
            false
        );

        SaveManager.Current.SetBool(
            "CostumeSkinTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "RerollDiceTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "TabletEngravingTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "ShowDiceConvertTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "ProtectionOfRationalTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "ProtectionOfRationalBrokenTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "AltarOfIdentifyTutorialEnd",
            true
        );

        SaveManager.Current.SetBool(
            "PocketDimensionShopTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "TalentTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "SubBagTutorial",
            false
        );

        SaveManager.Current.SetBool(
            "StartingFountainTutorial3",
            false
        );

        SaveManager.Current.SetBool(
            "ReplenishmentTutorial_InShop",
            false
        );

        if (SephiriaMod.GoalChapter >= 4)
        {
            SaveManager.Current.SetBool(
                "SawChapter4GrasstownTutorial",
                true
            );
        }

        SephiriaMod.ResetArchipelagoShrineRunState();
        SephiriaMod.ResetArchipelagoAnvilRunState();
        SephiriaMod.ResetArchipelagoCintamaniRunState();

        DesertEndEventPatch.Reset();

        MelonLogger.Msg(
            $"[AP Chapter] Starting run for Goal Chapter {SephiriaMod.GoalChapter}."
        );

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

        SephiriaMod.MarkArchipelagoRunVictory();

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
                player.PlayerAvatar.ForceDie();
            }
        }

        return false;
    }
}

[HarmonyPatch(
    typeof(PlayerLocalDataStorage),
    nameof(PlayerLocalDataStorage.UpdateMainQuestProgress)
)]
public static class Chapter2TitlePatch
{
    private static bool shown;

    public static void Postfix(
        PlayerLocalDataStorage __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 2 || shown)
            return;

        if (SaveManager.CurrentRun == null)
            return;

        // Only show the forced AP chapter title when
        // starting a fresh run. A resumed run already
        // has its CurrentGame assigned.
        if (SaveManager.CurrentRun.GetInt(
                "CurrentGame",
                -1) != -1)
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

    public static void Postfix(
        PlayerLocalDataStorage __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 3 || shown)
            return;

        if (SaveManager.CurrentRun == null)
            return;

        // Do not replay the chapter title when
        // loading an adventure already in progress.
        if (SaveManager.CurrentRun.GetInt(
                "CurrentGame",
                -1) != -1)
        {
            return;
        }

        shown = true;
        __instance.ChangeChapter(3);
    }
}

[HarmonyPatch(
    typeof(PlayerLocalDataStorage),
    nameof(PlayerLocalDataStorage.UpdateMainQuestProgress)
)]
public static class Chapter4TitlePatch
{
    private static bool shown;

    public static void Postfix(
        PlayerLocalDataStorage __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SephiriaMod.GoalChapter != 4 || shown)
            return;

        if (SaveManager.CurrentRun == null)
            return;

        // Do not replay the chapter title when
        // loading an adventure already in progress.
        if (SaveManager.CurrentRun.GetInt(
                "CurrentGame",
                -1) != -1)
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

        SephiriaMod.MarkArchipelagoRunVictory();

        MelonLogger.Msg(
            "[AP Chapter 5] Chapter 5 Qliphoth defeated."
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
                player.PlayerAvatar.ForceDie();
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

        SephiriaMod.MarkArchipelagoRunVictory();

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
                player.PlayerAvatar.ForceDie();
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

        SephiriaMod.MarkArchipelagoRunVictory();

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

            __instance.PlayerAvatar.ForceDie();
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

        SephiriaMod.MarkArchipelagoRunVictory();

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
                player.PlayerAvatar.ForceDie();
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

        SephiriaMod.MarkArchipelagoRunVictory();

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
                player.PlayerAvatar.ForceDie();
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

        if (SephiriaMod.GoalChapter < 1 ||
            SephiriaMod.GoalChapter > 6)
        {
            return true;
        }

        // Once the AP goal is complete, stop interfering
        // and allow Sephiria's normal ending/progression.
        if (SephiriaMod.CompletedChapterClears >=
            SephiriaMod.RequiredChapterClears)
        {
            return true;
        }

        // While the AP goal is still active, the only
        // chapter title that is valid is the selected
        // AP goal chapter.
        if (chapter != SephiriaMod.GoalChapter)
        {
            MelonLogger.Msg(
                $"[AP Chapter Title] Blocked Chapter {chapter} " +
                $"while AP goal is Chapter {SephiriaMod.GoalChapter}."
            );

            return false;
        }

        return true;
    }
}

[HarmonyPatch(
    typeof(UI_HardModePanel),
    "OnOpened"
)]
public static class ArchipelagoHardModePanelTutorialPatch
{
    public static void Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave ||
            SaveManager.Current == null)
        {
            return;
        }

        SaveManager.Current.SetBool(
            "HardModePanel_FirstOpen",
            false
        );
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
public static class ArchipelagoSephiriteGeneratePatch
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
            APDebug.Log(
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
            if (SephiriaMod.IsBlockedArchipelagoSephirite(
                    __instance))
            {
                MelonLogger.Error(
                    "[AP Sephirite] Eligible Sephirite could not " +
                    "receive a safe persistent sequence assignment. " +
                    "The reward has been blocked."
                );

                return;
            }

            MelonLogger.Error(
                "[AP Sephirite] Eligible Sephirite assignment " +
                "failed unexpectedly."
            );

            return;
        }

        if (wasAlreadyAssigned)
        {
            APDebug.Log(
                $"[AP Sephirite] Reused persistent " +
                $"assignment #{eligibleNumber}, " +
                $"Seed={__instance.CurrentSeed}."
            );
        }

        bool isArchipelagoPosition =
            eligibleNumber % 2 == 0;

        if (!isArchipelagoPosition)
        {
            APDebug.Log(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} -> VANILLA " +
                $"({__instance.type}, " +
                $"{__instance.Rewards.Count} choices, " +
                $"Seed={__instance.CurrentSeed})"
            );

            return;
        }

        int normalChoiceCount = __instance.Rewards.Count;

        int stageProgress;
        string poolName;
        List<string> locationNames;

        bool foundPool =
            SephiriaMod
                .TryPrepareArchipelagoSephiriteChoices(
                    __instance,
                    actor,
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

        if (SephiriaMod.IsBlockedArchipelagoSephirite(
            __instance))
        {
            MelonLogger.Error(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} is blocked because " +
                "its AP reward state could not be restored safely."
            );

            return;
        }

        if (SephiriaMod.WasArchipelagoSephiriteClaimed(__instance))
        {
            APDebug.Log(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} was already resolved. " +
                "It will be consumed without another reward."
            );

            return;
        }

        if (locationNames.Count == 0)
        {
            APDebug.Log(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} -> AP position, " +
                $"StageProgress={stageProgress}, " +
                $"Pool={poolName}, but that pool " +
                "has no unchecked locations. " +
                "Leaving vanilla."
            );

            return;
        }

        APDebug.Log(
            $"[AP Sephirite] Eligible " +
            $"#{eligibleNumber} -> AP, " +
            $"Seed={__instance.CurrentSeed}"
        );

        APDebug.Log(
            $"[AP Sephirite] " +
            $"StageProgress={stageProgress}, " +
            $"Pool={poolName}, " +
            $"NormalChoices={normalChoiceCount}, " +
            $"APChoices={locationNames.Count}"
        );

        foreach (string locationName in locationNames)
        {
            APDebug.Log(
                "[AP Sephirite Choice] " +
                locationName
            );
        }
    }
}

[HarmonyPatch(
    typeof(PlayerAvatar),
    nameof(PlayerAvatar.AcquireSephiriteReward)
)]
public static class ArchipelagoSephiriteOpenPatch
{
    public static bool Prefix(
        PlayerAvatar __instance,
        Sephirite sephiriteInstance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (SephiriaMod.IsBlockedArchipelagoSephirite(
                sephiriteInstance))
        {
            MelonLogger.Error(
                "[AP Sephirite] Blocked Sephirite tried to open. " +
                "Reward UI was prevented from opening."
            );

            if (UIManager.Instance != null)
            {
                UIManager.Instance
                    .GetElement<UI_SystemMessage>()
                    .Open(
                        "This Archipelago reward could not be restored safely.",
                        4f
                    );
            }

            return false;
        }

        if (!SephiriaMod.IsActiveArchipelagoSephirite(
                sephiriteInstance))
        {
            return true;
        }

        bool pendingResolvedAsClaimed;

        if (!SephiriaMod
                .TryResolveArchipelagoSephiritePendingClaim(
                    sephiriteInstance,
                    out pendingResolvedAsClaimed))
        {
            MelonLogger.Warning(
                "[AP Sephirite] Could not safely resolve " +
                "pending reward state. Reward UI was blocked."
            );

            return false;
        }

        if (pendingResolvedAsClaimed ||
            SephiriaMod.WasArchipelagoSephiriteClaimed(
                sephiriteInstance))
        {
            MelonLogger.Msg(
                "[AP Sephirite] Previously claimed AP " +
                "Sephirite was reconstructed. " +
                "Consuming without another reward."
            );

            __instance.SelectSephiriteReward(
                sephiriteInstance,
                -1,
                new ItemPosition(-1, -1)
            );

            return false;
        }

        return true;
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardPanel),
    "HandleSephiriteUIReopen"
)]
public static class ArchipelagoQueuedSephiriteOpenPatch
{
    public static bool Prefix(
        UI_SephiriteRewardPanel __instance,
        Sephirite sephirite,
        PlayerAvatar ___openedAvatar,
        out bool __state)
    {
        __state = false;

        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (sephirite == null)
            return true;

        if (SephiriaMod.IsBlockedArchipelagoSephirite(
                sephirite))
        {
            MelonLogger.Error(
                "[AP Sephirite] Blocked queued Sephirite " +
                "tried to reopen. Reward UI was prevented."
            );

            if (UIManager.Instance != null)
            {
                UIManager.Instance
                    .GetElement<UI_SystemMessage>()
                    .Open(
                        "This Archipelago reward could not be restored safely.",
                        4f
                    );
            }

            return false;
        }

        bool nextIsArchipelago =
            SephiriaMod.IsActiveArchipelagoSephirite(
                sephirite
            );

        __state = !nextIsArchipelago;

        if (!nextIsArchipelago)
            return true;

        bool pendingResolvedAsClaimed;

        if (!SephiriaMod
                .TryResolveArchipelagoSephiritePendingClaim(
                    sephirite,
                    out pendingResolvedAsClaimed))
        {
            MelonLogger.Warning(
                "[AP Sephirite] Could not safely resolve " +
                "pending queued reward state. " +
                "Reward UI was blocked."
            );

            return false;
        }

        if (pendingResolvedAsClaimed ||
            SephiriaMod.WasArchipelagoSephiriteClaimed(
                sephirite))
        {
            MelonLogger.Msg(
                "[AP Sephirite] Previously claimed queued " +
                "AP Sephirite was reconstructed. " +
                "Consuming without another reward."
            );

            if (___openedAvatar != null)
            {
                ___openedAvatar.SelectSephiriteReward(
                    sephirite,
                    -1,
                    new ItemPosition(-1, -1)
                );
            }

            return false;
        }

        return true;
    }

    public static void Postfix(
        UI_SephiriteRewardPanel __instance,
        PlayerAvatar ___openedAvatar,
        bool __state)
    {
        if (!__state)
            return;

        SephiriaMod.RestoreVanillaSephiritePanelControls(
            __instance,
            ___openedAvatar
        );
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardPanel),
    nameof(
        UI_SephiriteRewardPanel
            .AcquireSephiriteRewardByUIElement
    )
)]
public static class ArchipelagoSephiriteClaimPatch
{
    public static bool Prefix(
        UI_SephiriteRewardPanel __instance,
        UI_SephiriteRewardElement rewardElement,
        PlayerAvatar ___openedAvatar)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (rewardElement == null)
            return true;

        if (__instance == null ||
            __instance.sephirite == null ||
            ___openedAvatar == null)
        {
            MelonLogger.Warning(
                "[AP Sephirite] Invalid reward UI state. " +
                "AP check was not sent."
            );

            return false;
        }

        if (SephiriaMod.IsBlockedArchipelagoSephirite(
            __instance.sephirite))
        {
            MelonLogger.Error(
                "[AP Sephirite] Blocked Sephirite claim " +
                "was prevented."
            );

            if (UIManager.Instance != null)
            {
                UIManager.Instance
                    .GetElement<UI_SystemMessage>()
                    .Open(
                        "This Archipelago reward could not be restored safely.",
                        4f
                    );
            }

            return false;
        }

        long locationId;
        string locationName;

        if (!SephiriaMod
            .TryGetArchipelagoSephiriteChoice(
                rewardElement.reward.instanceID,
                out locationId,
                out locationName))
        {
            if (SephiriaMod.IsActiveArchipelagoSephirite(
                    __instance.sephirite))
            {
                MelonLogger.Error(
                    "[AP Sephirite] Active AP reward is missing " +
                    "its location mapping. Vanilla claim was blocked."
                );

                if (UIManager.Instance != null)
                {
                    UIManager.Instance
                        .GetElement<UI_SystemMessage>()
                        .Open(
                            "This Archipelago reward could not be restored safely.",
                            4f
                        );
                }

                return false;
            }

            return true;
        }

        // Persist what we are about to send BEFORE
        // contacting the Archipelago server.
        if (!SephiriaMod.SetArchipelagoSephiritePendingClaim(
                __instance.sephirite,
                locationId))
        {
            MelonLogger.Error(
                "[AP Sephirite] Could not persist pending " +
                "claim. AP check was not sent."
            );

            return false;
        }

        if (!SephiriaMod
                .TryCompleteArchipelagoSephiriteChoice(
                    rewardElement.reward.instanceID,
                    out locationName))
        {
            if (!SephiriaMod.ClearArchipelagoSephiritePendingClaim(
                __instance.sephirite))
            {
                MelonLogger.Warning(
                    "[AP Sephirite] AP check failed and Pending " +
                    "could not be cleared. Pending was retained " +
                    "for recovery."
                );
            }

            UIManager.Instance
                .GetElement<UI_SystemMessage>()
                .Open(
                    "Archipelago reward could not be sent. " +
                    "Check your connection.",
                    4f
                );

            return false;
        }

        bool claimedStateSaved =
            SephiriaMod.MarkArchipelagoSephiriteClaimed(
                __instance.sephirite
            );

        if (claimedStateSaved)
        {
            if (!SephiriaMod.ClearArchipelagoSephiritePendingClaim(
                    __instance.sephirite))
            {
                MelonLogger.Warning(
                    "[AP Sephirite] Claimed state was saved, " +
                    "but Pending could not be cleared. " +
                    "The reward will still be consumed because " +
                    "Claimed state is durable."
                );
            }
        }
        else
        {
            // IMPORTANT:
            // Leave Pending intact.
            //
            // The AP server may already have accepted the
            // location, so reload recovery must determine
            // what happened.
            MelonLogger.Error(
                "[AP Sephirite] AP check succeeded, but " +
                "claimed state could not be persisted. " +
                "Pending claim was retained."
            );
        }

        SephiriaMod.PlayArchipelagoSephiriteSelectSound(
            __instance
        );

        // -1 means finish the Sephirite without adding
        // its underlying vanilla shell item.
        ___openedAvatar.SelectSephiriteReward(
            __instance.sephirite,
            -1,
            new ItemPosition(-1, -1)
        );

        return false;
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardPanel),
    nameof(
        UI_SephiriteRewardPanel
            .AcquireSephiriteRewardToSubBag
    )
)]
public static class ArchipelagoSephiriteSubBagClaimPatch
{
    public static bool Prefix(
        UI_SephiriteRewardPanel __instance,
        UI_SephiriteRewardElement rewardElement,
        PlayerAvatar ___openedAvatar)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (rewardElement == null)
            return true;

        if (__instance == null ||
            __instance.sephirite == null ||
            ___openedAvatar == null)
        {
            MelonLogger.Warning(
                "[AP Sephirite] Invalid reward UI state. " +
                "AP check was not sent."
            );

            return false;
        }

        if (SephiriaMod.IsBlockedArchipelagoSephirite(
            __instance.sephirite))
        {
            MelonLogger.Error(
                "[AP Sephirite] Blocked Sephirite claim " +
                "was prevented."
            );

            if (UIManager.Instance != null)
            {
                UIManager.Instance
                    .GetElement<UI_SystemMessage>()
                    .Open(
                        "This Archipelago reward could not be restored safely.",
                        4f
                    );
            }

            return false;
        }

        long locationId;
        string locationName;

        if (!SephiriaMod
            .TryGetArchipelagoSephiriteChoice(
                rewardElement.reward.instanceID,
                out locationId,
                out locationName))
        {
            if (SephiriaMod.IsActiveArchipelagoSephirite(
                    __instance.sephirite))
            {
                MelonLogger.Error(
                    "[AP Sephirite] Active AP reward is missing " +
                    "its location mapping. Vanilla claim was blocked."
                );

                if (UIManager.Instance != null)
                {
                    UIManager.Instance
                        .GetElement<UI_SystemMessage>()
                        .Open(
                            "This Archipelago reward could not be restored safely.",
                            4f
                        );
                }

                return false;
            }

            return true;
        }

        if (!SephiriaMod.SetArchipelagoSephiritePendingClaim(
                __instance.sephirite,
                locationId))
        {
            MelonLogger.Error(
                "[AP Sephirite] Could not persist pending " +
                "claim. AP check was not sent."
            );

            return false;
        }

        if (!SephiriaMod
                .TryCompleteArchipelagoSephiriteChoice(
                    rewardElement.reward.instanceID,
                    out locationName))
        {
            if (!SephiriaMod.ClearArchipelagoSephiritePendingClaim(
                __instance.sephirite))
            {
                MelonLogger.Warning(
                    "[AP Sephirite] AP check failed and Pending " +
                    "could not be cleared. Pending was retained " +
                    "for recovery."
                );
            }

            return false;
        }

        bool claimedStateSaved =
            SephiriaMod.MarkArchipelagoSephiriteClaimed(
                __instance.sephirite
            );

        if (claimedStateSaved)
        {
            if (!SephiriaMod.ClearArchipelagoSephiritePendingClaim(
                    __instance.sephirite))
            {
                MelonLogger.Warning(
                    "[AP Sephirite] Claimed state was saved, " +
                    "but Pending could not be cleared. " +
                    "The reward will still be consumed because " +
                    "Claimed state is durable."
                );
            }
        }
        else
        {
            MelonLogger.Error(
                "[AP Sephirite] AP check succeeded, but " +
                "claimed state could not be persisted. " +
                "Pending claim was retained."
            );
        }

        SephiriaMod.PlayArchipelagoSephiriteSelectSound(
            __instance
        );

        ___openedAvatar.SelectSephiriteReward(
            __instance.sephirite,
            -1,
            new ItemPosition(-1, -1)
        );

        return false;
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardPanel),
    nameof(UI_SephiriteRewardPanel.Reroll)
)]
public static class ArchipelagoSephiriteRerollBlockPatch
{
    public static bool Prefix(
        UI_SephiriteRewardPanel __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return
            !SephiriaMod.IsActiveArchipelagoSephirite(
                __instance.sephirite
            ) &&
            !SephiriaMod.IsBlockedArchipelagoSephirite(
                __instance.sephirite
            );
    }
}


[HarmonyPatch(
    typeof(UI_SephiriteRewardPanel),
    nameof(UI_SephiriteRewardPanel.SkipSelect)
)]
public static class ArchipelagoSephiriteSkipBlockPatch
{
    public static bool Prefix(
        UI_SephiriteRewardPanel __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return
            !SephiriaMod.IsActiveArchipelagoSephirite(
                __instance.sephirite
            ) &&
            !SephiriaMod.IsBlockedArchipelagoSephirite(
                __instance.sephirite
            );
    }
}


[HarmonyPatch(
    typeof(UI_SephiriteRewardPanel),
    nameof(UI_SephiriteRewardPanel.ConvertRerollDice)
)]
public static class ArchipelagoSephiriteConvertBlockPatch
{
    public static bool Prefix(
        UI_SephiriteRewardPanel __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return
            !SephiriaMod.IsActiveArchipelagoSephirite(
                __instance.sephirite
            ) &&
            !SephiriaMod.IsBlockedArchipelagoSephirite(
                __instance.sephirite
            );
    }
}


[HarmonyPatch(
    typeof(Sephirite),
    nameof(Sephirite.Reroll)
)]
public static class ArchipelagoSephiriteUnderlyingRerollBlockPatch
{
    public static bool Prefix(
        Sephirite __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return
            !SephiriaMod.IsActiveArchipelagoSephirite(
                __instance
            ) &&
            !SephiriaMod.IsBlockedArchipelagoSephirite(
                __instance
            );
    }
}
[HarmonyPatch(
    typeof(Sephirite),
    "OnDestroy"
)]
public static class ArchipelagoSephiriteDestroyPatch
{
    public static void Postfix(
        Sephirite __instance)
    {
        SephiriaMod.ClearArchipelagoSephiriteRuntimeState(
            __instance
        );
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardElement),
    nameof(UI_SephiriteRewardElement.Initialize)
)]
public static class ArchipelagoSephiriteRewardVisualPatch
{
    public static void Postfix(
        UI_SephiriteRewardElement __instance,
        Sephirite sephirite,
        SephiriteRewardMetadata reward)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__instance == null ||
            sephirite == null)
        {
            return;
        }

        if (!SephiriaMod.IsActiveArchipelagoSephirite(
                sephirite))
        {
            return;
        }

        long locationId;
        string locationName;

        if (!SephiriaMod.TryGetArchipelagoSephiriteChoice(
                reward.instanceID,
                out locationId,
                out locationName))
        {
            return;
        }

        if (string.IsNullOrEmpty(locationName))
            return;

        string itemName;
        string itemDescription;

        if (!SephiriaMod.TryGetArchipelagoSephiriteScoutedDisplay(
                locationId,
                out itemName,
                out itemDescription))
        {
            itemName = locationName;
        }

        if (__instance.nameText != null)
        {
            __instance.nameText.text = itemName;
        }

        SephiriaMod.ApplyArchipelagoSephiriteRewardIcon(__instance);
        SephiriaMod.ApplyArchipelagoSephiriteRarity(__instance, locationId);

        if (__instance.levelImage != null)
        {
            __instance.levelImage.gameObject.SetActive(false);
        }
    }
}

[HarmonyPatch(
    typeof(UI_NewItemPicker_Controller),
    nameof(UI_NewItemPicker_Controller.PickSephiriteReward)
)]
public static class ArchipelagoSephiritePickerIconPatch
{
    public static void Postfix(
        UI_NewItemPicker_Controller __instance,
        UI_SephiriteRewardElement instance)
    {
        SephiriaMod.ApplyArchipelagoSephiritePickerIcon(
            __instance,
            instance
        );
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardElement),
    nameof(UI_SephiriteRewardElement.OnSelect)
)]
public static class ArchipelagoSephiriteTooltipVisualPatch
{
    private sealed class SavedTooltipObjectState
    {
        public GameObject GameObject;
        public bool WasActive;
    }

    private sealed class SavedTooltipImageState
    {
        public UnityEngine.UI.Image Image;
        public UnityEngine.Sprite Sprite;
        public UnityEngine.Material Material;
        public bool PreserveAspect;
        public Vector2 SizeDelta;
        public Quaternion Rotation;
    }

    private static readonly Dictionary<
        int,
        Dictionary<string, SavedTooltipObjectState>>
        savedTooltipObjectStates =
            new Dictionary<
                int,
                Dictionary<string, SavedTooltipObjectState>>();

    private static readonly Dictionary<
        int,
        SavedTooltipImageState>
        savedTooltipImageStates =
            new Dictionary<
                int,
                SavedTooltipImageState>();

    public static void Prefix(
        UI_SephiriteRewardElement __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        // Restore any UI state left by the previously
        // selected AP reward before Sephiria builds
        // the next vanilla tooltip.
        RestoreAllTooltipStates();
    }

    public static void Postfix(
        UI_SephiriteRewardElement __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__instance == null ||
            __instance.Sephirite == null)
        {
            return;
        }

        if (!SephiriaMod.IsActiveArchipelagoSephirite(
                __instance.Sephirite))
        {
            return;
        }

        if (__instance.LastTooltip == null)
            return;

        object tooltip = __instance.LastTooltip;

        long locationId;
        string locationName;

        if (!SephiriaMod.TryGetArchipelagoSephiriteChoice(
                __instance.reward.instanceID,
                out locationId,
                out locationName))
        {
            return;
        }

        string itemName;
        string itemDescription;

        if (!SephiriaMod.TryGetArchipelagoSephiriteScoutedDisplay(
                locationId,
                out itemName,
                out itemDescription))
        {
            itemName = locationName;
            itemDescription = "";
        }

        SetTextField(
            tooltip,
            "nameText",
            itemName
        );

        EItemRarity apRarity;

        if (SephiriaMod.TryGetArchipelagoSephiriteRarity(
                locationId,
                out apRarity))
        {
            SetTextRarityColor(
                tooltip,
                "nameText",
                apRarity
            );
        }

        // Remove information belonging to the
        // underlying vanilla shell item.
        SetTextField(
            tooltip,
            "typeText",
            ""
        );

        SetTextField(
            tooltip,
            "flavorText",
            itemDescription
        );

        SetTextField(
            tooltip,
            "levelText",
            ""
        );

        SetTextField(
            tooltip,
            "effectText",
            ""
        );

        ApplyArchipelagoTooltipIcon(
            __instance,
            tooltip
        );

        HideTooltipImageArrayAndRemember(
            __instance,
            tooltip,
            "subIconImages"
        );

        HideTooltipObjectAndRemember(
            __instance,
            tooltip,
            "categoryContainer"
        );

        HideTooltipObjectAndRemember(
            __instance,
            tooltip,
            "categoryViewer"
        );

        HideTooltipObjectAndRemember(
            __instance,
            tooltip,
            "tierDisplay"
        );

        HideTooltipObjectAndRemember(
            __instance,
            tooltip,
            "magicZone"
        );

        HideTooltipObjectAndRemember(
            __instance,
            tooltip,
            "companionZone"
        );

        HideTooltipObjectAndRemember(
            __instance,
            tooltip,
            "sephiriteDisableScreen"
        );

        SephiriaMod.ApplyArchipelagoSephiriteSelectedIcon(
            __instance
        );
    }

    private static void ApplyArchipelagoTooltipIcon(
        UI_SephiriteRewardElement rewardElement,
        object tooltip)
    {
        if (rewardElement == null ||
            tooltip == null)
        {
            return;
        }

        FieldInfo iconField =
            AccessTools.Field(
                tooltip.GetType(),
                "iconImage"
            );

        if (iconField == null)
            return;

        UnityEngine.UI.Image iconImage =
            iconField.GetValue(tooltip)
                as UnityEngine.UI.Image;

        if (iconImage == null ||
            iconImage.sprite == null)
        {
            return;
        }

        int rewardElementId =
            rewardElement.GetInstanceID();

        if (!savedTooltipImageStates.ContainsKey(
                rewardElementId))
        {
            savedTooltipImageStates[
                rewardElementId
            ] = new SavedTooltipImageState
            {
                Image = iconImage,
                Sprite = iconImage.sprite,
                Material = iconImage.material,
                PreserveAspect = iconImage.preserveAspect,
                SizeDelta =
                    iconImage.rectTransform.sizeDelta,
                Rotation =
                    iconImage.rectTransform.rotation
            };
        }

        SephiriaMod.ApplyArchipelagoTooltipIcon(
            iconImage
        );
    }

    private static void SetTextField(
        object tooltip,
        string fieldName,
        string value)
    {
        if (tooltip == null)
            return;

        FieldInfo field =
            AccessTools.Field(
                tooltip.GetType(),
                fieldName
            );

        if (field == null)
            return;

        object textObject =
            field.GetValue(tooltip);

        if (textObject == null)
            return;

        PropertyInfo textProperty =
            AccessTools.Property(
                textObject.GetType(),
                "text"
            );

        if (textProperty == null ||
            !textProperty.CanWrite)
        {
            return;
        }

        textProperty.SetValue(
            textObject,
            value,
            null
        );
    }

    private static void SetTextRarityColor(
        object tooltip,
        string fieldName,
        EItemRarity rarity)
    {
        if (tooltip == null)
            return;

        FieldInfo field =
            AccessTools.Field(
                tooltip.GetType(),
                fieldName
            );

        if (field == null)
            return;

        TMPro.TMP_Text textObject =
            field.GetValue(tooltip)
                as TMPro.TMP_Text;

        if (textObject == null)
            return;

        textObject.color =
            ItemDatabase.GetColorViaItemRarity(
                rarity
            );

        textObject.enableVertexGradient = false;
    }

    private static void HideTooltipObjectAndRemember(
        UI_SephiriteRewardElement rewardElement,
        object tooltip,
        string fieldName)
    {
        if (rewardElement == null ||
            tooltip == null)
        {
            return;
        }

        FieldInfo field =
            AccessTools.Field(
                tooltip.GetType(),
                fieldName
            );

        if (field == null)
            return;

        object value =
            field.GetValue(tooltip);

        if (value == null)
            return;

        GameObject gameObject = null;

        Component component =
            value as Component;

        if (component != null)
        {
            gameObject =
                component.gameObject;
        }
        else
        {
            gameObject =
                value as GameObject;
        }

        if (gameObject == null)
            return;

        int rewardElementId =
            rewardElement.GetInstanceID();

        Dictionary<
            string,
            SavedTooltipObjectState> states;

        if (!savedTooltipObjectStates.TryGetValue(
                rewardElementId,
                out states))
        {
            states =
                new Dictionary<
                    string,
                    SavedTooltipObjectState>();

            savedTooltipObjectStates[
                rewardElementId
            ] = states;
        }

        if (!states.ContainsKey(fieldName))
        {
            states[fieldName] =
                new SavedTooltipObjectState
                {
                    GameObject = gameObject,
                    WasActive = gameObject.activeSelf
                };
        }

        gameObject.SetActive(false);
    }

    private static void HideTooltipImageArrayAndRemember(
        UI_SephiriteRewardElement rewardElement,
        object tooltip,
        string fieldName)
    {
        if (rewardElement == null ||
            tooltip == null)
        {
            return;
        }

        FieldInfo field =
            AccessTools.Field(
                tooltip.GetType(),
                fieldName
            );

        if (field == null)
            return;

        UnityEngine.UI.Image[] images =
            field.GetValue(tooltip)
                as UnityEngine.UI.Image[];

        if (images == null)
            return;

        int rewardElementId =
            rewardElement.GetInstanceID();

        Dictionary<
            string,
            SavedTooltipObjectState> states;

        if (!savedTooltipObjectStates.TryGetValue(
                rewardElementId,
                out states))
        {
            states =
                new Dictionary<
                    string,
                    SavedTooltipObjectState>();

            savedTooltipObjectStates[
                rewardElementId
            ] = states;
        }

        for (int i = 0; i < images.Length; i++)
        {
            UnityEngine.UI.Image image =
                images[i];

            if (image == null)
                continue;

            string stateKey =
                fieldName + "_" + i;

            if (!states.ContainsKey(stateKey))
            {
                states[stateKey] =
                    new SavedTooltipObjectState
                    {
                        GameObject =
                            image.gameObject,
                        WasActive =
                            image.gameObject.activeSelf
                    };
            }

            image.gameObject.SetActive(false);
        }
    }

    public static void RestoreTooltipState(
        UI_SephiriteRewardElement rewardElement)
    {
        if (rewardElement == null)
            return;

        int rewardElementId =
            rewardElement.GetInstanceID();

        Dictionary<
            string,
            SavedTooltipObjectState> states;

        if (savedTooltipObjectStates.TryGetValue(
                rewardElementId,
                out states))
        {
            foreach (SavedTooltipObjectState state
                in states.Values)
            {
                if (state == null ||
                    state.GameObject == null)
                {
                    continue;
                }

                state.GameObject.SetActive(
                    state.WasActive
                );
            }

            savedTooltipObjectStates.Remove(
                rewardElementId
            );
        }

        SavedTooltipImageState imageState;

        if (savedTooltipImageStates.TryGetValue(
                rewardElementId,
                out imageState))
        {
            if (imageState != null &&
                imageState.Image != null)
            {
                imageState.Image.sprite =
                    imageState.Sprite;

                imageState.Image.material =
                    imageState.Material;

                imageState.Image.preserveAspect =
                    imageState.PreserveAspect;

                imageState.Image.rectTransform.sizeDelta =
                    imageState.SizeDelta;

                imageState.Image.rectTransform.rotation =
                    imageState.Rotation;
            }

            savedTooltipImageStates.Remove(
                rewardElementId
            );
        }
    }

    public static void RestoreAllTooltipStates()
    {
        foreach (
            Dictionary<
                string,
                SavedTooltipObjectState> states
            in savedTooltipObjectStates.Values)
        {
            foreach (SavedTooltipObjectState state
                in states.Values)
            {
                if (state == null ||
                    state.GameObject == null)
                {
                    continue;
                }

                state.GameObject.SetActive(
                    state.WasActive
                );
            }
        }

        foreach (SavedTooltipImageState imageState
            in savedTooltipImageStates.Values)
        {
            if (imageState == null ||
                imageState.Image == null)
            {
                continue;
            }

            imageState.Image.sprite =
                imageState.Sprite;

            imageState.Image.material =
                imageState.Material;

            imageState.Image.preserveAspect =
                imageState.PreserveAspect;

            imageState.Image.rectTransform.sizeDelta =
                imageState.SizeDelta;

            imageState.Image.rectTransform.rotation =
                imageState.Rotation;
        }

        savedTooltipImageStates.Clear();

        savedTooltipObjectStates.Clear();
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardElement),
    nameof(UI_SephiriteRewardElement.OnDeselect)
)]
public static class ArchipelagoSephiriteDeselectIconPatch
{
    public static void Postfix(
        UI_SephiriteRewardElement __instance)
    {
        SephiriaMod.ApplyArchipelagoSephiriteNormalIcon(
            __instance
        );
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardElement),
    nameof(UI_SephiriteRewardElement.OnDeselect)
)]
public static class ArchipelagoSephiriteTooltipDeselectPatch
{
    public static void Postfix(
        UI_SephiriteRewardElement __instance)
    {
        ArchipelagoSephiriteTooltipVisualPatch
            .RestoreTooltipState(
                __instance
            );
    }
}

[HarmonyPatch(
    typeof(UI_SephiriteRewardElement),
    "OnDisable"
)]
public static class ArchipelagoSephiriteTooltipDisablePatch
{
    public static void Postfix(
        UI_SephiriteRewardElement __instance)
    {
        ArchipelagoSephiriteTooltipVisualPatch
            .RestoreTooltipState(
                __instance
            );
    }
}

[HarmonyPatch(
    typeof(PlayerAvatar),
    nameof(PlayerAvatar.ServerLoadStage)
)]
public static class ArchipelagoLevelUpStageTransitionPatch
{
    public static bool Prefix(
        PlayerAvatar __instance,
        string stageName)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (__instance == null ||
            DungeonManager.Instance == null)
        {
            return true;
        }

        FloorData currentFloor;

        if (!DungeonManager.Instance.generatedFloors.TryGetValue(
                __instance.currentFloorGuid,
                out currentFloor) ||
            currentFloor == null)
        {
            return true;
        }

        int currentStageProgress;

        if (!DungeonManager.Instance.stageProgress.TryGetValue(
                currentFloor.stageName,
                out currentStageProgress))
        {
            return true;
        }

        int requiredFloorUnlocks;

        if (!SephiriaMod.TryGetRequiredFloorUnlockCount(
                currentStageProgress,
                out requiredFloorUnlocks))
        {
            return true;
        }

        // -------------------------------------------------
        // Level-up gate runs FIRST.
        // -------------------------------------------------

        LevelController levelController =
            __instance.GetComponent<LevelController>();

        if (levelController != null &&
            levelController.levelUpQueue != null &&
            levelController.levelUpQueue.Count > 0)
        {
            MelonLogger.Msg(
                $"[AP Stage Gate] Blocked " +
                $"{currentFloor.stageName} ({currentStageProgress}) -> " +
                $"{stageName}. " +
                $"QueuedLevelUps={levelController.levelUpQueue.Count}"
            );

            if (UIManager.Instance != null)
            {
                UIManager.Instance
                    .GetElement<UI_SystemMessage>()
                    .Open(
                        "Claim your level-up reward before continuing.",
                        4f
                    );
            }

            return false;
        }

        // -------------------------------------------------
        // Floor unlocks only apply to main progression
        // stages, not side stages.
        // -------------------------------------------------

        if (!SephiriaMod.IsMainProgressionStage(
                stageName))
        {
            return true;
        }

        // If this main stage was already generated and is
        // not farther forward than our current stage, this
        // is not a new floor progression gate.
        int targetStageProgress;

        if (DungeonManager.Instance.stageProgress.TryGetValue(
                stageName,
                out targetStageProgress) &&
            targetStageProgress <= currentStageProgress)
        {
            return true;
        }

        int receivedFloorUnlocks =
            SephiriaMod.GetProgressiveFloorUnlockItemCount();

        if (receivedFloorUnlocks >=
            requiredFloorUnlocks)
        {
            return true;
        }

        MelonLogger.Msg(
            $"[AP Floor Gate] Locked transition: " +
            $"{currentFloor.stageName} ({currentStageProgress}) -> " +
            $"{stageName}. " +
            $"FloorUnlocks={receivedFloorUnlocks}/" +
            $"{requiredFloorUnlocks}"
        );

        SephiriaMod.MarkArchipelagoForcedFloorDeath();

        foreach (PlayerSpawner player
            in PlayerSpawner.MultiplayerList)
        {
            if ((bool)player &&
                (bool)player.PlayerAvatar)
            {
                player.PlayerAvatar.ForceDie();
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(HardModePropActivator), "Update")]
public static class ArchipelagoHardModePropPatch
{
    public static bool Prefix(HardModePropActivator __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (__instance.isServer &&
            !__instance.isPropActive)
        {
            __instance.NetworkisPropActive = true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(OpenHardModePanel), "Update")]
public static class ArchipelagoHardModePopupPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return false;
    }
}

[HarmonyPatch(typeof(OpenMultiplayerPanel), "Update")]
public static class ArchipelagoMultiplayerPopupPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return false;
    }
}

[HarmonyPatch(
    typeof(UI_SaveProfilePanel),
    "OnOpened"
)]
public static class ArchipelagoSaveProfileNavigationPatch
{
    public static void Postfix(
        UI_SaveProfilePanel __instance)
    {
        if (__instance == null ||
            __instance.buttons == null)
        {
            return;
        }

        // ---------------------------------------------------------
        // Trash buttons
        // ---------------------------------------------------------
        for (int i = 0;
             i < __instance.buttons.Length;
             i++)
        {
            UI_SaveProfileButton profile =
                __instance.buttons[i];

            if (profile == null ||
                profile.deleteButton == null ||
                !profile.deleteButton.activeInHierarchy)
            {
                continue;
            }

            UnityEngine.UI.Selectable trash =
                profile.deleteButton.GetComponent<
                    UnityEngine.UI.Selectable
                >();

            if (trash == null)
                continue;

            UnityEngine.UI.Selectable startButton =
                FindStartButton(profile);

            UnityEngine.UI.Selectable apButton = null;

            Transform iconTransform =
                profile.transform.Find(
                    "ArchipelagoSaveIcon"
                );

            if (iconTransform != null &&
                iconTransform.gameObject.activeInHierarchy)
            {
                apButton =
                    iconTransform.GetComponent<
                        UnityEngine.UI.Selectable
                    >();
            }

            UnityEngine.UI.Selectable up =
                trash.FindSelectableOnUp();

            UnityEngine.UI.Selectable left =
                FindTrashButton(
                    __instance,
                    i,
                    -1
                );

            UnityEngine.UI.Selectable right =
                FindTrashButton(
                    __instance,
                    i,
                    1
                );

            UnityEngine.UI.Navigation navigation =
                trash.navigation;

            navigation.mode =
                UnityEngine.UI.Navigation.Mode.Explicit;

            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            navigation.selectOnUp = up;

            // AP save: Trash -> AP Icon
            // Normal save: Trash -> Start Game
            navigation.selectOnDown =
                apButton != null
                    ? apButton
                    : startButton;

            trash.navigation = navigation;


        }

        // ---------------------------------------------------------
        // Archipelago logo buttons
        // ---------------------------------------------------------
        for (int i = 0;
             i < __instance.buttons.Length;
             i++)
        {
            UI_SaveProfileButton profile =
                __instance.buttons[i];

            if (profile == null)
                continue;

            Transform iconTransform =
                profile.transform.Find(
                    "ArchipelagoSaveIcon"
                );

            if (iconTransform == null ||
                !iconTransform.gameObject.activeInHierarchy)
            {
                continue;
            }

            UnityEngine.UI.Selectable apButton =
                iconTransform.GetComponent<
                    UnityEngine.UI.Selectable
                >();

            if (apButton == null)
                continue;

            UnityEngine.UI.Selectable startButton =
                FindStartButton(profile);

            UnityEngine.UI.Selectable up =
                apButton.FindSelectableOnUp();

            UnityEngine.UI.Selectable left =
                FindArchipelagoButton(
                    __instance,
                    i,
                    -1
                );

            UnityEngine.UI.Selectable right =
                FindArchipelagoButton(
                    __instance,
                    i,
                    1
                );

            UnityEngine.UI.Navigation navigation =
                apButton.navigation;

            navigation.mode =
                UnityEngine.UI.Navigation.Mode.Explicit;

            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            navigation.selectOnUp = up;

            // AP Icon -> Start Game
            navigation.selectOnDown = startButton;

            apButton.navigation = navigation;
        }
    }

    private static UnityEngine.UI.Selectable FindStartButton(
        UI_SaveProfileButton profile)
    {
        UnityEngine.UI.Selectable[] selectables =
            profile.GetComponentsInChildren<
                UnityEngine.UI.Selectable
            >(true);

        foreach (UnityEngine.UI.Selectable selectable
                 in selectables)
        {
            if (selectable != null &&
                selectable.gameObject.name == "Button")
            {
                return selectable;
            }
        }

        return null;
    }

    private static UnityEngine.UI.Selectable FindTrashButton(
        UI_SaveProfilePanel panel,
        int startIndex,
        int direction)
    {
        for (int i = startIndex + direction;
             i >= 0 && i < panel.buttons.Length;
             i += direction)
        {
            UI_SaveProfileButton profile =
                panel.buttons[i];

            if (profile == null ||
                profile.deleteButton == null ||
                !profile.deleteButton.activeInHierarchy)
            {
                continue;
            }

            UnityEngine.UI.Selectable selectable =
                profile.deleteButton.GetComponent<
                    UnityEngine.UI.Selectable
                >();

            if (selectable != null)
                return selectable;
        }

        return null;
    }

    private static UnityEngine.UI.Selectable FindArchipelagoButton(
        UI_SaveProfilePanel panel,
        int startIndex,
        int direction)
    {
        for (int i = startIndex + direction;
             i >= 0 && i < panel.buttons.Length;
             i += direction)
        {
            UI_SaveProfileButton profile =
                panel.buttons[i];

            if (profile == null)
                continue;

            Transform iconTransform =
                profile.transform.Find(
                    "ArchipelagoSaveIcon"
                );

            if (iconTransform == null ||
                !iconTransform.gameObject.activeInHierarchy)
            {
                continue;
            }

            UnityEngine.UI.Selectable selectable =
                iconTransform.GetComponent<
                    UnityEngine.UI.Selectable
                >();

            if (selectable != null)
                return selectable;
        }

        return null;
    }
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.SetCurrentPlayer)
)]
public static class ArchipelagoCostumePlayerLoadedPatch
{
    public static void Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        // These vanilla flags mean "mission already completed".
        // AP ownership must never suppress the mission trackers.
        SephiriaMod.PrepareArchipelagoCostumeMissionTrackers();
    }

    public static void Postfix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (CombatManager.Instance != null &&
            CombatManager.Instance.unlockables != null)
        {
            for (int i =
                    CombatManager.Instance.unlockables.Count - 1;
                 i >= 0;
                 i--)
            {
                CostumeUnlockEntity tracker =
                    CombatManager.Instance.unlockables[i];

                if (tracker == null ||
                    tracker.unlockCostume == null)
                {
                    continue;
                }

                if (!SephiriaMod
                        .IsRemovedArchipelagoCostumeMission(
                            tracker.unlockCostume.id))
                {
                    continue;
                }

                tracker.ClearTarget();

                CombatManager.Instance.unlockables.RemoveAt(i);

                UnityEngine.Object.Destroy(
                    tracker.gameObject
                );
            }
        }

        SephiriaMod.QueuePermanentItemSync();
    }
}

[HarmonyPatch(
    typeof(CostumeUnlockEntity),
    nameof(CostumeUnlockEntity.Unlock)
)]
public static class ArchipelagoCostumeMissionPatch
{
    public static bool Prefix(
        CostumeUnlockEntity __instance
    )
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (__instance == null ||
            __instance.unlockCostume == null)
        {
            return false;
        }

        string costumeId =
            __instance.unlockCostume.id;

        // Fairy of Fortune is handled separately.
        //
        // Vanilla uses the Chapter 4 completion switch,
        // but AP requires clearing the game on any chapter.
        if (costumeId == "Luck")
        {
            __instance.ClearTarget();

            UnityEngine.Object.Destroy(
                __instance.gameObject
            );

            return false;
        }

        bool completed =
            SephiriaMod.TryCompleteArchipelagoCostumeMission(
                costumeId
            );

        // Only remove the tracker once the AP server
        // accepted the check, or already has it checked.
        //
        // If disconnected or sending fails, leave the
        // tracker alive so the mission can be retried.
        if (completed)
        {
            __instance.ClearTarget();

            UnityEngine.Object.Destroy(
                __instance.gameObject
            );
        }

        // Never award the vanilla costume on an AP save.
        return false;
    }
}

[HarmonyPatch(
    typeof(UI_CostumePanel),
    nameof(UI_CostumePanel.OnOpened)
)]
public static class ArchipelagoCostumePanelOpenPatch
{
    public static void Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        SephiriaMod.IsBuildingArchipelagoCostumePanel =
            true;
    }

    public static void Postfix(
        UI_CostumePanel __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        ArchipelagoCostumeTabController controller =
            __instance.GetComponent<
                ArchipelagoCostumeTabController>();

        if (controller == null)
        {
            controller =
                __instance.gameObject.AddComponent<
                    ArchipelagoCostumeTabController>();
        }

        controller.Initialize(
            __instance
        );
    }

    public static Exception Finalizer(
        Exception __exception)
    {
        SephiriaMod.IsBuildingArchipelagoCostumePanel =
            false;

        return __exception;
    }
}

[HarmonyPatch(
    typeof(CostumeDatabase),
    nameof(CostumeDatabase.IsUnlocked)
)]
public static class ArchipelagoCostumePanelHiddenPatch
{
    public static void Postfix(
        string id,
        ref bool __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!SephiriaMod
                .IsBuildingArchipelagoCostumePanel)
        {
            return;
        }

        // These are the two Hidden costumes.
        // Pretend they're unlocked ONLY while the
        // costume panel is building its list so they
        // aren't omitted.
        if (id == "Bat" ||
            id == "Skeleton")
        {
            __result = true;
        }
    }
}
[HarmonyPatch(
    typeof(CostumeDatabase),
    nameof(CostumeDatabase.IsDungreedDlcOwned)
)]
public static class ArchipelagoCostumePanelDungreedPatch
{
    public static void Postfix(
        ref bool __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!SephiriaMod
                .IsBuildingArchipelagoCostumePanel)
        {
            return;
        }

        // AP ownership of Adventurer Bunny is completely
        // independent of whether Dungreed is owned.
        __result = true;
    }
}

[HarmonyPatch(
    typeof(UI_CostumeListElement),
    nameof(UI_CostumeListElement.Initialize)
)]
public static class ArchipelagoCostumeListElementPatch
{
    public static void Postfix(
        UI_CostumeListElement __instance,
        CostumeEntity costume)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (costume == null)
            return;

        if (SephiriaMod.IsArchipelagoCostumeOwned(
                costume.id))
        {
            return;
        }

        // AP-unowned costumes reveal nothing.
        __instance.nameText.text = "Locked";
        __instance.nameText.color = UnityEngine.Color.gray;

        __instance.iconImage.sprite = null;
        __instance.iconImage.rectTransform.sizeDelta =
            Vector2.zero;

        __instance.backgroundImage.enabled = false;
        __instance.hardModeBackgroundImage.enabled = false;

        __instance.SetEquipCursor(false);
    }
}

[HarmonyPatch(
    typeof(UI_CostumePanel),
    "UpdateData"
)]
public static class ArchipelagoCostumeTooltipPatch
{
    public static void Postfix(
        UI_CostumePanel __instance,
        CostumeEntity costume,
        List<UI_CostumeSkinIcon> ___skinIcons,
        List<UI_ItemIcon> ___startingItemIcons)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (costume == null)
            return;

        if (SephiriaMod.IsArchipelagoCostumeOwned(
                costume.id))
        {
            return;
        }

        // Remove any skin icons vanilla may have created.
        foreach (UI_CostumeSkinIcon skinIcon
            in ___skinIcons)
        {
            if (skinIcon != null)
            {
                UnityEngine.Object.Destroy(
                    skinIcon.gameObject
                );
            }
        }

        ___skinIcons.Clear();

        // Remove starting-item icons vanilla may have
        // created for Default/Dungreed costumes.
        foreach (UI_ItemIcon itemIcon
            in ___startingItemIcons)
        {
            if (itemIcon != null)
            {
                UnityEngine.Object.Destroy(
                    itemIcon.gameObject
                );
            }
        }

        ___startingItemIcons.Clear();

        __instance.startingItem.gameObject.SetActive(
            false
        );

        // AP locked presentation.
        __instance.tooltipNameText.text = "Locked";
        __instance.tooltipFlavortextText.text = "";
        __instance.tooltipEffectText.text = "";

        __instance.tooltipIconImage.sprite = null;
        __instance.tooltipIconImage.gameObject.SetActive(
            false
        );

        __instance.tooltipLocked.SetActive(true);

        // Critically: no vanilla costume mission text.
        __instance.tooltipUnlockConditionText.text =
            "Locked";

        __instance.hardModeValue.SetActive(false);

        __instance.tooltipEquippedCursor.gameObject
            .SetActive(false);
    }
}

[HarmonyPatch(
    typeof(UI_CostumePanel),
    "HandleClick"
)]
public static class ArchipelagoCostumeClickPatch
{
    public static bool Prefix(
        UI_CostumeListElement e)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (e == null ||
            e.Costume == null)
        {
            return false;
        }

        return SephiriaMod.IsArchipelagoCostumeOwned(
            e.Costume.id
        );
    }
}

[HarmonyPatch(
    typeof(UI_CostumePanel),
    nameof(UI_CostumePanel.EquipSkin)
)]
public static class ArchipelagoCostumeSkinEquipPatch
{
    public static bool Prefix(
        UI_CostumeSkinIcon icon)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (icon == null ||
            icon.costume == null)
        {
            return false;
        }

        return SephiriaMod.IsArchipelagoCostumeOwned(
            icon.costume.id
        );
    }
}

[HarmonyPatch(
    typeof(TreeShopItemStorage),
    nameof(TreeShopItemStorage.CanBuyItem)
)]
public static class ArchipelagoPermanentRewardPurchasePatch
{
    public static bool Prefix(
    TreeShopItemEntity shopItem,
    ref bool __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        // Received Upgrades is display-only.
        // Nothing on this version of the tree can be purchased.
        if (ArchipelagoTreeShopModeController.IsTreeShopOpen &&
            ArchipelagoTreeShopModeController.CurrentMode ==
                ArchipelagoTreeShopModeController
                    .TreeMode.ReceivedUpgrades)
        {
            __result = false;
            return false;
        }

        if (shopItem == null ||
            !SephiriaMod
                .IsArchipelagoPermanentRewardTreeShopItem(
                    shopItem.id
                ))
        {
            return true;
        }

        __result = false;

        APDebug.Log(
            $"[AP Permanent Reward] Blocked vanilla " +
            $"TreeShop reward {shopItem.id}."
        );

        return false;
    }
}

[HarmonyPatch(
    typeof(TreeShopItemStorage),
    nameof(TreeShopItemStorage.Load)
)]
public static class ArchipelagoPermanentRewardLoadPatch
{
    public static void Prefix(
        ref DestinyInscriptionPurchaseData[] purchase)
    {
        if (!SephiriaMod.IsArchipelagoSave ||
            purchase == null)
        {
            return;
        }

        purchase = purchase
            .Where(item =>
                !SephiriaMod
                    .IsArchipelagoPermanentRewardTreeShopItem(
                        item.id
                    ))
            .ToArray();
    }
}

[HarmonyPatch(
    typeof(PlayerAvatar),
    nameof(PlayerAvatar.RequestMoveStage)
)]
public static class ArchipelagoDesertStagePatch
{
    public static void Prefix(
        PlayerAvatar __instance,
        ref string stageName)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__instance == null ||
            __instance.isInDungeon <= 0)
        {
            return;
        }

        if (DungeonManager.Instance == null)
            return;

        // ---------------------------------------------
        // Desert_Chapter1 requested
        // ---------------------------------------------

        if (string.Equals(
                stageName,
                "Desert_Chapter1",
                StringComparison.Ordinal))
        {
            StageEntity requestedStage =
                DungeonManager.Instance.FindStage(
                    "Desert_Chapter1"
                );

            StageEntity alternateStage =
                DungeonManager.Instance.FindStage(
                    "Desert_Chapter2"
                );

            // Only change it if Chapter1's desert does
            // NOT exist in this race, but Chapter2's does.
            if (requestedStage == null &&
                alternateStage != null)
            {
                MelonLogger.Msg(
                    $"[AP Chapter {SephiriaMod.GoalChapter}] " +
                    $"Current race does not contain " +
                    $"Desert_Chapter1. Using " +
                    $"Desert_Chapter2."
                );

                stageName = "Desert_Chapter2";
            }

            return;
        }

        // ---------------------------------------------
        // Desert_Chapter2 requested
        // ---------------------------------------------

        if (string.Equals(
                stageName,
                "Desert_Chapter2",
                StringComparison.Ordinal))
        {
            StageEntity requestedStage =
                DungeonManager.Instance.FindStage(
                    "Desert_Chapter2"
                );

            StageEntity alternateStage =
                DungeonManager.Instance.FindStage(
                    "Desert_Chapter1"
                );

            // Same protection in the opposite direction.
            if (requestedStage == null &&
                alternateStage != null)
            {
                MelonLogger.Msg(
                    $"[AP Chapter {SephiriaMod.GoalChapter}] " +
                    $"Current race does not contain " +
                    $"Desert_Chapter2. Using " +
                    $"Desert_Chapter1."
                );

                stageName = "Desert_Chapter1";
            }
        }
    }
}

[HarmonyPatch(
    typeof(UI_JournalPanel),
    nameof(UI_JournalPanel.Connect)
)]
public static class ArchipelagoJournalConnectPatch
{
    private const int ArchipelagoTabIndex = 4;

    public static void Postfix(
        UI_JournalPanel __instance)
    {
        if (__instance == null ||
            __instance.tab == null)
        {
            return;
        }

        ArchipelagoJournalTabState state =
            __instance.GetComponent<
                ArchipelagoJournalTabState>();

        // ---------------------------------------------
        // Normal save
        // ---------------------------------------------
        // If this journal object was previously used by
        // an AP save, restore its original vanilla state.
        // ---------------------------------------------

        if (!SephiriaMod.IsArchipelagoSave)
        {
            if (state != null &&
                state.Initialized)
            {
                RestoreVanillaJournal(
                    __instance,
                    state
                );
            }

            return;
        }

        // Already built for this journal instance.
        if (state != null &&
            state.Initialized)
        {
            return;
        }

        UI_Tab tab =
            __instance.tab;

        // We know the vanilla journal has exactly:
        //
        // 0 = Item
        // 1 = Weapon
        // 2 = Miracle
        // 3 = Lore
        //
        // If that is ever not true, do not modify it.
        if (tab.tabButtons == null ||
            tab.tabContents == null ||
            tab.tabButtons.Length != 4 ||
            tab.tabContents.Length != 4)
        {
            MelonLogger.Warning(
                "[AP Journal] Expected 4 vanilla " +
                "journal tabs. AP tab was not added."
            );

            return;
        }

        if (__instance.loreJournalContent == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Lore content was null. " +
                "AP tab was not added."
            );

            return;
        }

        if (state == null)
        {
            state =
                __instance.gameObject
                    .AddComponent<
                        ArchipelagoJournalTabState>();
        }

        state.OriginalButtons =
            tab.tabButtons;

        state.OriginalContents =
            tab.tabContents;

        state.TabLayout =
            tab.tabButtons[0]
                .transform
                .parent
                .GetComponent<HorizontalLayoutGroup>();

        if (state.TabLayout != null)
        {
            state.OriginalTabSpacing =
                state.TabLayout.spacing;
        }

        // =============================================
        // Clone the Lore top-level button
        // =============================================

        UI_TabButton loreButton =
            tab.tabButtons[3];

        RectTransform tabParent = loreButton.transform.parent
        as RectTransform;

        HorizontalLayoutGroup horizontalLayout =
            loreButton.transform.parent
                .GetComponent<HorizontalLayoutGroup>();

        GridLayoutGroup gridLayout =
            loreButton.transform.parent
                .GetComponent<GridLayoutGroup>();

        UI_TabButton apButton =
            UnityEngine.Object.Instantiate(
                loreButton,
                loreButton.transform.parent
            );

        apButton.name =
            "ArchipelagoJournalTabButton";

        apButton.transform.SetSiblingIndex(
            loreButton.transform.GetSiblingIndex() + 1
        );

        // Change the cloned Lore label to AP.
        TextMeshProUGUI[] buttonTexts =
            apButton.GetComponentsInChildren<TextMeshProUGUI>(
                true
            );

        foreach (TextMeshProUGUI text in buttonTexts)
        {
            if (text != null)
            {
                text.text = "AP";
            }
        }

        state.ArchipelagoButtonTexts =
            buttonTexts;

        // The cloned button also clones Lore's existing
        // click event. Replace the event completely so
        // this button selects index 4 instead of Lore.
        Button clickable =
            apButton.GetComponent<Button>();

        if (clickable == null)
        {
            clickable =
                apButton.GetComponentInChildren<Button>(
                    true
                );
        }

        if (clickable == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Could not find Button " +
                "component on cloned AP tab."
            );

            UnityEngine.Object.Destroy(
                apButton.gameObject
            );

            return;
        }

        clickable.onClick =
            new Button.ButtonClickedEvent();

        clickable.onClick.AddListener(
            delegate
            {
                if (__instance != null)
                {
                    __instance.SelectTab(
                        ArchipelagoTabIndex
                    );
                }
            }
        );

        // =============================================
        // Clone the Lore content page
        // =============================================

        GameObject apContentObject =
            UnityEngine.Object.Instantiate(
                __instance
                    .loreJournalContent
                    .gameObject,
                __instance
                    .loreJournalContent
                    .transform
                    .parent
            );

        apContentObject.name =
            "ArchipelagoJournalContent";

        apContentObject.transform.SetSiblingIndex(
            __instance
                .loreJournalContent
                .transform
                .GetSiblingIndex() + 1
        );

        UI_TabContent apContent =
            apContentObject.GetComponent<
                UI_TabContent>();

        if (apContent == null)
        {
            MelonLogger.Warning(
                "[AP Journal] Cloned Lore page did " +
                "not contain UI_TabContent."
            );

            UnityEngine.Object.Destroy(
                apButton.gameObject
            );

            UnityEngine.Object.Destroy(
                apContentObject
            );

            return;
        }

        UI_JournalContent_Lore clonedLore = apContentObject.GetComponent<UI_JournalContent_Lore>();

        if (clonedLore != null)
        {
            // Stop vanilla Lore logic from controlling
            // the cloned AP page.
            clonedLore.enabled = false;

            ArchipelagoJournalContentController
                apController =
                    apContentObject.AddComponent<
                        ArchipelagoJournalContentController>();

            apController.Initialize(
                clonedLore,
                __instance.itemJournalContent != null
                    ? __instance.itemJournalContent.transform
                    : null
            );

            apController.ConfigureNavigation(clickable);
        }

        apContentObject.SetActive(false);

        // =============================================
        // Add index 4 to UI_Tab
        // =============================================

        UI_TabButton[] newButtons =
            new UI_TabButton[5];

        UI_TabContent[] newContents =
            new UI_TabContent[5];

        Array.Copy(
            state.OriginalButtons,
            newButtons,
            4
        );

        Array.Copy(
            state.OriginalContents,
            newContents,
            4
        );

        newButtons[ArchipelagoTabIndex] =
            apButton;

        newContents[ArchipelagoTabIndex] =
            apContent;

        tab.tabButtons =
            newButtons;

        tab.tabContents =
            newContents;

        state.ArchipelagoButton =
            apButton;

        state.ArchipelagoContent = apContent;

        // Only change the layout after the AP journal
        // was successfully constructed.
        if (state.TabLayout != null)
        {
            state.TabLayout.spacing = 8f;
        }

        state.Initialized = true;

        MelonLogger.Msg(
            "[AP Journal] Added AP journal tab."
        );
    }

    private static void RestoreVanillaJournal(
        UI_JournalPanel panel,
        ArchipelagoJournalTabState state)
    {
        if (panel == null ||
            panel.tab == null ||
            state == null)
        {
            return;
        }

        if (state.OriginalButtons != null)
        {
            panel.tab.tabButtons =
                state.OriginalButtons;
        }

        if (state.OriginalContents != null)
        {
            panel.tab.tabContents =
                state.OriginalContents;
        }

        if (state.TabLayout != null)
        {
            state.TabLayout.spacing =
                state.OriginalTabSpacing;
        }

        if (state.ArchipelagoButton != null)
        {
            UnityEngine.Object.Destroy(
                state.ArchipelagoButton.gameObject
            );
        }

        if (state.ArchipelagoContent != null)
        {
            UnityEngine.Object.Destroy(
                state.ArchipelagoContent.gameObject
            );
        }

        state.ArchipelagoButton = null;
        state.ArchipelagoContent = null;
        state.ArchipelagoController = null;
        state.ArchipelagoButtonTexts = null;
        state.Initialized = false;

        MelonLogger.Msg(
            "[AP Journal] Restored vanilla journal."
        );
    }
}

[HarmonyPatch(
    typeof(BossSpawner),
    nameof(BossSpawner.ClearBoss)
)]
public static class ArchipelagoBossDefeatPatch
{
    public static void Postfix(
        BossSpawner __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__instance == null ||
            __instance.treeShopReward == null)
        {
            return;
        }

        SephiriaMod.TryCompleteArchipelagoBossDefeat(
            __instance.treeShopReward.id
        );
    }
}

[HarmonyPatch(
    typeof(UI_TreeShopPanel),
    nameof(UI_TreeShopPanel.OnOpened)
)]
public static class ArchipelagoTreeShopModePatch
{
    public static void Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        ArchipelagoTreeShopModeController
            .PrepareForOpen();
    }


    public static void Postfix(
        UI_TreeShopPanel __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        ArchipelagoTreeShopModeController
            controller =
                __instance.GetComponent<
                    ArchipelagoTreeShopModeController>();

        if (controller == null)
        {
            controller =
                __instance.gameObject.AddComponent<
                    ArchipelagoTreeShopModeController>();
        }

        controller.Initialize(
            __instance
        );
    }
}

[HarmonyPatch(
    typeof(UI_TreeShopPanel),
    nameof(UI_TreeShopPanel.OnClosed)
)]
public static class ArchipelagoTreeShopClosedPatch
{
    public static void Postfix()
    {
        ArchipelagoTreeShopModeController
            .MarkClosed();
    }
}

[HarmonyPatch(
    typeof(SaveData),
    nameof(SaveData.GetInt),
    new Type[]
    {
        typeof(string),
        typeof(int)
    }
)]
public static class ArchipelagoTreeShopGetIntPatch
{
    public static void Prefix(
        ref string key)
    {
        key =
            ArchipelagoTreeShopSaveRedirect
                .RedirectKey(key);
    }


    public static void Postfix(
        string key,
        ref int __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!ArchipelagoTreeShopModeController
                .IsTreeShopOpen)
        {
            return;
        }

        if (ArchipelagoTreeShopModeController
                .CurrentMode !=
            ArchipelagoTreeShopModeController
                .TreeMode.ReceivedUpgrades)
        {
            return;
        }

        const string prefix =
            "TreeShopItem_";

        const string suffix =
            "_PurchasedCount";

        if (string.IsNullOrEmpty(key) ||
            !key.StartsWith(
                prefix,
                StringComparison.Ordinal) ||
            !key.EndsWith(
                suffix,
                StringComparison.Ordinal))
        {
            return;
        }

        string idText =
            key.Substring(
                prefix.Length,
                key.Length -
                prefix.Length -
                suffix.Length
            );

        int treeShopItemId;

        if (!int.TryParse(
                idText,
                out treeShopItemId))
        {
            return;
        }

        int receivedCount;

        if (!SephiriaMod
                .TryGetArchipelagoReceivedTreeShopCount(
                    treeShopItemId,
                    out receivedCount))
        {
            return;
        }

        __result = receivedCount;
    }
}

[HarmonyPatch(
    typeof(SaveData),
    nameof(SaveData.SetInt),
    new Type[]
    {
        typeof(string),
        typeof(int)
    }
)]
public static class ArchipelagoTreeShopSetIntPatch
{
    public static void Prefix(
        ref string key)
    {
        key =
            ArchipelagoTreeShopSaveRedirect
                .RedirectKey(key);
    }
}

[HarmonyPatch(
    typeof(TreeShopItemStorage),
    nameof(TreeShopItemStorage.BuyItem)
)]
public static class ArchipelagoTreeShopBuyItemPatch
{
    public static bool Prefix(int id)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (!ArchipelagoTreeShopModeController.IsTreeShopOpen)
            return true;

        if (ArchipelagoTreeShopModeController.CurrentMode !=
            ArchipelagoTreeShopModeController.TreeMode.ArchipelagoTree)
        {
            return false;
        }

        // Root's Retreat is handled separately.
        if (id == 3000)
            return true;

        TreeShopItemEntity shopItem =
            TreeShopItemDatabase.FindById(id);

        if (shopItem == null)
        {
            MelonLogger.Warning(
                $"[AP Tree] Could not find TreeShop item {id}."
            );

            return false;
        }

        int currentCount =
            SaveManager.Current.GetInt(
                $"TreeShopItem_{id}_PurchasedCount",
                0
            );

        if (currentCount >= shopItem.maxQuantity)
            return false;

        int newCount = currentCount + 1;

        SaveManager.Current.SetInt(
            $"TreeShopItem_{id}_PurchasedCount",
            newCount
        );

        SaveManager.Save(
            saveCurrent: true,
            saveCurrentRun: false
        );

        MelonLogger.Msg(
            $"[AP Tree] Purchased node {id}: " +
            $"{newCount}/{shopItem.maxQuantity}"
        );

        if (newCount >= shopItem.maxQuantity)
        {
            MelonLogger.Msg(
                $"[AP Tree] Node {id} fully purchased."
            );

            SephiriaMod
                .TryCompleteArchipelagoDestinyInscription(
                    id
                );
        }

        // CRITICAL:
        // Do not run vanilla BuyItem().
        // That would actually grant the Sephiria upgrade.
        return false;
    }
}

[HarmonyPatch(
    typeof(UI_HUDMenu),
    "UpdatePassiveButton"
)]
public static class ArchipelagoPassiveButtonPatch
{
    public static IEnumerable<CodeInstruction>
        Transpiler(
            IEnumerable<CodeInstruction> instructions)
    {
        return SephiriaMod.ReplacePassiveAccessCheck(
            instructions
        );
    }
}

[HarmonyPatch(
    typeof(PlayerInputController),
    nameof(PlayerInputController.HandleOpenPassivePanel)
)]
public static class ArchipelagoPassiveInputPatch
{
    public static IEnumerable<CodeInstruction>
        Transpiler(
            IEnumerable<CodeInstruction> instructions)
    {
        return SephiriaMod.ReplacePassiveAccessCheck(
            instructions
        );
    }
}

[HarmonyPatch(
    typeof(UI_PassivePanel),
    nameof(UI_PassivePanel.OnOpened)
)]
public static class ArchipelagoTalentTutorialPatch
{
    public static void Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        SaveManager.Current.SetBool(
            "TalentTutorial2",
            false
        );
    }
}

[HarmonyPatch(
    typeof(AbilitySelector),
    "HandleInteraction"
)]
public static class ArchipelagoTalentMirrorPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        return SephiriaMod.HasArchipelagoTalentAccess();
    }
}

[HarmonyPatch(
    typeof(Interactable),
    nameof(Interactable.GetInteractionString)
)]
public static class ArchipelagoTalentMirrorTextPatch
{
    public static void Postfix(
        Interactable __instance,
        ref string __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        // Only modify Talent mirror interactables.
        if (__instance.GetComponent<AbilitySelector>() == null)
            return;

        if (SephiriaMod.HasArchipelagoTalentAccess())
            return;

        __result = "LOCKED";
    }
}

[HarmonyPatch(
    typeof(SwitchManager),
    nameof(SwitchManager.SetDestinySwitch)
)]
public static class ArchipelagoHomeRepairsMessagePatch
{
    public static void Prefix(
        string destinySwitchName,
        bool value,
        ref bool inscriptionNotify,
        out bool __state)
    {
        __state =
            SephiriaMod.IsArchipelagoSave &&
            destinySwitchName == "RebuildHomeClear" &&
            value &&
            inscriptionNotify;

        if (__state)
        {
            // Prevent the vanilla notification from appearing.
            // The switch itself will still be set normally.
            inscriptionNotify = false;
        }
    }

    public static void Postfix(bool __state)
    {
        if (!__state)
            return;

        UIManager.Instance
            .GetElement<UI_DestinyChangeHUD>()
            .Open("Your house has been repaired.");
    }
}

[HarmonyPatch(
    typeof(QuestController),
    "Reward",
    new Type[] { typeof(QuestReward[]) }
)]
public static class ArchipelagoHomeRepairsCompletionPatch
{
    public static void Postfix(QuestReward[] __0)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__0 == null)
            return;

        foreach (QuestReward reward in __0)
        {
            if (reward == null)
                continue;

            if (reward.type !=
                QuestReward.Type.DestinyChange)
            {
                continue;
            }

            if (reward.destinyChangeKey !=
                "RebuildHomeClear")
            {
                continue;
            }

            if (!reward.destinyChangeValue)
                continue;

            SephiriaMod.TryCompleteArchipelagoMission(
                "Home Repairs"
            );

            return;
        }
    }
}

[HarmonyPatch(
    typeof(QuestController),
    "Reward",
    new Type[] { typeof(QuestReward[]) }
)]
public static class ArchipelagoMagicWorkshopCompletionPatch
{
    public static void Prefix(
        ref QuestReward[] __0,
        out bool __state)
    {
        __state = false;

        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__0 == null)
            return;

        __state = __0.Any(
            reward =>
                reward != null &&
                reward.type ==
                    QuestReward.Type.PotionStorage
        );

        if (!__state)
            return;

        // Do not grant the vanilla Potion Storage reward.
        // Potion Storage is an Archipelago item instead.
        __0 = __0
            .Where(
                reward =>
                    reward == null ||
                    reward.type !=
                        QuestReward.Type.PotionStorage
            )
            .ToArray();
    }

    public static void Postfix(bool __state)
    {
        if (!__state)
            return;

        SephiriaMod.TryCompleteArchipelagoMission(
            "Magic Workshop"
        );
    }
}

[HarmonyPatch(
    typeof(QuestController),
    "Reward",
    new Type[] { typeof(QuestReward[]) }
)]
public static class ArchipelagoRyliesGiftCompletionPatch
{
    public static void Prefix(
        ref QuestReward[] __0,
        out bool __state)
    {
        __state = false;

        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__0 == null)
            return;

        __state = __0.Any(
            reward =>
                reward != null &&
                reward.type ==
                    QuestReward.Type.DestinyChange &&
                string.Equals(
                    reward.destinyChangeKey,
                    "StartingPotionLV1",
                    StringComparison.Ordinal
                ) &&
                reward.destinyChangeValue
        );

        if (!__state)
            return;

        // Starting Potion comes from Archipelago instead.
        __0 = __0
            .Where(
                reward =>
                    reward == null ||
                    reward.type !=
                        QuestReward.Type.DestinyChange ||
                    !string.Equals(
                        reward.destinyChangeKey,
                        "StartingPotionLV1",
                        StringComparison.Ordinal
                    )
            )
            .ToArray();
    }

    public static void Postfix(bool __state)
    {
        if (!__state)
            return;

        SephiriaMod.TryCompleteArchipelagoMission(
            "Rylie's Gift"
        );
    }
}

[HarmonyPatch(
    typeof(FunctionNode_CreateItemOnGround),
    nameof(FunctionNode_CreateItemOnGround.DoSomething)
)]
public static class ArchipelagoRyliePotionDropPatch
{
    public static bool Prefix(
        FunctionNode_CreateItemOnGround __instance,
        ref IEnumerator __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        // Rylie's physical reward is item ID 0.
        if (__instance.itemEntity == null ||
            __instance.itemEntity.id != 0)
        {
            return true;
        }

        if (CombatManager.Instance == null ||
            CombatManager.Instance.CurrentPlayer == null)
        {
            return true;
        }

        QuestController questController =
            CombatManager.Instance.CurrentPlayer
                .GetComponent<QuestController>();

        if (questController == null)
            return true;

        bool rylieQuestActive =
            questController.IsDoingQuest(
                "Destiny_RyliePotion0") ||
            questController.IsDoingQuest(
                "Destiny_RyliePotion0-1") ||
            questController.IsDoingQuest(
                "Destiny_RyliePotion0-2") ||
            questController.IsDoingQuest(
                "Destiny_RyliePotion0-3");

        if (!rylieQuestActive)
            return true;

        __result = SkipPotionDrop();

        APDebug.Log(
            "[AP Mission] Suppressed Rylie's vanilla potion drop."
        );

        return false;
    }

    private static IEnumerator SkipPotionDrop()
    {
        yield return null;
    }
}

[HarmonyPatch(
    typeof(QuestController),
    "Reward",
    new Type[] { typeof(QuestReward[]) }
)]
public static class ArchipelagoTerrainRecoveryCompletionPatch
{
    public static void Prefix(
        ref QuestReward[] __0,
        out bool __state)
    {
        __state = false;

        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__0 == null)
            return;

        bool hasTerrainRepair =
            __0.Any(
                reward =>
                    reward != null &&
                    reward.type ==
                        QuestReward.Type.DestinyChange &&
                    string.Equals(
                        reward.destinyChangeKey,
                        "FillTheCliffClear",
                        StringComparison.Ordinal
                    ) &&
                    reward.destinyChangeValue
            );

        bool hasWillpowerUnlock =
            __0.Any(
                reward =>
                    reward != null &&
                    reward.type ==
                        QuestReward.Type.DestinyChange &&
                    string.Equals(
                        reward.destinyChangeKey,
                        "Passive_9_Unlocked",
                        StringComparison.Ordinal
                    ) &&
                    reward.destinyChangeValue
            );

        // This exact combination identifies the final
        // Terrain Recovery reward.
        __state =
            hasTerrainRepair &&
            hasWillpowerUnlock;

        if (!__state)
            return;

        // Keep FillTheCliffClear so the terrain remains
        // physically repaired.
        //
        // Remove only Passive_9_Unlocked because
        // Willpower is now an Archipelago item.
        __0 = __0
            .Where(
                reward =>
                    reward == null ||
                    reward.type !=
                        QuestReward.Type.DestinyChange ||
                    !string.Equals(
                        reward.destinyChangeKey,
                        "Passive_9_Unlocked",
                        StringComparison.Ordinal
                    )
            )
            .ToArray();
    }

    public static void Postfix(bool __state)
    {
        if (!__state)
            return;

        SephiriaMod.TryCompleteArchipelagoMission(
            "Terrain Recovery"
        );
    }
}

[HarmonyPatch(
    typeof(QuestController),
    "Reward",
    new Type[] { typeof(QuestReward[]) }
)]
public static class ArchipelagoBasementCompletionPatch
{
    public static void Prefix(
        ref QuestReward[] __0,
        out bool __state)
    {
        __state = false;

        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__0 == null)
            return;

        __state =
            __0.Any(
                reward =>
                    reward != null &&
                    reward.type ==
                        QuestReward.Type.DestinyChange &&
                    string.Equals(
                        reward.destinyChangeKey,
                        "CostumeDyeing",
                        StringComparison.Ordinal
                    ) &&
                    reward.destinyChangeValue
            );

        if (!__state)
            return;

        // Costume Dyeing comes from Archipelago instead.
        __0 = __0
            .Where(
                reward =>
                    reward == null ||
                    reward.type !=
                        QuestReward.Type.DestinyChange ||
                    !string.Equals(
                        reward.destinyChangeKey,
                        "CostumeDyeing",
                        StringComparison.Ordinal
                    )
            )
            .ToArray();
    }

    public static void Postfix(bool __state)
    {
        if (!__state)
            return;

        SephiriaMod.TryCompleteArchipelagoMission(
            "Basement"
        );
    }
}

[HarmonyPatch(
    typeof(QuestController),
    "Reward",
    new Type[] { typeof(QuestReward[]) }
)]
public static class ArchipelagoHideAndSeekCompletionPatch
{
    public static void Prefix(
        ref QuestReward[] __0,
        out bool __state)
    {
        __state = false;

        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__0 == null)
            return;

        __state =
            __0.Any(
                reward =>
                    reward != null &&
                    reward.type ==
                        QuestReward.Type.DestinyChange &&
                    string.Equals(
                        reward.destinyChangeKey,
                        "Passive_11_Unlocked",
                        StringComparison.Ordinal
                    ) &&
                    reward.destinyChangeValue
            );

        if (!__state)
            return;

        // Ingenuity comes from Archipelago instead.
        __0 = __0
            .Where(
                reward =>
                    reward == null ||
                    reward.type !=
                        QuestReward.Type.DestinyChange ||
                    !string.Equals(
                        reward.destinyChangeKey,
                        "Passive_11_Unlocked",
                        StringComparison.Ordinal
                    )
            )
            .ToArray();
    }

    public static void Postfix(bool __state)
    {
        if (!__state)
            return;

        SephiriaMod.TryCompleteArchipelagoMission(
            "Hide and Seek"
        );
    }
}

[HarmonyPatch(
    typeof(QuestController),
    "Reward",
    new Type[] { typeof(QuestReward[]) }
)]
public static class ArchipelagoFruitSkewerCompletionPatch
{
    public static void Prefix(
        ref QuestReward[] __0,
        out bool __state)
    {
        __state = false;

        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__0 == null)
            return;

        __state =
            __0.Any(
                reward =>
                    reward != null &&
                    reward.type ==
                        QuestReward.Type.DestinyChange &&
                    string.Equals(
                        reward.destinyChangeKey,
                        "FruitSkewerClear",
                        StringComparison.Ordinal
                    ) &&
                    reward.destinyChangeValue
            );

        if (!__state)
            return;

        // Fruit Skewer functionality comes from
        // the Archipelago item instead.
        __0 = __0
            .Where(
                reward =>
                    reward == null ||
                    reward.type !=
                        QuestReward.Type.DestinyChange ||
                    !string.Equals(
                        reward.destinyChangeKey,
                        "FruitSkewerClear",
                        StringComparison.Ordinal
                    )
            )
            .ToArray();
    }

    public static void Postfix(bool __state)
    {
        if (!__state)
            return;

        // Remember that the Fruit Skewer mission itself
        // has been completed.
        if (SaveManager.Current != null)
        {
            SaveManager.Current.SetBool(
                "ArchipelagoFruitSkewerMissionComplete",
                true
            );

            SaveManager.Save(
                saveCurrent: true,
                saveCurrentRun: false
            );
        }

        bool fruitSkewerUnlocked =
            SwitchManager.GetDestinySwitch(
                "FruitSkewerClear",
                fallback: false
            );

        // The mission requires the player to create a
        // temporary Fruit Skewer. Remove it only if the
        // permanent AP unlock has NOT already been received.
        if (!fruitSkewerUnlocked)
        {
            if (SaveManager.Current != null)
            {
                SaveManager.Current.SetInt(
                    "FruitSkewer_FruitCount",
                    0
                );

                SaveManager.Current.SetInt(
                    "FruitSkewer_AdaptiveItemDropBonus",
                    0
                );

                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );
            }

            if (CombatManager.Instance != null &&
                CombatManager.Instance.CurrentPlayer != null &&
                NetworkServer.active)
            {
                PlayerLocalDataStorage data =
                    CombatManager.Instance.CurrentPlayer
                        .GetComponent<PlayerLocalDataStorage>();

                if (data != null)
                {
                    data.UpdateFruitSkewerBonus();
                }
            }

            APDebug.Log(
                "[AP Mission] Removed temporary Fruit Skewer."
            );
        }

        SephiriaMod.TryCompleteArchipelagoMission(
            "Fruit Skewer"
        );

        SephiriaMod.RefreshArchipelagoFruitSkewerStand();
    }
}

[HarmonyPatch(
    typeof(UI_FruitSkewerPanel),
    nameof(UI_FruitSkewerPanel.OnOpened)
)]
public static class ArchipelagoFruitSkewerTutorialPatch
{
    public static void Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (SaveManager.Current == null)
            return;

        SaveManager.Current.SetBool(
            "FruitSkewerTutorial",
            false
        );
    }
}

[HarmonyPatch(
    typeof(FruitSkewerSelector),
    nameof(FruitSkewerSelector.OpenUI)
)]
public static class ArchipelagoFruitSkewerSelectorPatch
{
    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        // AP Fruit Skewer Unlock received.
        if (SwitchManager.GetDestinySwitch(
                "FruitSkewerClear",
                fallback: false))
        {
            return true;
        }

        if (CombatManager.Instance == null ||
            CombatManager.Instance.CurrentPlayer == null)
        {
            return false;
        }

        QuestController questController =
            CombatManager.Instance.CurrentPlayer
                .GetComponent<QuestController>();

        if (questController == null)
            return false;

        // Allow the stand temporarily while the
        // Fruit Skewer mission is in progress.
        return
            questController.IsDoingQuest(
                "Destiny_FruitSkewer_0") ||
            questController.IsDoingQuest(
                "Destiny_FruitSkewer_1") ||
            questController.IsDoingQuest(
                "Destiny_FruitSkewer_2") ||
            questController.IsDoingQuest(
                "Destiny_FruitSkewer_3") ||
            questController.IsDoingQuest(
                "Destiny_FruitSkewer_4");
    }
}

[HarmonyPatch(
    typeof(PlayerLocalDataStorage),
    nameof(PlayerLocalDataStorage.CheckWeaponUnlocked)
)]
public static class ArchipelagoWeaponUnlockedPatch
{
    public static bool Prefix(
        PlayerLocalDataStorage __instance)
    {
        // Normal saves keep 100% vanilla behavior.
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        __instance.isWeaponUnlocked.Clear();

        if (SaveManager.Current.GetBool(
                "ArchipelagoSwordAndShieldUnlocked",
                false))
        {
            __instance.isWeaponUnlocked.Add(
                EWeaponType.SwordAndShield,
                true
            );
        }

        if (SwitchManager.GetDestinySwitch(
                "GreatswordUnlocked",
                false))
        {
            __instance.isWeaponUnlocked.Add(
                EWeaponType.GreatSword,
                true
            );
        }

        if (SwitchManager.GetDestinySwitch(
                "DaggerUnlocked",
                false))
        {
            __instance.isWeaponUnlocked.Add(
                EWeaponType.Dagger,
                true
            );
        }

        if (SwitchManager.GetDestinySwitch(
                "CrossbowUnlocked",
                false))
        {
            __instance.isWeaponUnlocked.Add(
                EWeaponType.Crossbow,
                true
            );
        }

        if (SwitchManager.GetDestinySwitch(
                "KatanaUnlocked",
                false))
        {
            __instance.isWeaponUnlocked.Add(
                EWeaponType.Katana,
                true
            );
        }

        if (SwitchManager.GetDestinySwitch(
            "StaffUnlocked",
            false))
        {
            __instance.isWeaponUnlocked.Add(
                EWeaponType.Staff,
                true
            );
        }

        return false;
    }
}

[HarmonyPatch(
    typeof(Interactable),
    "Awake"
)]
public static class ArchipelagoWeaponStandAwakePatch
{
    public static void Postfix(
        Interactable __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!(__instance is TownWeaponStand))
            return;

        SephiriaMod.QueueArchipelagoWeaponStandSync();
    }
}

[HarmonyPatch(
    typeof(TownWeaponStand),
    nameof(TownWeaponStand.Interactive)
)]
public static class ArchipelagoWeaponStandInteractionPatch
{
    public static bool Prefix(
        TownWeaponStand __instance)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (__instance == null ||
            __instance.weapon == null ||
            __instance.weapon.mainWeaponPrefab == null)
        {
            return true;
        }

        WeaponSimple weaponSimple =
            __instance.weapon.mainWeaponPrefab
                .GetComponent<WeaponSimple>();

        if (weaponSimple == null)
            return true;

        EWeaponType weaponType =
            weaponSimple.weaponType;

        switch (weaponType)
        {
            case EWeaponType.SwordAndShield:
            case EWeaponType.GreatSword:
            case EWeaponType.Dagger:
            case EWeaponType.Crossbow:
            case EWeaponType.Staff:
            case EWeaponType.Katana:
                return SephiriaMod
                    .IsArchipelagoWeaponOwned(
                        weaponType
                    );

            default:
                return true;
        }
    }
}

[HarmonyPatch(
    typeof(UI_TreeShopItem),
    nameof(UI_TreeShopItem.SetVisible)
)]
public static class ArchipelagoTreeShopItemVisualPatch
{
    public static void Postfix(
        UI_TreeShopItem __instance,
        int value)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!ArchipelagoTreeShopModeController
            .IsTreeShopOpen)
        {
            return;
        }

        if (ArchipelagoTreeShopModeController
            .CurrentMode !=
            ArchipelagoTreeShopModeController
                .TreeMode.ArchipelagoTree)
        {
            return;
        }

        if (__instance == null ||
            __instance.connected == null ||
            __instance.iconImage == null)
        {
            return;
        }

        // Root's Retreat belongs to the separate
        // Hard Mode system, not the AP tree.
        if (__instance.connected.id == 3000)
            return;

        // If Sephiria has hidden the entire node,
        // leave it hidden.
        if (value == 0)
            return;

        int purchasedCount =
            SaveManager.Current.GetInt(
                $"TreeShopItem_{__instance.connected.id}_PurchasedCount",
                0
            );

        bool fullyClaimed =
            purchasedCount >=
            __instance.connected.maxQuantity;

        if (!fullyClaimed)
        {
            // The AP Tree does not reveal the
            // vanilla reward represented by this node.
            __instance.iconImage
                .gameObject.SetActive(false);

            return;
        }

        // Fully claimed AP location:
        // replace the vanilla reward icon with
        // the Archipelago logo.
        UnityEngine.Sprite apSprite =
            SephiriaMod
                .GetArchipelagoSaveProfileSprite(
                    __instance.connected.icon
                );

        if (apSprite == null)
        {
            __instance.iconImage
                .gameObject.SetActive(false);

            return;
        }

        // Remember the normal size of this node's
        // vanilla reward icon before replacing it.
        Vector2 normalIconSize =
            __instance.iconImage
                .rectTransform.sizeDelta;

        __instance.iconImage
            .gameObject.SetActive(true);

        __instance.iconImage.sprite =
            apSprite;

        __instance.iconImage.preserveAspect =
            true;

        __instance.iconImage.material =
            null;

        // The AP logo is visually much larger than
        // Sephiria's normal reward icons, so display
        // it at 65% of the normal icon area.
        __instance.iconImage
            .rectTransform.sizeDelta =
                normalIconSize * 0.65f;
    }
}

public sealed class ArchipelagoTreeShopMissionLockState
{
    public TreeShopItemEntity Item;
    public string RelatedDestinySwitch;
}


public static class ArchipelagoTreeShopMissionLockBypass
{
    public static ArchipelagoTreeShopMissionLockState Begin(
        UI_TreeShopItem item)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return null;

        if (!ArchipelagoTreeShopModeController
                .IsTreeShopOpen)
        {
            return null;
        }

        if (ArchipelagoTreeShopModeController
                .CurrentMode !=
            ArchipelagoTreeShopModeController
                .TreeMode.ArchipelagoTree)
        {
            return null;
        }

        if (item == null ||
            item.connected == null)
        {
            return null;
        }

        int id = item.connected.id;

        // These are the two vanilla mission-gated
        // nodes in the Destiny Inscription tree:
        //
        // 300 = Talent Point I
        //       requires Home Repairs
        //
        // 18  = Hide and Seek
        //       requires Ark Exploration
        if (id != 300 &&
            id != 18)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(
                item.connected.relatedDestinySwitch))
        {
            return null;
        }

        ArchipelagoTreeShopMissionLockState state =
            new ArchipelagoTreeShopMissionLockState
            {
                Item = item.connected,
                RelatedDestinySwitch =
                    item.connected.relatedDestinySwitch
            };

        // Temporarily remove the vanilla mission gate.
        item.connected.relatedDestinySwitch = "";

        return state;
    }


    public static void End(
        ArchipelagoTreeShopMissionLockState state)
    {
        if (state == null ||
            state.Item == null)
        {
            return;
        }

        // Put the vanilla data straight back.
        state.Item.relatedDestinySwitch =
            state.RelatedDestinySwitch;
    }
}

[HarmonyPatch(
    typeof(UI_TreeShopPanel),
    "Update"
)]
public static class ArchipelagoTreeShopMissionLockUpdatePatch
{
    public static void Prefix(
        UI_TreeShopItem ___currentSelected,
        out ArchipelagoTreeShopMissionLockState __state)
    {
        __state =
            ArchipelagoTreeShopMissionLockBypass.Begin(
                ___currentSelected
            );
    }


    public static void Postfix(
        ArchipelagoTreeShopMissionLockState __state)
    {
        ArchipelagoTreeShopMissionLockBypass.End(
            __state
        );
    }
}


[HarmonyPatch(
    typeof(UI_TreeShopPanel),
    "HandleItemSelected"
)]
public static class ArchipelagoTreeShopMissionLockTooltipPatch
{
    public static void Prefix(
        UI_TreeShopItem item,
        out ArchipelagoTreeShopMissionLockState __state)
    {
        __state =
            ArchipelagoTreeShopMissionLockBypass.Begin(
                item
            );
    }


    public static void Postfix(
        ArchipelagoTreeShopMissionLockState __state)
    {
        ArchipelagoTreeShopMissionLockBypass.End(
            __state
        );
    }
}

[HarmonyPatch(
    typeof(UI_TreeShopPanel),
    "HandleItemSelected"
)]
public static class ArchipelagoTreeShopTooltipPatch
{
    public static void Postfix(
        UI_TreeShopPanel __instance,
        UI_TreeShopItem item)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!ArchipelagoTreeShopModeController
                .IsTreeShopOpen)
        {
            return;
        }

        // -------------------------------------------------
        // RECEIVED UPGRADES
        // -------------------------------------------------
        if (ArchipelagoTreeShopModeController
                .CurrentMode ==
            ArchipelagoTreeShopModeController
                .TreeMode.ReceivedUpgrades)
        {
            if (item == null ||
                item.connected == null)
            {
                return;
            }

            // Root's Retreat belongs to Hard Mode.
            if (item.connected.id == 3000)
            {
                return;
            }

            int receivedCount;

            SephiriaMod
                .TryGetArchipelagoReceivedTreeShopCount(
                    item.connected.id,
                    out receivedCount
                );

            bool hasReceivedUpgrade =
                receivedCount > 0;

            // Vanilla may mark this node as locked because
            // its normal tree prerequisites have not been met.
            //
            // On Received Upgrades, prerequisite progression
            // does not matter. The only thing that matters is
            // whether Archipelago actually sent this upgrade.
            if (__instance.tooltipDisabledPanel != null)
            {
                __instance.tooltipDisabledPanel
                    .gameObject.SetActive(
                        !hasReceivedUpgrade
                    );
            }

            if (__instance.tooltipDisabledText_Locked != null)
            {
                __instance.tooltipDisabledText_Locked
                    .gameObject.SetActive(
                        !hasReceivedUpgrade
                    );
            }

            if (__instance.tooltipDisabledText_WIP != null)
            {
                __instance.tooltipDisabledText_WIP
                    .gameObject.SetActive(false);
            }

            if (__instance
                    .tooltipDisabledText_Locked_DestinySwitch != null)
            {
                __instance
                    .tooltipDisabledText_Locked_DestinySwitch
                    .gameObject.SetActive(false);
            }

            // AP Tree can hide this icon. Received Upgrades
            // always uses Sephiria's normal vanilla icon.
            if (__instance.tooltipIconImage != null)
            {
                __instance.tooltipIconImage
                    .gameObject.SetActive(true);
            }

            return;
        }

        if (item == null ||
            item.connected == null)
        {
            return;
        }

        // Root's Retreat belongs to Hard Mode.
        // It is not an AP Tree location, so make
        // sure its normal icon remains visible.
        if (item.connected.id == 3000)
        {
            if (__instance.tooltipIconImage != null)
            {
                __instance.tooltipIconImage
                    .gameObject.SetActive(true);
            }

            return;
        }

        string itemName;
        string itemDescription;

        if (!SephiriaMod
                .TryGetArchipelagoDestinyInscriptionScoutedDisplay(
                    item.connected.id,
                    out itemName,
                    out itemDescription))
        {
            return;
        }

        // The randomized AP item replaces the
        // vanilla Destiny Inscription reward title.
        if (__instance.tooltipNameText != null)
        {
            __instance.tooltipNameText.text =
                itemName;
        }

        // Use the same description presentation
        // as randomized Sephirite rewards.
        if (__instance.tooltipDescriptionText != null)
        {
            __instance.tooltipDescriptionText.text =
                itemDescription;
        }

        // Do not show a vanilla stat/reward effect
        // for an AP check.
        if (__instance.addedEffectText != null)
        {
            __instance.addedEffectText
                .gameObject
                .SetActive(false);
        }

        // -----------------------------------------
        // CLAIM STATE
        // -----------------------------------------

        int purchasedCount =
            SaveManager.Current.GetInt(
                $"TreeShopItem_{item.connected.id}_PurchasedCount",
                0
            );

        bool fullyClaimed =
            purchasedCount >=
            item.connected.maxQuantity;

        if (__instance.tooltipIconImage == null)
            return;

        // Vanilla HandleItemSelected() has just restored
        // this icon's normal size, so remember that size
        // before replacing the sprite.
        Vector2 normalIconSize =
            __instance.tooltipIconImage
                .rectTransform.sizeDelta;

        if (!fullyClaimed)
        {
            // A null sprite by itself renders as a white
            // rectangle, so hide the Image object entirely.
            __instance.tooltipIconImage
                .gameObject.SetActive(false);

            return;
        }

        UnityEngine.Sprite apLogo =
            SephiriaMod.GetArchipelagoSephiriteSprite(
                item.connected.icon
            );

        if (apLogo == null)
            return;

        __instance.tooltipIconImage
            .gameObject.SetActive(true);

        __instance.tooltipIconImage.sprite =
            apLogo;

        __instance.tooltipIconImage.preserveAspect =
            true;

        __instance.tooltipIconImage.material =
            __instance.tooltipIconImage.defaultMaterial;

        // Same smaller presentation we used for
        // the AP logo inside the tree nodes.
        __instance.tooltipIconImage
            .rectTransform.sizeDelta =
                normalIconSize * 0.65f;
    }
}

[HarmonyPatch(
    typeof(UI_TreeShopPurchasePanel),
    nameof(UI_TreeShopPurchasePanel.Open),
    new Type[] { typeof(TreeShopItemEntity) }
)]
public static class ArchipelagoTreeShopPurchasePopupPatch
{
    public static bool Prefix(
        TreeShopItemEntity item)
    {
        // Normal saves keep Sephiria's vanilla popup.
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        // Never show Destiny Inscription unlock
        // popups on an Archipelago save.
        return false;
    }
}

// popup suppression here??

[HarmonyPatch]
public static class ArchipelagoTreeShopPreviewPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(UI_UnlockItemPreviewer)
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly
            )
            .Where(method => method.Name == "Open");
    }

    public static bool Prefix()
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        if (!ArchipelagoTreeShopModeController
                .IsTreeShopOpen)
        {
            return true;
        }

        // Block preview ONLY on the randomized AP Tree.
        if (ArchipelagoTreeShopModeController
                .CurrentMode ==
            ArchipelagoTreeShopModeController
                .TreeMode.ArchipelagoTree)
        {
            return false;
        }

        // Received Upgrades keeps vanilla preview.
        return true;
    }
}

[HarmonyPatch]
public static class ArchipelagoAnvilFloorGenerationPatch
{
    public sealed class AnvilGenerationState
    {
        public object Instance;

        public FieldInfo MainEventTypesField;
        public FieldInfo SubEventTypesField;

        public EFloorMainEventType[] MainEventTypes;
        public EFloorMainEventType[] SubEventTypes;
    }

    public static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(StageEntity)
            .Assembly
            .GetTypes()
            .Where(type =>
                typeof(StageEntity).IsAssignableFrom(type) &&
                type != typeof(StageEntity))
            .Select(type =>
                type.GetMethod(
                    "GenerateStage",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly
                ))
            .Where(method => method != null)
            .Cast<MethodBase>();
    }

    public static void Prefix(
        object __instance,
        DungeonManager dungeon,
        ref bool createAnvil,
        out AnvilGenerationState __state)
    {
        __state = null;

        if (!SephiriaMod.IsArchipelagoSave)
            return;

        int unlockedAnvils = SephiriaMod.GetProgressiveAnvilUpgradeCount();
        int usedAnvils = SephiriaMod.GetArchipelagoAnvilUsesThisRun();

        int remainingAnvils =
            Math.Max(
                unlockedAnvils - usedAnvils,
                0
            );

        // For StageEntity implementations that actually use
        // the vanilla createAnvil argument.
        createAnvil = remainingAnvils > 0;


        Type stageType = __instance.GetType();

        FieldInfo mainEventTypesField =
            GetFieldQuietly(
                stageType,
                "mainEventTypes"
            );

        FieldInfo subEventTypesField =
            GetFieldQuietly(
                stageType,
                "subEventTypes"
            );

        EFloorMainEventType[] originalMainEventTypes = null;
        EFloorMainEventType[] originalSubEventTypes = null;

        EFloorMainEventType[] newMainEventTypes = null;
        EFloorMainEventType[] newSubEventTypes = null;


        if (mainEventTypesField != null &&
            mainEventTypesField.FieldType ==
                typeof(EFloorMainEventType[]))
        {
            originalMainEventTypes =
                mainEventTypesField.GetValue(__instance)
                    as EFloorMainEventType[];

            if (originalMainEventTypes != null)
            {
                newMainEventTypes =
                    (EFloorMainEventType[])
                        originalMainEventTypes.Clone();
            }
        }


        if (subEventTypesField != null &&
            subEventTypesField.FieldType ==
                typeof(EFloorMainEventType[]))
        {
            originalSubEventTypes =
                subEventTypesField.GetValue(__instance)
                    as EFloorMainEventType[];

            if (originalSubEventTypes != null)
            {
                newSubEventTypes =
                    (EFloorMainEventType[])
                        originalSubEventTypes.Clone();
            }
        }


        if (newMainEventTypes == null &&
            newSubEventTypes == null)
        {
            return;
        }


        __state = new AnvilGenerationState
        {
            Instance = __instance,

            MainEventTypesField =
                mainEventTypesField,

            SubEventTypesField =
                subEventTypesField,

            MainEventTypes =
                originalMainEventTypes,

            SubEventTypes =
                originalSubEventTypes
        };


        LimitAnvils(
            newMainEventTypes,
            ref remainingAnvils
        );

        LimitAnvils(
            newSubEventTypes,
            ref remainingAnvils
        );


        if (newMainEventTypes != null)
        {
            mainEventTypesField.SetValue(
                __instance,
                newMainEventTypes
            );
        }

        if (newSubEventTypes != null)
        {
            subEventTypesField.SetValue(
                __instance,
                newSubEventTypes
            );
        }
    }

    private static void LimitAnvils(
        EFloorMainEventType[] eventTypes,
        ref int remainingAnvils)
    {
        if (eventTypes == null)
            return;

        for (int i = 0;
            i < eventTypes.Length;
            i++)
        {
            if (eventTypes[i] !=
                EFloorMainEventType.Anvil)
            {
                continue;
            }

            if (remainingAnvils > 0)
            {
                remainingAnvils--;
                continue;
            }

            eventTypes[i] =
                EFloorMainEventType.Charm;
        }
    }

    private static FieldInfo GetFieldQuietly(
    Type type,
    string fieldName)
    {
        while (type != null)
        {
            FieldInfo field =
                type.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly
                );

            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }

    public static void Postfix(
        AnvilGenerationState __state)
    {
        if (__state == null ||
            __state.Instance == null)
        {
            return;
        }


        if (__state.MainEventTypesField != null &&
            __state.MainEventTypes != null)
        {
            __state.MainEventTypesField.SetValue(
                __state.Instance,
                __state.MainEventTypes
            );
        }


        if (__state.SubEventTypesField != null &&
            __state.SubEventTypes != null)
        {
            __state.SubEventTypesField.SetValue(
                __state.Instance,
                __state.SubEventTypes
            );
        }
    }
}

[HarmonyPatch(
    typeof(Anvil),
    "Ckeck"
)]
public static class ArchipelagoRunAnvilInteractablePatch
{
    public static void Postfix(
        ref bool __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!__result)
            return;

        __result =
            SephiriaMod.CanUseArchipelagoRunAnvil();
    }
}

[HarmonyPatch(
    typeof(UI_WeaponEnhancementPanel),
    nameof(UI_WeaponEnhancementPanel.Open),
    new Type[]
    {
        typeof(Anvil),
        typeof(IAnvilSound)
    }
)]
public static class ArchipelagoRunAnvilPanelPatch
{
    public static bool Prefix(
        Anvil anvil)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        // Training Grounds passes null.
        // Never restrict the Training Grounds Anvil.
        if (anvil == null)
            return true;

        return SephiriaMod
            .CanUseArchipelagoRunAnvil();
    }
}

[HarmonyPatch(
    typeof(Anvil),
    nameof(Anvil.EnhanceClient)
)]
public static class ArchipelagoRunAnvilUsePatch
{
    public static void Prefix(
        bool ___optimisticEnhanced,
        out bool __state)
    {
        __state =
            SephiriaMod.IsArchipelagoSave &&
            !___optimisticEnhanced;
    }


    public static void Postfix(
        bool __state)
    {
        if (!__state)
            return;

        SephiriaMod.RecordArchipelagoRunAnvilUse();
    }
}

[HarmonyPatch(
    typeof(PlayerSpawner),
    nameof(PlayerSpawner.CmdSetSapphireInRun)
)]
public static class ArchipelagoSapphireMultiplierPatch
{
    public static void Prefix(
        PlayerSpawner __instance,
        ref int amount)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (__instance == null)
            return;

        int multiplier =
            SephiriaMod.SapphireMultiplier;

        if (multiplier <= 1)
            return;

        int currentAmount =
            __instance.sapphireInRun;

        // CmdSetSapphireInRun receives the NEW TOTAL,
        // not the amount gained.
        //
        // Only multiply positive gains. Decreases/resets
        // should retain vanilla behavior.
        if (amount <= currentAmount)
            return;

        int gainedAmount =
            amount - currentAmount;

        amount =
            currentAmount +
            (gainedAmount * multiplier);
    }
}

[HarmonyPatch(
    typeof(UI_GameOverLabel),
    nameof(UI_GameOverLabel.CalculateRunEarnedSapphire)
)]
public static class ArchipelagoGameOverSapphireMultiplierPatch
{
    public static void Postfix(
        LevelController levelController,
        PlayerSpawner playerSpawner,
        PlayerAvatar playerAvatar,
        ref int __result)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        int multiplier =
            SephiriaMod.SapphireMultiplier;

        if (multiplier <= 1)
            return;

        if (levelController == null ||
            playerSpawner == null)
        {
            return;
        }

        // sapphireInRun has already been multiplied
        // when it was collected.
        //
        // The only unmultiplied natural Sapphire
        // remaining here is the level-based reward.
        int levelSapphire =
            Math.Max(
                0,
                levelController.currentLevel - 1
            );

        int multipliedBase =
            playerSpawner.sapphireInRun +
            (levelSapphire * multiplier);

        // Reapply Sephiria's normal Sapphire Bonus
        // percentage to the correctly multiplied base.
        if (playerAvatar != null)
        {
            int sapphireBonus =
                playerAvatar.GetCustomStatUnsafe(
                    "SAPPHIREBONUS"
                );

            if (sapphireBonus > 0)
            {
                multipliedBase +=
                    Mathf.CeilToInt(
                        (float)(
                            multipliedBase *
                            sapphireBonus
                        ) /
                        100f
                    );
            }
        }

        __result = multipliedBase;
    }
}

[HarmonyPatch(
    typeof(PlayerSpawner),
    "HandleDieClientside"
)]
public static class ArchipelagoDeathLinkPlayerDeathPatch
{
    public static void Postfix(
        PlayerSpawner __instance)
    {
        SephiriaMod
            .HandleLocalPlayerDeathForDeathLink(
                __instance
            );
    }
}

[HarmonyPatch(
    typeof(PlayerAvatar),
    nameof(PlayerAvatar.AttackButtonDown)
)]
public static class ArchipelagoNormalAttackTrapPatch
{
    public static bool Prefix(
        PlayerAvatar __instance)
    {
        if (!SephiriaMod
                .IsCantNormalAttackTrapActive)
        {
            return true;
        }

        if (CombatManager.Instance == null ||
            CombatManager.Instance.CurrentPlayer !=
                __instance)
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(
    typeof(PlayerLocalDataStorage),
    nameof(PlayerLocalDataStorage.Move)
)]
public static class ArchipelagoFlippedControlsTrapPatch
{
    public static void Prefix(
        PlayerLocalDataStorage __instance,
        ref Vector2 dir)
    {
        if (!SephiriaMod.IsFlippedControlsTrapActive)
            return;

        if (__instance == null ||
            __instance.Avatar == null)
        {
            return;
        }

        if (CombatManager.Instance == null ||
            CombatManager.Instance.CurrentPlayer !=
                __instance.Avatar)
        {
            return;
        }

        dir = -dir;
    }
}

[HarmonyPatch(
    typeof(CharacterDash),
    nameof(CharacterDash.StartDash)
)]
public static class ArchipelagoCantDashTrapPatch
{
    public static bool Prefix(
        CharacterDash __instance)
    {
        if (!SephiriaMod.IsCantDashTrapActive)
            return true;

        if (__instance == null ||
            __instance.UnitAvatar == null)
        {
            return true;
        }

        PlayerAvatar player =
            __instance.UnitAvatar as PlayerAvatar;

        if (player == null)
            return true;

        if (CombatManager.Instance == null ||
            CombatManager.Instance.CurrentPlayer != player)
        {
            return true;
        }

        // Trap is active on the local player.
        // Completely prevent StartDash().
        return false;
    }
}

[HarmonyPatch(
    typeof(IntegratedActionController),
    nameof(IntegratedActionController.Cast)
)]
public static class ArchipelagoCantCastTrapPatch
{
    public static bool Prefix(
        IntegratedActionController __instance,
        int idx)
    {
        if (!SephiriaMod.IsCantCastTrapActive)
            return true;

        if (__instance == null)
            return true;

        PlayerAvatar player =
            __instance.GetComponent<PlayerAvatar>();

        if (player == null)
            return true;

        if (CombatManager.Instance == null ||
            CombatManager.Instance.CurrentPlayer != player)
        {
            return true;
        }

        QuickSlotData slot = null;

        // Normal quick slots: 0 - 7
        if (
            idx >= 0 &&
            idx < __instance.quickSlots.Length
        )
        {
            slot = __instance.quickSlots[idx];
        }

        // Weapon-bound quick slots: 100 - 102
        else if (
            idx >= 100 &&
            idx <= 102
        )
        {
            slot =
                __instance.quickSlotsWeapon[
                    idx - 100
                ];
        }

        if (slot == null)
            return true;

        // Only block actual magic.
        if (slot.Type == QuickSlotType.Magic)
            return false;

        return true;
    }
}

[HarmonyPatch(
    typeof(UI_SignMessage),
    nameof(UI_SignMessage.Open)
)]
public static class ArchipelagoGoalSignPatch
{
    public static void Prefix(
        ref string message)
    {
        if (!SephiriaMod.IsArchipelagoSave)
            return;

        if (!string.Equals(
                message,
                "My Home",
                StringComparison.Ordinal))
        {
            return;
        }

        int clears =
            SephiriaMod.RequiredChapterClears;

        string runText =
            clears == 1
                ? "run"
                : "runs";

        if (SephiriaMod.UniqueWeaponsRequired)
        {
            message =
                "Win " +
                clears +
                " " +
                runText +
                " with unique weapons";
        }
        else
        {
            message =
                "Win " +
                clears +
                " " +
                runText;
        }
    }
}