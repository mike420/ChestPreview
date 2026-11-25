using BepInEx;
using BepInEx.Logging;
using ChestPreview.UI;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace ChestPreview
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.Major)]
    internal class ChestPreviewMod : BaseUnityPlugin
    {
        internal const string PluginName = "ChestPreview";
        internal const string PluginVersion = "1.1.0";
        internal const string PluginAuthor = "MagicMike";
        internal const string PluginGUID = "MagicMike.ChestPreview";
        internal const string PluginLink = "https://discord.gg/mnr36G6J4x";

        internal static ChestPreviewMod Instance;

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private GameObject _overlayRoot;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ChestListConfig.Init(Config);
            MagicMike.ModSettings.ModSettings.Init(this);
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();
            _overlayRoot = new GameObject("ChestPreview_Overlay");
            _overlayRoot.AddComponent<ChestListOverlay>();
            Object.DontDestroyOnLoad(_overlayRoot);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            if (_overlayRoot != null)
            {
                Object.Destroy(_overlayRoot);
                _overlayRoot = null;
            }
            Instance = null;
        }
    }
}
