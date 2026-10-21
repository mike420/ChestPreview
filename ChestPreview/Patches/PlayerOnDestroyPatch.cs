using HarmonyLib;

namespace ChestPreview.Patches;

[HarmonyPatch(typeof(Player), "OnDestroy")]
internal static class PlayerOnDestroyPatch
{
	private static void Prefix(Player __instance)
	{
		if (__instance == Player.m_localPlayer)
		{
			ChestTarget.Clear();
		}
	}
}
