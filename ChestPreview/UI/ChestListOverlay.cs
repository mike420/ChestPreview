using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChestPreview.UI;

internal sealed class ChestListOverlay : MonoBehaviour
{
	private sealed class DisplayEntry
	{
		public ItemDrop.ItemData Item;

		public int Stack;

		public int Order;

		public string SortName;

		public int HiddenCount;
	}

	private sealed class RowWidgets
	{
		public GameObject Root;

		public RectTransform Rect;

		public LayoutElement Layout;

		public Image Icon;

		public TextMeshProUGUI Label;
	}

	private readonly struct InvisibleMeasureScope(ChestListOverlay owner, bool restoreAlpha, float previousAlpha) : IDisposable
	{
		private readonly ChestListOverlay _owner = owner;

		private readonly bool _restoreAlpha = restoreAlpha;

		private readonly float _previousAlpha = previousAlpha;

		public void Dispose()
		{
			if (!(_owner == null) && _restoreAlpha && Alive(_owner._canvasGroup))
			{
				_owner._canvasGroup.alpha = _previousAlpha;
			}
		}
	}

	private const float DefaultMinPanelWidth = 300f;

	private const float DefaultMaxPanelWidth = 470f;

	private const float ContentRightPad = 28f;

	private const float TitleHeight = 34f;

	private const float TitleGap = 4f;

	private const float RowHeight = 31f;

	private const float MinRowHeight = 22f;

	private const float IconSize = 27f;

	private const float MinIconSize = 20f;

	private const float TitleFontSize = 25f;

	private const float ItemFontSize = 21f;

	private const float MinItemFontSize = 16f;

	private const float MinPad = 16f;

	private const float InnerPad = 4f;

	private const float ColumnGap = 14f;

	private const float ScreenEdgeMargin = 36f;

	private const float HudTopReserve = 88f;

	private const float HudBottomReserve = 100f;

	private const float ComfortableHeightFraction = 0.55f;

	private const int MaxColumns = 3;

	private Canvas _canvas;

	private CanvasGroup _canvasGroup;

	private RectTransform _panel;

	private Image _panelImage;

	private RectTransform _content;

	private RectTransform _titleRt;

	private TextMeshProUGUI _title;

	private RectTransform _listArea;

	private RectTransform _listRoot;

	private GridLayoutGroup _listGrid;

	private readonly List<RowWidgets> _rows = new List<RowWidgets>(64);

	private readonly List<DisplayEntry> _displayEntries = new List<DisplayEntry>(64);

	private readonly Dictionary<string, int> _mergeIndexByKey = new Dictionary<string, int>(64);

	private readonly StringBuilder _labelBuilder = new StringBuilder(64);

	private readonly StringBuilder _keyBuilder = new StringBuilder(64);

	private Container _boundContainer;

	private int _boundSignature;

	private bool _hasBoundSignature;

	private float _boundAvailableHeight = -1f;

	private bool _boundShowIcons;

	private bool _boundCombineStacks;

	private float _boundMinPanelWidth = -1f;

	private float _boundMaxPanelWidth = -1f;

	private ChestSortType _boundSortType;

	private bool _boundSortAscending;

	private int _boundMaxEntries = int.MinValue;

	private string _boundCapacityFormat;

	private bool _uiBuilt;

	private float _panelWidth = 300f;

	private float _listContentHeight;

	private int _layoutColumns = 1;

	private int _layoutRowsPerColumn = 1;

	private float _layoutRowHeight = 31f;

	private float _layoutIconSize = 27f;

	private float _layoutItemFontSize = 21f;

	private float _padL = 12f;

	private float _padR = 12f;

	private float _padT = 8f;

	private float _padB = 10f;

	private static TMP_FontAsset _cachedFont;

	private static Material _cachedFontMaterial;

	private static float _cachedFontSize = 16f;

	private static Color _cachedFontColor = new Color(1f, 0.984f, 0.875f, 1f);

	private void Update()
	{
		try
		{
			Tick();
		}
		catch (Exception arg)
		{
			ChestPreviewMod.Log?.LogError($"ChestListOverlay tick failed: {arg}");
			SafeHide();
		}
	}

	private void Tick()
	{
		if (!_uiBuilt)
		{
			if (!TryResolveGameFont(out var _, out var material, out var _, out var color))
			{
				return;
			}
			BuildUi();
			_uiBuilt = true;
			SafeHide();
		}
		if (!Alive(_canvas) || !Alive(_panel) || !Alive(_canvasGroup))
		{
			_uiBuilt = false;
			return;
		}
		if (!TryGetPreviewTarget(out var container, out var inventory))
		{
			// Keep the bound state so re-hovering an unchanged chest doesn't rebuild the list.
			HidePanel();
			return;
		}
		ApplyLayoutAndAlpha();
		RefreshContents(container, inventory);
		SafeShow();
	}

	private void OnDestroy()
	{
		_rows.Clear();
		_uiBuilt = false;
		_boundContainer = null;
	}

	private static bool TryGetPreviewTarget(out Container container, out Inventory inventory)
	{
		container = null;
		inventory = null;
		Player localPlayer = Player.m_localPlayer;
		if (!Alive(localPlayer))
		{
			return false;
		}
		if (ShouldHideForMenus())
		{
			return false;
		}
		container = ChestTarget.Current;
		if (!ChestTarget.CanPreview(container, localPlayer))
		{
			GameObject gameObject = null;
			try
			{
				gameObject = localPlayer.GetHoverObject();
			}
			catch
			{
				return false;
			}
			container = ChestTarget.ResolveContainer(gameObject);
			if (!ChestTarget.CanPreview(container, localPlayer))
			{
				return false;
			}
		}
		if (!Alive(container))
		{
			return false;
		}
		try
		{
			inventory = container.GetInventory();
		}
		catch
		{
			return false;
		}
		if (inventory == null || inventory.NrOfItems() == 0)
		{
			return false;
		}
		return true;
	}

	private static bool ShouldHideForMenus()
	{
		try
		{
			if (InventoryGui.IsVisible())
			{
				return true;
			}
			if (Menu.IsVisible())
			{
				return true;
			}
			if (Alive(TextViewer.instance) && TextViewer.instance.IsVisible())
			{
				return true;
			}
			if (Console.IsVisible())
			{
				return true;
			}
			if (Alive(Chat.instance) && Chat.instance.HasFocus())
			{
				return true;
			}
			if (StoreGui.IsVisible())
			{
				return true;
			}
		}
		catch
		{
			return true;
		}
		return false;
	}

	private void BuildUi()
	{
		_canvas = base.gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = 80;
		CanvasScaler canvasScaler = base.gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		canvasScaler.matchWidthOrHeight = 0.5f;
		_canvasGroup = base.gameObject.AddComponent<CanvasGroup>();
		_canvasGroup.blocksRaycasts = false;
		_canvasGroup.interactable = false;
		GameObject gameObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		_panel = gameObject.GetComponent<RectTransform>();
		_panel.anchorMin = new Vector2(0.5f, 0.5f);
		_panel.anchorMax = new Vector2(0.5f, 0.5f);
		_panel.pivot = new Vector2(0.5f, 0.5f);
		_panel.sizeDelta = new Vector2(_panelWidth, 120f);
		_panelImage = gameObject.GetComponent<Image>();
		_panelImage.color = GetBackgroundColor();
		_panelImage.raycastTarget = false;
		GameObject gameObject2 = new GameObject("Content", typeof(RectTransform));
		gameObject2.transform.SetParent(_panel, worldPositionStays: false);
		_content = gameObject2.GetComponent<RectTransform>();
		ApplyContentInsets();
		_title = CreateGameTmp("Title", _content, isTitle: true);
		_titleRt = _title.rectTransform;
		_titleRt.anchorMin = new Vector2(0f, 1f);
		_titleRt.anchorMax = new Vector2(1f, 1f);
		_titleRt.pivot = new Vector2(0.5f, 1f);
		_titleRt.anchoredPosition = Vector2.zero;
		_titleRt.sizeDelta = new Vector2(0f, 34f);
		_title.alignment = TextAlignmentOptions.Center;
		_title.raycastTarget = false;
		_title.maskable = false;
		_title.overflowMode = TextOverflowModes.Overflow;
		_title.textWrappingMode = TextWrappingModes.NoWrap;
		_title.enableAutoSizing = false;
		_title.text = "Chest";
		_title.gameObject.SetActive(value: true);
		GameObject gameObject3 = new GameObject("ListArea", typeof(RectTransform));
		gameObject3.transform.SetParent(_content, worldPositionStays: false);
		_listArea = gameObject3.GetComponent<RectTransform>();
		_listArea.anchorMin = Vector2.zero;
		_listArea.anchorMax = Vector2.one;
		_listArea.pivot = new Vector2(0.5f, 1f);
		_listArea.offsetMin = Vector2.zero;
		_listArea.offsetMax = new Vector2(0f, -38f);
		_title.transform.SetAsFirstSibling();
		GameObject gameObject4 = new GameObject("List", typeof(RectTransform), typeof(GridLayoutGroup));
		gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
		_listRoot = gameObject4.GetComponent<RectTransform>();
		_listRoot.anchorMin = new Vector2(0f, 1f);
		_listRoot.anchorMax = new Vector2(1f, 1f);
		_listRoot.pivot = new Vector2(0.5f, 1f);
		_listRoot.anchoredPosition = Vector2.zero;
		_listRoot.sizeDelta = new Vector2(0f, 0f);
		_listGrid = gameObject4.GetComponent<GridLayoutGroup>();
		_listGrid.cellSize = new Vector2(100f, 31f);
		_listGrid.spacing = new Vector2(14f, 0f);
		_listGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
		_listGrid.startAxis = GridLayoutGroup.Axis.Vertical;
		_listGrid.childAlignment = TextAnchor.UpperLeft;
		_listGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		_listGrid.constraintCount = 1;
		_listGrid.padding = new RectOffset(0, 0, 0, 0);
		ApplyPanelSize(_panelWidth, GetMinPanelHeight());
	}

	private float GetTitleBlockHeight()
	{
		return 38f;
	}

	private float GetBorderChromeHeight()
	{
		return _padT + _padB;
	}

	private float GetMinPanelHeight()
	{
		return GetBorderChromeHeight() + GetTitleBlockHeight() + _layoutRowHeight;
	}

	private float GetAvailableScreenHeight()
	{
		float num = Mathf.Max(1f, GetParentCanvasSize().y);
		float configuredPositionY = GetConfiguredPositionY();
		float a = num * (1f - configuredPositionY) - 88f;
		float b = num * configuredPositionY - 100f;
		float num2 = 2f * Mathf.Max(0f, Mathf.Min(a, b));
		float num3 = num * 0.55f;
		float num4 = num - 88f - 100f;
		float b2 = Mathf.Min(num2, num3, num4);
		return Mathf.Max(140f, b2);
	}

	private static float GetConfiguredPositionY()
	{
		try
		{
			if (ChestListConfig.UiPositionY != null)
			{
				return Mathf.Clamp01(ChestListConfig.UiPositionY.Value);
			}
		}
		catch
		{
		}
		return 0.55f;
	}

	private Vector2 GetParentCanvasSize()
	{
		RectTransform rectTransform = (Alive(_panel) ? (_panel.parent as RectTransform) : null);
		Vector2 result = ((rectTransform != null) ? rectTransform.rect.size : Vector2.zero);
		if (result.x <= 1f || result.y <= 1f)
		{
			float num = 1080f;
			float num2 = 1920f;
			float num3 = Mathf.Max(1f, Screen.width);
			float num4 = Mathf.Max(1f, Screen.height);
			float a = Mathf.Log(num3 / num2, 2f);
			float b = Mathf.Log(num4 / num, 2f);
			float p = Mathf.Lerp(a, b, 0.5f);
			float num5 = Mathf.Pow(2f, p);
			result = new Vector2(num3 / num5, num4 / num5);
		}
		return result;
	}

	private void ComputeFitToScreenLayout(int total)
	{
		int value = Mathf.Max(1, total);
		float availableScreenHeight = GetAvailableScreenHeight();
		float num = GetBorderChromeHeight() + GetTitleBlockHeight();
		float num2 = Mathf.Max(22f, availableScreenHeight - num);
		int num3 = 1;
		float num4 = 31f;
		for (int i = 1; i <= 3; i++)
		{
			int num5 = CeilDiv(value, i);
			float num6 = (float)num5 * 31f;
			if (num + num6 <= availableScreenHeight + 0.5f)
			{
				num3 = i;
				num4 = 31f;
				break;
			}
			num3 = i;
			if (i == 3)
			{
				num4 = Mathf.Clamp(num2 / (float)num5, 22f, 31f);
			}
		}
		int num7 = CeilDiv(value, num3);
		float num8 = (float)num7 * num4;
		if (num + num8 > availableScreenHeight + 0.5f)
		{
			num4 = Mathf.Max(22f, num2 / (float)Mathf.Max(1, num7));
		}
		_layoutColumns = num3;
		_layoutRowsPerColumn = num7;
		_layoutRowHeight = num4;
		if (Mathf.Approximately(num4, 31f))
		{
			_layoutIconSize = 27f;
			_layoutItemFontSize = 21f;
		}
		else
		{
			float t = Mathf.InverseLerp(22f, 31f, num4);
			_layoutIconSize = Mathf.Lerp(20f, 27f, t);
			_layoutItemFontSize = Mathf.Lerp(16f, 21f, t);
		}
	}

	private static int CeilDiv(int value, int divisor)
	{
		if (divisor <= 0)
		{
			return value;
		}
		return (value + divisor - 1) / divisor;
	}

	private float GetMinPanelWidth()
	{
		float value = 300f;
		try
		{
			if (ChestListConfig.MinPanelWidth != null)
			{
				value = ChestListConfig.MinPanelWidth.Value;
			}
		}
		catch
		{
		}
		return Mathf.Clamp(value, 200f, 600f);
	}

	private float GetMaxPanelWidth()
	{
		float value = 470f;
		try
		{
			if (ChestListConfig.MaxPanelWidth != null)
			{
				value = ChestListConfig.MaxPanelWidth.Value;
			}
		}
		catch
		{
		}
		value = Mathf.Clamp(value, 250f, 800f);
		return Mathf.Max(value, GetMinPanelWidth());
	}

	private float GetMaxTotalPanelWidth()
	{
		int num = Mathf.Max(1, _layoutColumns);
		float maxPanelWidth = GetMaxPanelWidth();
		float num2 = 14f * (float)Mathf.Max(0, num - 1);
		float a = maxPanelWidth * (float)num + num2;
		float b = Mathf.Max(maxPanelWidth, GetParentCanvasSize().x - 72f);
		return Mathf.Min(a, b);
	}

	private void ApplyLayoutAndAlpha()
	{
		float num = Mathf.Clamp01(ChestListConfig.UiPositionX.Value);
		float configuredPositionY = GetConfiguredPositionY();
		float alpha = Mathf.Clamp01(ChestListConfig.UiAlpha.Value);
		Vector2 parentCanvasSize = GetParentCanvasSize();
		Vector2 anchoredPosition = new Vector2((num - 0.5f) * parentCanvasSize.x, (configuredPositionY - 0.5f) * parentCanvasSize.y);
		float num2 = (Alive(_panel) ? (_panel.sizeDelta.x * 0.5f) : (_panelWidth * 0.5f));
		float num3 = (Alive(_panel) ? (_panel.sizeDelta.y * 0.5f) : (GetMinPanelHeight() * 0.5f));
		float num4 = parentCanvasSize.x * 0.5f - num2 - 36f;
		float num5 = (0f - parentCanvasSize.x) * 0.5f + num2 + 36f;
		float num6 = parentCanvasSize.y * 0.5f - num3 - 88f;
		float num7 = (0f - parentCanvasSize.y) * 0.5f + num3 + 100f;
		if (num4 < num5)
		{
			anchoredPosition.x = 0f;
		}
		else
		{
			anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, num5, num4);
		}
		if (num6 < num7)
		{
			anchoredPosition.y = 0f;
		}
		else
		{
			anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, num7, num6);
		}
		_panel.anchoredPosition = anchoredPosition;
		if (Alive(_canvasGroup))
		{
			_canvasGroup.alpha = alpha;
		}
		if (Alive(_panelImage))
		{
			_panelImage.color = GetBackgroundColor();
		}
	}

	private void RefreshContents(Container container, Inventory inventory)
	{
		if (!Alive(container) || inventory == null || !Alive(_title) || !Alive(_listRoot))
		{
			return;
		}
		int num;
		int signature;
		List<ItemDrop.ItemData> allItems;
		try
		{
			num = inventory.NrOfItems();
			allItems = inventory.GetAllItems();
			signature = ComputeInventorySignature(allItems);
		}
		catch
		{
			return;
		}
		bool value = ChestListConfig.ShowItemImages.Value;
		bool value2 = ChestListConfig.CombineStacks.Value;
		float minPanelWidth = GetMinPanelWidth();
		float maxPanelWidth = GetMaxPanelWidth();
		ChestSortType sortType = ChestListConfig.SortType.Value;
		bool sortAscending = ChestListConfig.SortAscending.Value;
		int maxEntries = ChestListConfig.MaxEntries.Value;
		string capacityFormat = ChestListConfig.CapacityFormat.Value;
		float availableHeight = GetAvailableScreenHeight();
		if ((object)container == _boundContainer && _hasBoundSignature && signature == _boundSignature && value == _boundShowIcons && value2 == _boundCombineStacks && Mathf.Approximately(minPanelWidth, _boundMinPanelWidth) && Mathf.Approximately(maxPanelWidth, _boundMaxPanelWidth) && Mathf.Approximately(availableHeight, _boundAvailableHeight) && sortType == _boundSortType && sortAscending == _boundSortAscending && maxEntries == _boundMaxEntries && capacityFormat == _boundCapacityFormat)
		{
			return;
		}
		int num3 = allItems?.Count ?? 0;
		List<ItemDrop.ItemData> list;
		try
		{
			list = BuildGridOrderedCopy(allItems);
		}
		catch
		{
			return;
		}
		if (list == null)
		{
			list = new List<ItemDrop.ItemData>();
			num3 = 0;
		}
		BuildDisplayEntries(list, value2);
		SortAndLimitDisplayEntries(sortType, sortAscending, maxEntries);
		_boundSortType = sortType;
		_boundSortAscending = sortAscending;
		_boundMaxEntries = maxEntries;
		_boundCapacityFormat = capacityFormat;
		_boundContainer = container;
		_boundSignature = signature;
		_hasBoundSignature = true;
		_boundAvailableHeight = availableHeight;
		_boundShowIcons = value;
		_boundCombineStacks = value2;
		_boundMinPanelWidth = minPanelWidth;
		_boundMaxPanelWidth = maxPanelWidth;
		float borderChromeHeight = GetBorderChromeHeight();
		float titleBlockHeight = GetTitleBlockHeight();
		int count = _displayEntries.Count;
		ComputeFitToScreenLayout(count);
		float minPanelHeight = GetMinPanelHeight();
		float num4 = Mathf.Max((float)_layoutRowsPerColumn * _layoutRowHeight, _layoutRowHeight);
		float num5 = borderChromeHeight + titleBlockHeight + num4;
		if (num5 < minPanelHeight)
		{
			num5 = minPanelHeight;
		}
		if (num5 > availableHeight)
		{
			num5 = availableHeight;
			num4 = Mathf.Max(_layoutRowHeight, num5 - borderChromeHeight - titleBlockHeight);
			_layoutRowHeight = Mathf.Max(22f, num4 / (float)Mathf.Max(1, _layoutRowsPerColumn));
		}
		if (Alive(_title))
		{
			if (Alive(_title.gameObject) && !_title.gameObject.activeSelf)
			{
				_title.gameObject.SetActive(value: true);
			}
			_title.text = GetContainerTitle(container) + FormatCapacity(capacityFormat, num, inventory);
		}
		EnsureRowCount(count);
		for (int i = 0; i < _rows.Count; i++)
		{
			RowWidgets rowWidgets = _rows[i];
			if (!Alive(rowWidgets.Root))
			{
				continue;
			}
			if (i >= count)
			{
				rowWidgets.Root.SetActive(value: false);
				continue;
			}
			rowWidgets.Root.SetActive(value: true);
			if (Alive(rowWidgets.Layout))
			{
				rowWidgets.Layout.preferredHeight = _layoutRowHeight;
				rowWidgets.Layout.minHeight = _layoutRowHeight;
				rowWidgets.Layout.flexibleHeight = 0f;
			}
			rowWidgets.Label.fontStyle = FontStyles.Normal;
			rowWidgets.Label.fontSize = _layoutItemFontSize;
			DisplayEntry displayEntry = _displayEntries[i];
			ItemDrop.ItemData itemData = displayEntry?.Item;
			if (itemData == null || itemData.m_shared == null)
			{
				rowWidgets.Icon.gameObject.SetActive(value: false);
				rowWidgets.Label.margin = Vector4.zero;
				rowWidgets.Label.text = string.Empty;
				if (displayEntry != null && displayEntry.HiddenCount > 0)
				{
					rowWidgets.Label.fontStyle = FontStyles.Italic;
					rowWidgets.Label.text = $"<alpha=#AA>+{displayEntry.HiddenCount} more";
				}
				continue;
			}
			if (value)
			{
				Sprite sprite = null;
				try
				{
					sprite = itemData.GetIcon();
				}
				catch
				{
					sprite = null;
				}
				bool flag = sprite != null;
				rowWidgets.Icon.enabled = flag;
				rowWidgets.Icon.sprite = sprite;
				rowWidgets.Icon.gameObject.SetActive(flag);
				if (flag && Alive(rowWidgets.Icon.rectTransform))
				{
					rowWidgets.Icon.rectTransform.sizeDelta = new Vector2(_layoutIconSize, _layoutIconSize);
				}
				rowWidgets.Label.margin = (flag ? new Vector4(_layoutIconSize + 8f, 0f, 0f, 0f) : Vector4.zero);
			}
			else
			{
				rowWidgets.Icon.gameObject.SetActive(value: false);
				rowWidgets.Label.margin = Vector4.zero;
			}
			rowWidgets.Label.text = FormatItemLabel(itemData, displayEntry.SortName, displayEntry.Stack);
		}
		RelayoutPanelWidth(value, count, num5);
		if (!value2 && count != num3)
		{
			ChestPreviewMod.Log?.LogDebug($"Chest list row count mismatch: shown={count}, inventory.GetAllItems()={num3}");
		}
		ApplyLayoutAndAlpha();
	}

	private static List<ItemDrop.ItemData> BuildGridOrderedCopy(List<ItemDrop.ItemData> raw)
	{
		if (raw == null || raw.Count == 0)
		{
			return new List<ItemDrop.ItemData>();
		}
		List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(raw.Count);
		for (int i = 0; i < raw.Count; i++)
		{
			ItemDrop.ItemData itemData = raw[i];
			if (itemData != null)
			{
				list.Add(itemData);
			}
		}
		list.Sort(CompareGridPos);
		return list;
	}

	// Cheap per-frame content hash; counts alone miss item swaps that keep the same totals.
	private static int ComputeInventorySignature(List<ItemDrop.ItemData> items)
	{
		unchecked
		{
			int num = 17;
			if (items == null)
			{
				return num;
			}
			num = num * 31 + items.Count;
			for (int i = 0; i < items.Count; i++)
			{
				ItemDrop.ItemData itemData = items[i];
				if (itemData != null)
				{
					num = num * 31 + (itemData.m_shared?.m_name?.GetHashCode() ?? 0);
					num = num * 31 + itemData.m_stack;
					num = num * 31 + itemData.m_quality;
					num = num * 31 + itemData.m_variant;
					num = num * 31 + itemData.m_worldLevel;
					num = num * 31 + itemData.m_gridPos.x;
					num = num * 31 + itemData.m_gridPos.y;
				}
			}
			return num;
		}
	}

	private static int CompareGridPos(ItemDrop.ItemData a, ItemDrop.ItemData b)
	{
		if (a == b)
		{
			return 0;
		}
		if (a == null)
		{
			return 1;
		}
		if (b == null)
		{
			return -1;
		}
		int num = a.m_gridPos.y.CompareTo(b.m_gridPos.y);
		if (num != 0)
		{
			return num;
		}
		return a.m_gridPos.x.CompareTo(b.m_gridPos.x);
	}

	private void ApplyListContentSize(int rowCount)
	{
		int num = Mathf.Max(1, _layoutColumns);
		_listContentHeight = Mathf.Max(0f, (float)(_layoutRowsPerColumn = ((rowCount <= 0) ? 1 : Mathf.Max(1, CeilDiv(rowCount, num)))) * _layoutRowHeight);
		if (Alive(_listRoot))
		{
			if (Alive(_listGrid))
			{
				float num2 = Mathf.Max(1f, _panelWidth - _padL - _padR);
				float num3 = 14f * (float)Mathf.Max(0, num - 1);
				float x = Mathf.Max(1f, (num2 - num3) / (float)num);
				_listGrid.constraintCount = num;
				_listGrid.cellSize = new Vector2(x, _layoutRowHeight);
				_listGrid.spacing = new Vector2((num > 1) ? 14f : 0f, 0f);
			}
			Vector2 sizeDelta = _listRoot.sizeDelta;
			sizeDelta.y = _listContentHeight;
			_listRoot.sizeDelta = sizeDelta;
			_listRoot.anchoredPosition = new Vector2(_listRoot.anchoredPosition.x, 0f);
			// The grid marks itself dirty; Unity's normal layout pass rebuilds it before rendering.
		}
	}

	// Measures with TMP_Text.GetPreferredValues, which doesn't need the text mesh or a canvas flush.
	private void RelayoutPanelWidth(bool showIcons, int total, float panelHeight)
	{
		EnsureFontsAppliedForMeasure(total);
		using (BeginInvisibleMeasureScope())
		{
			ApplyPanelSize(MeasurePanelWidth(showIcons, total), panelHeight);
			ApplyListContentSize(total);
		}
	}

	private InvisibleMeasureScope BeginInvisibleMeasureScope()
	{
		float previousAlpha = (Alive(_canvasGroup) ? _canvasGroup.alpha : 1f);
		bool num = Alive(_panel) && _panel.gameObject.activeSelf;
		bool flag = Alive(_canvas) && _canvas.enabled;
		if (!num || !flag)
		{
			if (Alive(_canvasGroup))
			{
				_canvasGroup.alpha = 0f;
			}
			if (Alive(_canvas) && !_canvas.enabled)
			{
				_canvas.enabled = true;
			}
			if (Alive(_panel) && !_panel.gameObject.activeSelf)
			{
				_panel.gameObject.SetActive(value: true);
			}
			return new InvisibleMeasureScope(this, restoreAlpha: true, previousAlpha);
		}
		return new InvisibleMeasureScope(this, restoreAlpha: false, previousAlpha);
	}

	private void EnsureFontsAppliedForMeasure(int total)
	{
		if (!TryResolveGameFont(out var _, out var _, out var _, out var _))
		{
			return;
		}
		if (Alive(_title) && (!Alive(_title.font) || IsLiberation(_title.font)))
		{
			ApplyGameFont(_title, isTitle: true);
		}
		for (int i = 0; i < total && i < _rows.Count; i++)
		{
			TextMeshProUGUI label = _rows[i].Label;
			if (Alive(label))
			{
				if (!Alive(label.font) || IsLiberation(label.font))
				{
					ApplyGameFont(label, isTitle: false);
				}
				label.fontSize = _layoutItemFontSize;
			}
		}
	}

	private float MeasurePanelWidth(bool showIcons, int total)
	{
		float num = 0f;
		float num2 = 0f;
		float minPanelWidth = GetMinPanelWidth();
		float maxPanelWidth = GetMaxPanelWidth();
		if (Alive(_title) && !string.IsNullOrEmpty(_title.text))
		{
			num2 = _title.GetPreferredValues(_title.text).x;
		}
		for (int i = 0; i < total && i < _rows.Count; i++)
		{
			RowWidgets rowWidgets = _rows[i];
			if (Alive(rowWidgets.Label) && !string.IsNullOrEmpty(rowWidgets.Label.text))
			{
				Vector4 margin = rowWidgets.Label.margin;
				rowWidgets.Label.margin = Vector4.zero;
				float x = rowWidgets.Label.GetPreferredValues(rowWidgets.Label.text).x;
				rowWidgets.Label.margin = margin;
				float num3 = 0f;
				if (showIcons && Alive(rowWidgets.Icon) && rowWidgets.Icon.gameObject.activeSelf)
				{
					num3 = _layoutIconSize + 8f;
				}
				num = Mathf.Max(num, num3 + x);
			}
		}
		int num4 = Mathf.Max(1, _layoutColumns);
		float maxTotalPanelWidth = GetMaxTotalPanelWidth();
		if (num <= 0.5f && num2 <= 0.5f)
		{
			return Mathf.Min(minPanelWidth * (float)num4 + 14f * (float)Mathf.Max(0, num4 - 1), maxTotalPanelWidth);
		}
		float value = Mathf.Max(num, num2) + _padL + _padR + 28f;
		value = Mathf.Clamp(value, minPanelWidth, maxPanelWidth);
		if (num4 <= 1)
		{
			return value;
		}
		float num5 = Mathf.Clamp(num + 28f, Mathf.Max(1f, minPanelWidth - _padL - _padR), Mathf.Max(1f, maxPanelWidth - _padL - _padR));
		float num6 = 14f * (float)(num4 - 1);
		float a = _padL + _padR + num5 * (float)num4 + num6;
		float a2 = num2 + _padL + _padR + 28f;
		return Mathf.Clamp(Mathf.Max(a, Mathf.Min(a2, maxTotalPanelWidth)), minPanelWidth, maxTotalPanelWidth);
	}

	private void ApplyPanelSize(float panelWidth, float panelHeight)
	{
		if (Alive(_panel) && Alive(_content))
		{
			panelHeight = Mathf.Max(panelHeight, GetMinPanelHeight());
			float minPanelWidth = GetMinPanelWidth();
			float maxTotalPanelWidth = GetMaxTotalPanelWidth();
			panelWidth = Mathf.Clamp(panelWidth, minPanelWidth, maxTotalPanelWidth);
			_panelWidth = panelWidth;
			_panel.sizeDelta = new Vector2(panelWidth, panelHeight);
			ApplyContentInsets();
		}
	}

	private void ApplyContentInsets()
	{
		if (Alive(_content))
		{
			_content.anchorMin = Vector2.zero;
			_content.anchorMax = Vector2.one;
			_content.offsetMin = new Vector2(_padL, _padB);
			_content.offsetMax = new Vector2(0f - _padR, 0f - _padT);
		}
	}

	private void EnsureRowCount(int count)
	{
		while (_rows.Count < count)
		{
			_rows.Add(CreateRow(_rows.Count));
		}
	}

	private RowWidgets CreateRow(int index)
	{
		GameObject gameObject = new GameObject($"Row_{index}", typeof(RectTransform), typeof(LayoutElement));
		gameObject.transform.SetParent(_listRoot, worldPositionStays: false);
		gameObject.SetActive(value: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		LayoutElement component2 = gameObject.GetComponent<LayoutElement>();
		component2.preferredHeight = 31f;
		component2.minHeight = 31f;
		component2.flexibleHeight = 0f;
		component2.flexibleWidth = 0f;
		component2.layoutPriority = 100;
		GameObject obj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component3 = obj.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 0.5f);
		component3.anchorMax = new Vector2(0f, 0.5f);
		component3.pivot = new Vector2(0f, 0.5f);
		component3.anchoredPosition = Vector2.zero;
		component3.sizeDelta = new Vector2(27f, 27f);
		Image component4 = obj.GetComponent<Image>();
		component4.preserveAspect = true;
		component4.raycastTarget = false;
		component4.maskable = true;
		obj.SetActive(value: false);
		TextMeshProUGUI textMeshProUGUI = CreateGameTmp("Label", gameObject.transform, isTitle: false);
		StretchFull(textMeshProUGUI.rectTransform);
		textMeshProUGUI.alignment = TextAlignmentOptions.MidlineLeft;
		textMeshProUGUI.raycastTarget = false;
		textMeshProUGUI.maskable = true;
		textMeshProUGUI.overflowMode = TextOverflowModes.Overflow;
		textMeshProUGUI.textWrappingMode = TextWrappingModes.NoWrap;
		textMeshProUGUI.gameObject.SetActive(value: true);
		gameObject.SetActive(value: true);
		return new RowWidgets
		{
			Root = gameObject,
			Rect = component,
			Layout = component2,
			Icon = component4,
			Label = textMeshProUGUI
		};
	}

	private void BuildDisplayEntries(List<ItemDrop.ItemData> items, bool combineStacks)
	{
		_displayEntries.Clear();
		_mergeIndexByKey.Clear();
		if (items == null || items.Count == 0)
		{
			return;
		}
		if (!combineStacks)
		{
			for (int i = 0; i < items.Count; i++)
			{
				ItemDrop.ItemData itemData = items[i];
				if (itemData != null)
				{
					_displayEntries.Add(new DisplayEntry
					{
						Item = itemData,
						Stack = itemData.m_stack
					});
				}
			}
			return;
		}
		for (int j = 0; j < items.Count; j++)
		{
			ItemDrop.ItemData itemData2 = items[j];
			if (itemData2 != null && itemData2.m_shared != null)
			{
				string key = BuildMergeKey(itemData2);
				if (_mergeIndexByKey.TryGetValue(key, out var value))
				{
					_displayEntries[value].Stack += itemData2.m_stack;
					continue;
				}
				_mergeIndexByKey[key] = _displayEntries.Count;
				_displayEntries.Add(new DisplayEntry
				{
					Item = itemData2,
					Stack = itemData2.m_stack
				});
			}
		}
	}

	private void SortAndLimitDisplayEntries(ChestSortType sortType, bool ascending, int maxEntries)
	{
		for (int i = 0; i < _displayEntries.Count; i++)
		{
			DisplayEntry entry = _displayEntries[i];
			entry.Order = i;
			entry.SortName = Localize(entry.Item?.m_shared?.m_name);
		}
		_displayEntries.Sort((a, b) => CompareEntries(a, b, sortType, ascending));
		if (maxEntries >= 0 && _displayEntries.Count > maxEntries)
		{
			int hidden = _displayEntries.Count - maxEntries;
			_displayEntries.RemoveRange(maxEntries, hidden);
			_displayEntries.Add(new DisplayEntry
			{
				HiddenCount = hidden
			});
		}
	}

	private static int CompareEntries(DisplayEntry a, DisplayEntry b, ChestSortType sortType, bool ascending)
	{
		int result = 0;
		switch (sortType)
		{
		case ChestSortType.Value:
			result = GetSortValue(a).CompareTo(GetSortValue(b));
			break;
		case ChestSortType.Weight:
			result = GetSortWeight(a).CompareTo(GetSortWeight(b));
			break;
		case ChestSortType.Name:
			result = string.Compare(a.SortName, b.SortName, StringComparison.CurrentCultureIgnoreCase);
			break;
		}
		if (!ascending)
		{
			result = -result;
		}
		if (result == 0 && sortType != ChestSortType.Name)
		{
			result = string.Compare(a.SortName, b.SortName, StringComparison.CurrentCultureIgnoreCase);
		}
		if (result == 0)
		{
			result = a.Order.CompareTo(b.Order);
		}
		return result;
	}

	private static float GetSortValue(DisplayEntry entry)
	{
		ItemDrop.ItemData.SharedData shared = entry.Item?.m_shared;
		return (shared != null) ? ((float)shared.m_value * (float)entry.Stack) : 0f;
	}

	private static float GetSortWeight(DisplayEntry entry)
	{
		ItemDrop.ItemData.SharedData shared = entry.Item?.m_shared;
		return (shared != null) ? (shared.m_weight * (float)entry.Stack) : 0f;
	}

	private string BuildMergeKey(ItemDrop.ItemData item)
	{
		_keyBuilder.Length = 0;
		ItemDrop.ItemData.SharedData shared = item.m_shared;
		string text = ((shared != null) ? shared.m_name : string.Empty);
		_keyBuilder.Append(text ?? string.Empty);
		_keyBuilder.Append('|');
		_keyBuilder.Append(item.m_worldLevel);
		if (shared != null && shared.m_maxQuality > 1)
		{
			_keyBuilder.Append('|');
			_keyBuilder.Append('q');
			_keyBuilder.Append(item.m_quality);
		}
		if (shared != null && shared.m_variants > 1)
		{
			_keyBuilder.Append('|');
			_keyBuilder.Append('v');
			_keyBuilder.Append(item.m_variant);
		}
		return _keyBuilder.ToString();
	}

	private static string GetContainerTitle(Container container)
	{
		if (!Alive(container))
		{
			return "Chest";
		}
		try
		{
			if ((object)container != null)
			{
				string hoverName = ((Hoverable)container).GetHoverName();
				if (!string.IsNullOrEmpty(hoverName))
				{
					return Localize(hoverName);
				}
			}
		}
		catch
		{
		}
		string text = container.m_name;
		if (!string.IsNullOrEmpty(text))
		{
			return Localize(text);
		}
		try
		{
			Inventory inventory = container.GetInventory();
			if (inventory != null)
			{
				string text2 = inventory.GetName();
				if (!string.IsNullOrEmpty(text2))
				{
					return Localize(text2);
				}
			}
		}
		catch
		{
		}
		try
		{
			Piece piece = ChestTarget.ContainerPiece(container);
			if (!Alive(piece))
			{
				piece = container.GetComponentInParent<Piece>();
			}
			if (Alive(piece) && !string.IsNullOrEmpty(piece.m_name))
			{
				return Localize(piece.m_name);
			}
		}
		catch
		{
		}
		return "Chest";
	}

	private static string FormatCapacity(string format, int usedSlots, Inventory inventory)
	{
		if (string.IsNullOrEmpty(format))
		{
			return string.Empty;
		}
		int totalSlots = inventory.GetWidth() * inventory.GetHeight();
		string text;
		try
		{
			text = string.Format(format, usedSlots, totalSlots);
		}
		catch (FormatException)
		{
			text = format;
		}
		return "  <alpha=#AA>" + text;
	}

	private string FormatItemLabel(ItemDrop.ItemData item, string localizedName, int stack)
	{
		_labelBuilder.Length = 0;
		if (item == null || item.m_shared == null)
		{
			return string.Empty;
		}
		_labelBuilder.Append(localizedName ?? Localize(item.m_shared.m_name));
		if (stack > 1)
		{
			_labelBuilder.Append("  x").Append(stack);
		}
		if (item.m_quality > 1)
		{
			_labelBuilder.Append("  [Q").Append(item.m_quality).Append(']');
		}
		return _labelBuilder.ToString();
	}

	private static string Localize(string token)
	{
		if (string.IsNullOrEmpty(token))
		{
			return string.Empty;
		}
		if (Localization.instance != null)
		{
			return Localization.instance.Localize(token);
		}
		return token;
	}

	private static Color GetBackgroundColor()
	{
		return new Color(0f, 0f, 0f, Mathf.Clamp01(ChestListConfig.BackgroundAlpha.Value));
	}

	private static TextMeshProUGUI CreateGameTmp(string name, Transform parent, bool isTitle)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform));
		obj.SetActive(value: false);
		obj.transform.SetParent(parent, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = obj.AddComponent<TextMeshProUGUI>();
		ApplyGameFont(textMeshProUGUI, isTitle);
		if (!Alive(textMeshProUGUI.font) || IsLiberation(textMeshProUGUI.font))
		{
			ApplyGameFont(textMeshProUGUI, isTitle);
		}
		return textMeshProUGUI;
	}

	private static void ApplyGameFont(TextMeshProUGUI text, bool isTitle)
	{
		if (!Alive(text) || !TryResolveGameFont(out var font, out var material, out var _, out var color) || IsLiberation(font) || !Alive(font))
		{
			return;
		}
		text.font = font;
		if (Alive(material))
		{
			text.fontSharedMaterial = material;
		}
		else if (Alive(font.material))
		{
			text.fontSharedMaterial = font.material;
		}
		if (Alive(text.font) && IsLiberation(text.font))
		{
			text.font = font;
			if (Alive(material))
			{
				text.fontSharedMaterial = material;
			}
		}
		text.fontSize = (isTitle ? 25f : 21f);
		text.color = (isTitle ? new Color(1f, 0.92f, 0.7f, 1f) : color);
		text.fontStyle = FontStyles.Normal;
	}

	private static bool TryResolveGameFont(out TMP_FontAsset font, out Material material, out float size, out Color color)
	{
		font = null;
		material = null;
		size = _cachedFontSize;
		color = _cachedFontColor;
		if (Alive(_cachedFont))
		{
			font = _cachedFont;
			material = (Alive(_cachedFontMaterial) ? _cachedFontMaterial : _cachedFont.material);
			size = _cachedFontSize;
			color = _cachedFontColor;
			return !IsLiberation(font);
		}
		_cachedFont = null;
		_cachedFontMaterial = null;
		InventoryGui instance = InventoryGui.instance;
		if (Alive(instance) && (TryTakeFont(instance.m_containerName, ref font, ref material, ref size, ref color) || TryTakeFont(instance.m_weight, ref font, ref material, ref size, ref color) || TryTakeFont(instance.m_containerWeight, ref font, ref material, ref size, ref color) || TryTakeFont(instance.m_playerName, ref font, ref material, ref size, ref color) || TryTakeFontFromChildren(instance.gameObject, ref font, ref material, ref size, ref color)))
		{
			CacheGameFont(font, material, size, color);
			return true;
		}
		if (Alive(Hud.instance) && TryTakeFontFromChildren(Hud.instance.gameObject, ref font, ref material, ref size, ref color))
		{
			CacheGameFont(font, material, size, color);
			return true;
		}
		TMP_FontAsset[] array = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
		foreach (TMP_FontAsset tMP_FontAsset in array)
		{
			if (Alive(tMP_FontAsset) && !string.IsNullOrEmpty(tMP_FontAsset.name) && !IsLiberation(tMP_FontAsset))
			{
				string text = tMP_FontAsset.name;
				if (text.IndexOf("Norse", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Averia", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Valheim", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					font = tMP_FontAsset;
					material = tMP_FontAsset.material;
					CacheGameFont(font, material, size, color);
					return true;
				}
			}
		}
		foreach (TMP_FontAsset tMP_FontAsset2 in array)
		{
			if (Alive(tMP_FontAsset2) && !string.IsNullOrEmpty(tMP_FontAsset2.name) && !IsLiberation(tMP_FontAsset2))
			{
				font = tMP_FontAsset2;
				material = tMP_FontAsset2.material;
				CacheGameFont(font, material, size, color);
				return true;
			}
		}
		return false;
	}

	private static bool TryTakeFont(TMP_Text source, ref TMP_FontAsset font, ref Material material, ref float size, ref Color color)
	{
		if (!Alive(source) || !Alive(source.font) || IsLiberation(source.font))
		{
			return false;
		}
		font = source.font;
		material = (Alive(source.fontSharedMaterial) ? source.fontSharedMaterial : source.font.material);
		size = ((source.fontSize > 1f) ? source.fontSize : 16f);
		color = source.color;
		return true;
	}

	private static bool TryTakeFontFromChildren(GameObject root, ref TMP_FontAsset font, ref Material material, ref float size, ref Color color)
	{
		if (!Alive(root))
		{
			return false;
		}
		TextMeshProUGUI[] componentsInChildren = root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (TryTakeFont(componentsInChildren[i], ref font, ref material, ref size, ref color))
			{
				return true;
			}
		}
		return false;
	}

	private static void CacheGameFont(TMP_FontAsset font, Material material, float size, Color color)
	{
		_cachedFont = font;
		_cachedFontMaterial = (Alive(material) ? material : ((font != null) ? font.material : null));
		_cachedFontSize = ((size > 1f) ? size : 16f);
		_cachedFontColor = color;
	}

	private static bool IsLiberation(TMP_FontAsset font)
	{
		if (!Alive(font) || string.IsNullOrEmpty(font.name))
		{
			return true;
		}
		return font.name.IndexOf("Liberation", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static void StretchFull(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	private static bool Alive(UnityEngine.Object obj)
	{
		return obj != null;
	}

	private void SafeShow()
	{
		if (_uiBuilt)
		{
			if (Alive(_canvas) && !_canvas.enabled)
			{
				_canvas.enabled = true;
			}
			if (Alive(_panel) && !_panel.gameObject.activeSelf)
			{
				_panel.gameObject.SetActive(value: true);
			}
		}
	}

	private void SafeHide()
	{
		_boundContainer = null;
		_hasBoundSignature = false;
		_boundAvailableHeight = -1f;
		_boundShowIcons = false;
		_boundCombineStacks = false;
		_boundMinPanelWidth = -1f;
		_boundMaxPanelWidth = -1f;
		_boundMaxEntries = int.MinValue;
		_listContentHeight = 0f;
		_layoutColumns = 1;
		_layoutRowsPerColumn = 1;
		_layoutRowHeight = 31f;
		_layoutIconSize = 27f;
		_layoutItemFontSize = 21f;
		HidePanel();
	}

	private void HidePanel()
	{
		if (_uiBuilt && Alive(_panel) && _panel.gameObject.activeSelf)
		{
			_panel.gameObject.SetActive(value: false);
		}
	}
}
