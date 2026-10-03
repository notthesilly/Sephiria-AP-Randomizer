using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using MelonLoader;
using Mirror;
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
        public static string StartingCostumeId { get; private set; }
        public static int CompletedChapterClears { get; private set; }
        public static bool Chapter2EndingReached { get; set; }
        public static bool ArchipelagoGoalPending { get; set; }

        private static bool hardModeSyncPending;
        private static bool permanentItemSyncPending;

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

        private static readonly HashSet<int> activeArchipelagoSephiriteRuntimeInstances = new HashSet<int>();
        private static readonly HashSet<int> blockedArchipelagoSephiriteRuntimeInstances = new HashSet<int>();
        private static readonly Dictionary<int, long> archipelagoSephiriteLocationByRewardInstanceId = new Dictionary<int, long>();
        private static readonly Dictionary<int, string> archipelagoSephiriteLocationNameByRewardInstanceId = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> archipelagoSephiriteRuntimeIdentities = new Dictionary<int, string>();
        private sealed class ArchipelagoScoutedItemData{ public string ItemName; public string PlayerName; public string GameName; public int Flags; }
        private static readonly Dictionary<long, ArchipelagoScoutedItemData> archipelagoSephiriteScoutedItems = new Dictionary<long, ArchipelagoScoutedItemData>();
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
                    { "Crocodile", 2009 },
                    { "Deer", 2010 },
                    { "Luck", 2011 },
                    { "Squirrel", 2012 },
                    { "Turtle", 2013 },
                    { "ForestCat", 2014 },
                    { "Ghost", 2015 },
                    { "Seren", 2016 },
                    { "Skeleton", 2017 },
                    { "Bat", 2018 }
                };
        private static readonly FieldInfo sephiriteRerollButtonGroupField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "rerollButtonGroup");
        private static readonly FieldInfo sephiriteSkipSelectButtonGroupField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "skipSelectButtonGroup");
        private static readonly FieldInfo sephiriteConvertRerollDiceButtonGroupField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "convertRerollDiceButtonGroup");
        private static readonly FieldInfo sephiriteSelectSoundField = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "selectSound");
        private static readonly System.Random archipelagoShrineCheckRandom = new System.Random();
        private static int hiddenRoomRewardAttemptsThisRun;
        private static UnityEngine.Sprite archipelagoShrineSprite;
        private static UnityEngine.Sprite archipelagoSephiriteSprite;
        private static UnityEngine.Sprite archipelagoSephiriteOutlineSprite;
        private static UnityEngine.Sprite archipelagoSaveProfileSprite;
        private static UnityEngine.Sprite archipelagoSaveProfileOutlineSprite;
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

            MelonLogger.Msg(
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

            MelonLogger.Msg(
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

            MelonLogger.Msg(
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

            MelonLogger.Msg(
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

                        Flags = Convert.ToInt32(
                            scoutedItem.Flags
                        )
                    };
                }

                MelonLogger.Msg(
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
            else if ((itemData.Flags & 2) != 0)
            {
                rewardElement.bgImage.sprite =
                    rewardElement.rareBGSprite;

                rewardElement.isGoodItem = true;
            }
            else if ((itemData.Flags & 4) != 0)
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
            else if ((itemData.Flags & 2) != 0)
            {
                rarity = EItemRarity.Rare;
            }
            else if ((itemData.Flags & 4) != 0)
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

        private static void LogArchipelagoHardModeRuntimeState()
        {
            if (!IsArchipelagoSave)
                return;

            if (DungeonManager.Instance == null)
            {
                MelonLogger.Msg(
                    "[AP Hard Mode Runtime] DungeonManager is null."
                );

                return;
            }

            foreach (KeyValuePair<string, int> shard
                in hardModeMaxLevelsByShardKey)
            {
                string shardKey = shard.Key;

                int savedLevel =
                    SaveManager.Current.GetInt(
                        "HardModeShard_" +
                        shardKey +
                        "_Level",
                        -999
                    );

                int runtimeLevel = -999;
                int runtimeEnvironment = -999;

                bool hasRuntimeLevel =
                    DungeonManager.Instance
                        .hardModeShardLevels
                        .TryGetValue(
                            shardKey,
                            out runtimeLevel
                        );

                bool hasRuntimeEnvironment =
                    DungeonManager.Instance
                        .hardModeEnvironment
                        .TryGetValue(
                            shardKey,
                            out runtimeEnvironment
                        );

                MelonLogger.Msg(
                    "[AP Hard Mode Runtime] " +
                    shardKey +
                    " | SavedLevel=" +
                    savedLevel +
                    " | RuntimeLevel=" +
                    (hasRuntimeLevel
                        ? runtimeLevel.ToString()
                        : "MISSING") +
                    " | Environment=" +
                    (hasRuntimeEnvironment
                        ? runtimeEnvironment.ToString()
                        : "MISSING")
                                    );
            }
        }

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
            StartingCostumeId = null;
            CompletedChapterClears = 0;

            Chapter2EndingReached = false;
            ArchipelagoGoalPending = false;

            completedWeaponTypes.Clear();

            archipelagoSephiriteRuntimeIdentities.Clear();
            activeArchipelagoSephiriteRuntimeInstances.Clear();
            blockedArchipelagoSephiriteRuntimeInstances.Clear();
            archipelagoSephiriteLocationByRewardInstanceId.Clear();
            archipelagoSephiriteLocationNameByRewardInstanceId.Clear();
            archipelagoSephiriteScoutedItems.Clear();

            ResetArchipelagoShrineRunState();

            hardModeSyncPending = false;
            permanentItemSyncPending = false;

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
                QueuePermanentItemSync();
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

                StartingCostumeId =
                    Convert.ToString(
                        loginSuccess.SlotData["starting_costume"]
                    );

                MelonLogger.Msg(
                    $"AP Starting Costume: {StartingCostumeId}"
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

                await ScoutArchipelagoSephiriteItems(apSession);

                QueueHardModeSync();
                QueuePermanentItemSync();

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

            if (permanentItemSyncPending &&
                SaveManager.Current != null)
            {
                permanentItemSyncPending = false;

                ApplyArchipelagoPermanentItemState();
            }

            if (!hardModeSyncPending)
                return;

            hardModeSyncPending = false;

            ApplyArchipelagoHardModeState();

            LogArchipelagoHardModeRuntimeState();
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

            ArchipelagoSession apSession =
                session as ArchipelagoSession;

            if (apSession == null)
                return;

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

                        MelonLogger.Msg(
                            $"[AP Costume] {costumeId} = True"
                        );
                    }
                    else if (!shouldBeUnlocked &&
                             currentlyUnlocked)
                    {
                        CostumeDatabase.LockCostume(
                            costumeId
                        );

                        MelonLogger.Msg(
                            $"[AP Costume] {costumeId} = False"
                        );
                    }
                }
            }

            // ---------------------------------------------
            // Make sure an AP save isn't configured to use
            // a costume it does not own.
            // ---------------------------------------------

            bool saveChanged = false;

            string equippedCostume =
                SaveManager.Current.GetString(
                    "PlayerCostume",
                    ""
                );

            if (!string.IsNullOrEmpty(StartingCostumeId) &&
                !ownedCostumeIds.Contains(equippedCostume))
            {
                SaveManager.Current.SetString(
                    "PlayerCostume",
                    StartingCostumeId
                );

                saveChanged = true;

                MelonLogger.Msg(
                    $"[AP Costume] Equipped starting costume: " +
                    $"{StartingCostumeId}"
                );
            }

            if (saveChanged)
            {
                SaveManager.Save(
                    saveCurrent: true,
                    saveCurrentRun: false
                );
            }

            MelonLogger.Msg(
                $"[AP Costume] Ownership synchronized. " +
                $"Owned={ownedCostumeIds.Count}, " +
                $"Starting={StartingCostumeId}, " +
                $"RuntimeReady={CombatManager.Instance != null}"
            );
        }

        public static void PrepareArchipelagoCostumeMissionTrackers()
        {
            if (!IsArchipelagoSave)
                return;

            if (SaveManager.Current == null)
                return;

            foreach (string costumeId
                in archipelagoCostumeMissionLocations.Keys)
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
                         $"[DEBUG] Updated AP save settings: " +
                         $"{hostValue}:{port}, Slot={slotName}"
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

            MelonLogger.Msg(
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

        MelonLogger.Msg(
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

        MelonLogger.Msg(
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
            "TabletMixTutorial",
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

        if (SephiriaMod.WasArchipelagoSephiriteClaimed( __instance))
        {
            MelonLogger.Msg(
                $"[AP Sephirite] Eligible " +
                $"#{eligibleNumber} was already resolved. " +
                "It will be consumed without another reward."
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
            $"NormalChoices={normalChoiceCount}, " +
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

        LevelController levelController =
            __instance.GetComponent<LevelController>();

        if (levelController == null ||
            levelController.levelUpQueue == null ||
            levelController.levelUpQueue.Count == 0)
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

        bool protectedTransition =
            currentStageProgress == 1 ||
            currentStageProgress == 2 ||
            currentStageProgress == 3 ||
            currentStageProgress == 4 ||
            currentStageProgress == 6;

        if (!protectedTransition)
            return true;

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
    typeof(CostumeUnlockEntity),
    nameof(CostumeUnlockEntity.Unlock)
)]
public static class ArchipelagoCostumeVanillaUnlockPatch
{
    public static bool Prefix(
        CostumeUnlockEntity __instance
    )
    {
        // Normal saves retain completely vanilla
        // costume behavior.
        if (!SephiriaMod.IsArchipelagoSave)
            return true;

        // On AP saves, costume missions must not
        // directly award their vanilla costumes.
        //
        // The mission will become an AP location check
        // in the next step.
        return false;
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

        // Fairy of Fortune is special in AP.
        //
        // Vanilla unlocks it from Chapter 4 completion, but our
        // AP mission is "clear the game on any chapter".
        // Therefore the vanilla Luck trigger must NEVER send
        // the AP location.
        if (costumeId == "Luck")
            return false;

        SephiriaMod.TryCompleteArchipelagoCostumeMission(
            costumeId
        );

        // Never grant the vanilla costume.
        return false;
    }
}