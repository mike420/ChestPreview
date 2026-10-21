using HarmonyLib;

namespace ChestPreview.Patches;

[HarmonyPatch(typeof(Player), "UpdateHover")]
internal static class PlayerUpdateHoverPatch
{
	private static void Postfix(Player __instance)
	{
		if (!(__instance != Player.m_localPlayer))
		{
			ChestTarget.SetFromHoverObject(__instance.GetHoverObject());
		}
	}
}
