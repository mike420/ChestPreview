using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Extensions;
using System.Collections.Generic;

namespace ChestPreview;

internal enum ChestSortType
{
	Name,
	Value,
	Weight
}

internal static class ChestListConfig
{
	internal static ConfigEntry<int> MaxEntries;

	internal static ConfigEntry<ChestSortType> SortType;

	internal static ConfigEntry<bool> SortAscending;

	internal static ConfigEntry<bool> CombineStacks;

	internal static ConfigEntry<string> CapacityFormat;

	internal static ConfigEntry<float> UiPositionX;

	internal static ConfigEntry<float> UiPositionY;

	internal static ConfigEntry<float> UiAlpha;

	internal static ConfigEntry<float> BackgroundAlpha;

	internal static ConfigEntry<bool> ShowItemImages;

	internal static ConfigEntry<float> MinPanelWidth;

	internal static ConfigEntry<float> MaxPanelWidth;

	internal static void Init(ConfigFile config)
	{
		MaxEntries = config.BindConfigInOrder("General", "MaxEntries", -1, "Maximum number of rows to show. -1 = unlimited.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<int>(-1, 1000));
		SortType = config.BindConfigInOrder("General", "SortType", ChestSortType.Value, "How rows are sorted: Name, Value (total value of the row) or Weight (total weight of the row). Ties are sorted by name.", synced: false, sectionOrder: true, settingOrder: true);
		SortAscending = config.BindConfigInOrder("General", "SortAscending", false, "When enabled, sort lowest-to-highest (A-Z for Name). When disabled, sort highest-to-lowest (Z-A for Name).", synced: false, sectionOrder: true, settingOrder: true);
		CombineStacks = config.BindConfigInOrder("General", "CombineStacks", true, "When enabled, merge identical items into one row with a total count (same name/world level; quality only when the item supports tiers; variant only when it has multiple variants). When disabled, each inventory stack is listed separately. On by default.", synced: false, sectionOrder: true, settingOrder: true);
		CapacityFormat = config.BindConfigInOrder("UI", "CapacityFormat", "{0}/{1}", "Capacity text shown next to the chest name. {0} = slots used, {1} = total slots. Leave empty to hide.", synced: false, sectionOrder: true, settingOrder: true);
		UiPositionX = config.BindConfigInOrder("UI", "PositionX", 0.72f, "Horizontal screen position of the overlay (0 = left edge, 1 = right edge). Anchor is the panel center.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 1f));
		UiPositionY = config.BindConfigInOrder("UI", "PositionY", 0.55f, "Vertical screen position of the overlay (0 = bottom edge, 1 = top edge). Anchor is the panel center.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 1f));
		UiAlpha = config.BindConfigInOrder("UI", "Alpha", 0.92f, "Overlay transparency (0 = invisible, 1 = fully opaque).", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 1f));
		BackgroundAlpha = config.BindConfigInOrder("UI", "BackgroundAlpha", 0.66f, "Opacity of the black panel background behind the text (0 = no background, 1 = solid black).", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 1f));
		ShowItemImages = config.BindConfigInOrder("UI", "ShowItemImages", true, "When enabled, show item icons to the left of each name/count. On by default.", synced: false, sectionOrder: true, settingOrder: true);
		MinPanelWidth = config.BindConfigInOrder("UI", "MinPanelWidth", 300f, "Minimum overlay panel width in UI pixels. Panel grows with the longest row up to MaxPanelWidth.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(200f, 600f));
		MaxPanelWidth = config.BindConfigInOrder("UI", "MaxPanelWidth", 470f, "Maximum overlay panel width in UI pixels. Longer item names are clipped beyond this.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(250f, 800f));

        RemoveOrphanedEntries(config);

        // Decimal settings rounded (and kept in range) whichever editor sets them
        MagicMike.ModSettings.ConfigRounding.RoundAll(config);
        MagicMike.ModSettings.ConfigRounding.Round(MinPanelWidth, 0);
        MagicMike.ModSettings.ConfigRounding.Round(MaxPanelWidth, 0);
        MagicMike.ModSettings.ConfigRounding.Round(UiPositionX, 3);
        MagicMike.ModSettings.ConfigRounding.Round(UiPositionY, 3);
    }

    private static void RemoveOrphanedEntries(ConfigFile config)
    {
        Dictionary<ConfigDefinition, string> orphaned = Traverse.Create(config).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>();
        if (orphaned == null || orphaned.Count == 0)
        {
            return;
        }
        Jotunn.Logger.LogInfo("[ChestPreview] Removing " + orphaned.Count + " old setting(s) from the config file");
        orphaned.Clear();
    }
}
