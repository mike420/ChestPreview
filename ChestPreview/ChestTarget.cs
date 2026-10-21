using System;
using HarmonyLib;
using UnityEngine;

namespace ChestPreview;

internal static class ChestTarget
{
	// Container.m_nview, m_piece and CheckAccess are private in Valheim; the runtime blocks direct access.
	internal static readonly AccessTools.FieldRef<Container, ZNetView> ContainerNView = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

	internal static readonly AccessTools.FieldRef<Container, Piece> ContainerPiece = AccessTools.FieldRefAccess<Container, Piece>("m_piece");

	private static readonly Func<Container, long, bool> ContainerCheckAccess = AccessTools.MethodDelegate<Func<Container, long, bool>>(AccessTools.Method(typeof(Container), "CheckAccess", new Type[1] { typeof(long) }));

	internal static Container Current { get; private set; }

	internal static void SetFromHoverObject(GameObject hover)
	{
		Current = ResolveContainer(hover);
	}

	internal static void Clear()
	{
		Current = null;
	}

	internal static Container ResolveContainer(GameObject hover)
	{
		if (hover == null)
		{
			return null;
		}
		Container componentInParent = hover.GetComponentInParent<Container>();
		if (componentInParent == null || !IsValidContainer(componentInParent))
		{
			return null;
		}
		if (!ShouldShow(componentInParent))
		{
			return null;
		}
		return componentInParent;
	}

	internal static bool IsValidContainer(Container container)
	{
		if (container == null)
		{
			return false;
		}
		ZNetView nview = ContainerNView(container);
		if (nview != null && !nview.IsValid())
		{
			return false;
		}
		return container.GetInventory() != null;
	}

	internal static bool IsPlayerPlaced(Container container)
	{
		if (container == null)
		{
			return false;
		}
		Piece piece = ContainerPiece(container);
		if (piece == null)
		{
			piece = container.GetComponent<Piece>();
		}
		if (piece == null)
		{
			piece = container.GetComponentInParent<Piece>();
		}
		if (piece != null)
		{
			return piece.IsPlacedByPlayer();
		}
		return false;
	}

	internal static bool ShouldShow(Container container)
	{
		return IsPlayerPlaced(container);
	}

	internal static bool CanPreview(Container container, Player player)
	{
		if (container == null || player == null)
		{
			return false;
		}
		if (!IsValidContainer(container))
		{
			return false;
		}
		if (!ShouldShow(container))
		{
			return false;
		}
		try
		{
			if (!ContainerCheckAccess(container, player.GetPlayerID()))
			{
				return false;
			}
		}
		catch
		{
			return false;
		}
		return true;
	}
}
