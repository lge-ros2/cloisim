/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ObjectInspectorWindow : MonoBehaviour
{
	private struct MotorEntry
	{
		public string Name;
		public Motor Motor;
		public PID Pid;
	}

	private struct VisualizeEntry
	{
		public string Name;
		public Device Device;
	}

	private RuntimeGizmos.TransformGizmo _gizmo;
	private Transform _currentTarget;
	private readonly List<MotorEntry> _entries = new();
	private readonly List<VisualizeEntry> _visualizeEntries = new();

	private bool _showWindow = false;
	private bool _collapsed = false;
	private readonly Dictionary<Transform, bool> _collapsedState = new();
	private readonly Dictionary<Transform, Vector2> _windowPosState = new();
	private Rect _windowRect = new Rect(20, 200, 300, 400);

	public static bool IsMouseOver { get; private set; }
	public static Rect ActiveWindowRect { get; private set; }
	public static bool IsVisible { get; private set; }
	public static bool IsEditing { get; private set; }

	private const float TitleBarHeight = 28f;
	private const float FieldWidth = 64f;
	private const float LabelWidth = 12f;
	private const float RangeLabelWidth = 48f;
	private const float WindowWidth = 260f;
	private const int CornerRadius = 6;
	private const float CardPadding = 6f;
	private const float MotorCardHeight = 100f;
	private const float SectionHeaderHeight = 24f;
	private const float VisualizeRowHeight = 22f;

	// Colors
	private static readonly Color BgColor = new Color(0.22f, 0.22f, 0.27f, 0.94f);
	private static readonly Color TitleBarColor = new Color(0.28f, 0.28f, 0.34f, 1f);
	private static readonly Color CardColor = new Color(0.26f, 0.26f, 0.32f, 1f);
	private static readonly Color AccentColor = new Color(0.35f, 0.55f, 0.95f, 1f);
	private static readonly Color MotorNameColor = new Color(0.95f, 0.80f, 0.30f, 1f);
	private static readonly Color TextColor = new Color(0.88f, 0.88f, 0.92f, 1f);
	private static readonly Color DimTextColor = new Color(0.55f, 0.55f, 0.62f, 1f);
	private static readonly Color FieldBgColor = new Color(0.18f, 0.18f, 0.22f, 1f);
	private static readonly Color ButtonBgColor = new Color(0.18f, 0.18f, 0.24f, 0.96f);
	private static readonly Color ButtonHoverColor = new Color(0.30f, 0.30f, 0.38f, 1f);
	private static readonly Color ButtonDangerColor = new Color(0.62f, 0.24f, 0.28f, 0.98f);
	private static readonly Color SeparatorColor = new Color(0.30f, 0.30f, 0.38f, 0.5f);

	private Texture2D _bgTex;
	private Texture2D _titleBarTex;
	private Texture2D _cardTex;
	private Texture2D _fieldTex;
	private Texture2D _btnNormalTex;
	private Texture2D _btnHoverTex;
	private Texture2D _btnDangerTex;
	private Texture2D _accentTex;

	private GUIStyle _titleStyle;
	private GUIStyle _motorNameStyle;
	private GUIStyle _sectionHeaderStyle;
	private GUIStyle _labelStyle;
	private GUIStyle _dimLabelStyle;
	private GUIStyle _fieldStyle;
	private GUIStyle _btnStyle;
	private GUIStyle _collapseBtnStyle;
	private GUIStyle _closeBtnStyle;
	private GUIStyle _windowBgStyle;
	private bool _stylesInitialized;

	void Start()
	{
		_gizmo = Main.Gizmos;
		CreateTextures();
	}

	void OnDestroy()
	{
		DestroyTextures();
	}

	void Update()
	{
		if (_gizmo == null)
			return;

		// Close the window if its target was deselected or removed. The window is
		// opened explicitly on right-click via OpenAt(); it is no longer shown
		// automatically on selection.
		if (_currentTarget == null)
			return;

		var mainTarget = _gizmo.mainTargetRoot;
		if (mainTarget == null || (mainTarget != _currentTarget && !IsSelected(_currentTarget)))
		{
			CloseWindow();
		}
	}

	private bool IsSelected(Transform target)
	{
		_gizmo.GetSelectedTargets(out var list);
		return list != null && list.Contains(target);
	}

	// Opens the context inspector for the given target near the cursor position,
	// offset away from the target so the window doesn't cover it.
	public static void OpenAt(Transform target, Vector2 screenPos, Camera camera)
	{
		var window = FindAnyObjectByType<ObjectInspectorWindow>();
		if (window != null)
		{
			window.Open(target, screenPos, camera);
		}
	}

	private void Open(Transform target, Vector2 screenPos, Camera camera)
	{
		if (target == null)
			return;

		if (target != _currentTarget)
		{
			SaveWindowState();
			_currentTarget = target;
		}

		RefreshContent();

		if (_entries.Count == 0 && _visualizeEntries.Count == 0)
		{
			CloseWindow();
			return;
		}

		// Input System screen space is bottom-left origin; IMGUI is top-left.
		var guiClickPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
		var windowPos = ComputeWindowPosition(target, camera, guiClickPos);
		_windowRect.x = windowPos.x;
		_windowRect.y = windowPos.y;
		_collapsed = false;
		_showWindow = true;
	}

	// Fixed pixel gap kept between the window and the target's on-screen bounds,
	// so the offset doesn't blow up when zoomed in close or shrink to nothing
	// when zoomed out — unlike a gap scaled by the target's screen-space size.
	private const float TargetClearanceGap = 24f;

	// Picks the first side (right/left/below/above) of the target's screen-space
	// bounding box that has room for the window, so it never overlaps the target.
	// Falls back to a small offset from the click point if the target fills the
	// screen and no side has room (e.g. extreme close-up).
	private Vector2 ComputeWindowPosition(Transform target, Camera camera, Vector2 guiClickPos)
	{
		var estimatedHeight = EstimateContentHeight();
		var fallback = ClampToScreen(guiClickPos + new Vector2(TargetClearanceGap, TargetClearanceGap), estimatedHeight);

		if (camera == null)
			return fallback;

		var renderers = target.GetComponentsInChildren<Renderer>();
		if (renderers.Length == 0)
			return fallback;

		var bounds = renderers[0].bounds;
		for (var i = 1; i < renderers.Length; i++)
		{
			bounds.Encapsulate(renderers[i].bounds);
		}

		var min = new Vector2(float.MaxValue, float.MaxValue);
		var max = new Vector2(float.MinValue, float.MinValue);
		for (var dx = -1; dx <= 1; dx += 2)
		{
			for (var dy = -1; dy <= 1; dy += 2)
			{
				for (var dz = -1; dz <= 1; dz += 2)
				{
					var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(dx, dy, dz));
					var screenPoint = camera.WorldToScreenPoint(corner);
					var guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
					min = Vector2.Min(min, guiPoint);
					max = Vector2.Max(max, guiPoint);
				}
			}
		}

		var verticalCenterY = Mathf.Clamp((min.y + max.y) * 0.5f - estimatedHeight * 0.5f, 0, Mathf.Max(0, Screen.height - estimatedHeight));
		var horizontalCenterX = Mathf.Clamp((min.x + max.x) * 0.5f - WindowWidth * 0.5f, 0, Mathf.Max(0, Screen.width - WindowWidth));

		// Right of target
		if (Screen.width - max.x >= WindowWidth + TargetClearanceGap)
		{
			return new Vector2(max.x + TargetClearanceGap, verticalCenterY);
		}

		// Left of target
		if (min.x - WindowWidth - TargetClearanceGap >= 0)
		{
			return new Vector2(min.x - WindowWidth - TargetClearanceGap, verticalCenterY);
		}

		// Below target
		if (Screen.height - max.y >= estimatedHeight + TargetClearanceGap)
		{
			return new Vector2(horizontalCenterX, max.y + TargetClearanceGap);
		}

		// Above target
		if (min.y - estimatedHeight - TargetClearanceGap >= 0)
		{
			return new Vector2(horizontalCenterX, min.y - estimatedHeight - TargetClearanceGap);
		}

		// Target's bounding box fills the screen (extreme close-up) — no side
		// fully clears it, so just offset from the click point.
		return fallback;
	}

	private float EstimateContentHeight()
	{
		return TitleBarHeight + 6 + PidSectionHeight() + VisualizeSectionHeight() + 6;
	}

	private static Vector2 ClampToScreen(Vector2 pos, float height)
	{
		return new Vector2(
			Mathf.Clamp(pos.x, 0, Mathf.Max(0, Screen.width - WindowWidth)),
			Mathf.Clamp(pos.y, 0, Mathf.Max(0, Screen.height - height)));
	}

	public static void CloseIfOpen()
	{
		var window = FindAnyObjectByType<ObjectInspectorWindow>();
		if (window == null)
		{
			return;
		}

		window.CloseWindow();
	}

	private void CloseWindow()
	{
		SaveWindowState();
		_currentTarget = null;
		_showWindow = false;
		_entries.Clear();
		_visualizeEntries.Clear();
	}

	private void SaveWindowState()
	{
		if (_currentTarget != null)
		{
			_windowPosState[_currentTarget] = new Vector2(_windowRect.x, _windowRect.y);
			_collapsedState[_currentTarget] = _collapsed;
		}
	}

	private void RestoreWindowState()
	{
		if (_currentTarget != null && _windowPosState.TryGetValue(_currentTarget, out var pos))
		{
			_windowRect.x = pos.x;
			_windowRect.y = pos.y;
		}
		_collapsed = _currentTarget != null && _collapsedState.TryGetValue(_currentTarget, out var saved) && saved;
	}

	private void RefreshContent()
	{
		_entries.Clear();
		_visualizeEntries.Clear();

		if (_currentTarget == null)
			return;

		var bodies = _currentTarget.GetComponentsInChildren<ArticulationBody>();
		foreach (var ab in bodies)
		{
			var motor = Motor.FindByArticulationBody(ab);
			if (motor?.PidControl != null)
			{
				var parentModel = ab.GetComponentInParent<SDFormat.Helper.Model>();
				var name = (parentModel != null && parentModel.transform != _currentTarget)
					? parentModel.name + "/" + motor.Name
					: motor.Name;

				_entries.Add(new MotorEntry
				{
					Name = name,
					Motor = motor,
					Pid = motor.PidControl,
				});
			}
		}

		var devices = _currentTarget.GetComponentsInChildren<Device>();
		foreach (var device in devices)
		{
			if (device.SupportsVisualize)
			{
				var name = string.IsNullOrEmpty(device.DeviceName) ? device.name : device.DeviceName;
				_visualizeEntries.Add(new VisualizeEntry
				{
					Name = name,
					Device = device,
				});
			}
		}
	}

	private Texture2D MakeTex(Color color)
	{
		var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		tex.SetPixel(0, 0, color);
		tex.Apply();
		return tex;
	}

	private Texture2D MakeRoundedTex(int w, int h, Color fill, int radius)
	{
		var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
		var clear = new Color(0, 0, 0, 0);
		for (var y = 0; y < h; y++)
		{
			for (var x = 0; x < w; x++)
			{
				var inside = true;
				// check four corners
				if (x < radius && y < radius)
					inside = (radius - x) * (radius - x) + (radius - y) * (radius - y) <= radius * radius;
				else if (x >= w - radius && y < radius)
					inside = (x - (w - radius - 1)) * (x - (w - radius - 1)) + (radius - y) * (radius - y) <= radius * radius;
				else if (x < radius && y >= h - radius)
					inside = (radius - x) * (radius - x) + (y - (h - radius - 1)) * (y - (h - radius - 1)) <= radius * radius;
				else if (x >= w - radius && y >= h - radius)
					inside = (x - (w - radius - 1)) * (x - (w - radius - 1)) + (y - (h - radius - 1)) * (y - (h - radius - 1)) <= radius * radius;

				tex.SetPixel(x, y, inside ? fill : clear);
			}
		}
		tex.Apply();
		return tex;
	}

	private void CreateTextures()
	{
		_bgTex = MakeRoundedTex(64, 64, BgColor, CornerRadius);
		_titleBarTex = MakeTex(TitleBarColor);
		_cardTex = MakeTex(CardColor);
		_fieldTex = MakeTex(FieldBgColor);
		_btnNormalTex = MakeRoundedTex(24, 24, ButtonBgColor, 4);
		_btnHoverTex = MakeRoundedTex(24, 24, ButtonHoverColor, 4);
		_btnDangerTex = MakeRoundedTex(24, 24, ButtonDangerColor, 4);
		_accentTex = MakeTex(AccentColor);
	}

	private void DestroyTextures()
	{
		Destroy(_bgTex);
		Destroy(_titleBarTex);
		Destroy(_cardTex);
		Destroy(_fieldTex);
		Destroy(_btnNormalTex);
		Destroy(_btnHoverTex);
		Destroy(_btnDangerTex);
		Destroy(_accentTex);
	}

	private void InitStyles()
	{
		if (_stylesInitialized)
			return;

		_titleStyle = new GUIStyle(GUI.skin.label)
		{
			fontStyle = FontStyle.Bold,
			fontSize = 13,
			alignment = TextAnchor.MiddleLeft,
			normal = { textColor = TextColor },
			padding = new RectOffset(4, 0, 0, 0),
		};

		_motorNameStyle = new GUIStyle(GUI.skin.label)
		{
			fontStyle = FontStyle.Bold,
			fontSize = 12,
			alignment = TextAnchor.MiddleLeft,
			normal = { textColor = MotorNameColor },
			padding = new RectOffset(2, 0, 2, 2),
		};

		// Section headers ("PID Control", "Visualize") use a distinct accent
		// color + smaller caps look so they read as group labels, not entries —
		// otherwise they blend visually with the per-link/per-device names below.
		_sectionHeaderStyle = new GUIStyle(GUI.skin.label)
		{
			fontStyle = FontStyle.Bold,
			fontSize = 10,
			alignment = TextAnchor.MiddleLeft,
			normal = { textColor = AccentColor },
			padding = new RectOffset(2, 0, 0, 0),
		};

		_labelStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 12,
			alignment = TextAnchor.MiddleLeft,
			normal = { textColor = TextColor },
			fontStyle = FontStyle.Bold,
		};

		_dimLabelStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 11,
			alignment = TextAnchor.MiddleLeft,
			normal = { textColor = DimTextColor },
		};

		_fieldStyle = new GUIStyle(GUI.skin.textField)
		{
			fontSize = 12,
			alignment = TextAnchor.MiddleRight,
			normal = { background = _fieldTex, textColor = TextColor },
			focused = { background = _fieldTex, textColor = Color.white },
			hover = { background = _fieldTex, textColor = TextColor },
			active = { background = _fieldTex, textColor = Color.white },
			padding = new RectOffset(4, 4, 2, 2),
			margin = new RectOffset(2, 2, 1, 1),
			border = new RectOffset(0, 0, 0, 0),
			fixedHeight = 20,
		};

		_btnStyle = new GUIStyle(GUI.skin.button)
		{
			fontSize = 14,
			fontStyle = FontStyle.Bold,
			fixedWidth = 22,
			fixedHeight = 22,
			alignment = TextAnchor.MiddleCenter,
			normal = { textColor = TextColor, background = _btnNormalTex },
			hover = { textColor = TextColor, background = _btnHoverTex },
			active = { textColor = TextColor, background = _btnHoverTex },
			focused = { textColor = TextColor, background = _btnNormalTex },
			border = new RectOffset(0, 0, 0, 0),
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 3, 0),
			contentOffset = new Vector2(0, -1),
		};

		_collapseBtnStyle = new GUIStyle(_btnStyle)
		{
			fontSize = 13,
			contentOffset = Vector2.zero,
		};

		_closeBtnStyle = new GUIStyle(_btnStyle)
		{
			fontSize = 13,
			contentOffset = Vector2.zero,
			hover = { textColor = Color.white, background = _btnDangerTex },
			active = { textColor = Color.white, background = _btnDangerTex },
		};

		_windowBgStyle = new GUIStyle
		{
			normal = { background = _bgTex },
			border = new RectOffset(CornerRadius, CornerRadius, CornerRadius, CornerRadius),
			padding = new RectOffset(0, 0, 0, 0),
		};

		_stylesInitialized = true;
	}

	void OnGUI()
	{
		if (!_showWindow || (_entries.Count == 0 && _visualizeEntries.Count == 0))
		{
			IsMouseOver = false;
			IsVisible = false;
			IsEditing = false;
			return;
		}

		// Check if mouse is over the window to block scene interaction
		var mousePos = Event.current.mousePosition;
		IsMouseOver = _windowRect.Contains(mousePos);

		InitStyles();

		var contentHeight = _collapsed
			? TitleBarHeight
			: TitleBarHeight + 6 + PidSectionHeight() + VisualizeSectionHeight() + 6;

		_windowRect.width = WindowWidth;
		_windowRect.height = contentHeight;

		// Clamp to screen before drawing
		_windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
		_windowRect.y = Mathf.Clamp(_windowRect.y, 0, Mathf.Max(0, Screen.height - _windowRect.height));

		_windowRect = GUI.Window(9901, _windowRect, DrawWindow, GUIContent.none, _windowBgStyle);

		ActiveWindowRect = _windowRect;
		IsVisible = true;
		IsEditing = GUIUtility.keyboardControl != 0;
	}

	private void DrawWindow(int windowID)
	{
		// Title bar background
		var titleRect = new Rect(0, 0, _windowRect.width, TitleBarHeight);
		GUI.DrawTexture(titleRect, _titleBarTex);

		// Accent line at top
		GUI.DrawTexture(new Rect(0, 0, _windowRect.width, 2), _accentTex);

		// Title bar content
		var modelName = _currentTarget != null ? _currentTarget.name : "Inspector";
		var titleContentRect = new Rect(8, 1, _windowRect.width - 64, TitleBarHeight);
		GUI.Label(titleContentRect, modelName, _titleStyle);

		// Collapse button
		var collapseRect = new Rect(_windowRect.width - 52, 5, 22, 22);
		if (GUI.Button(collapseRect, _collapsed ? "\u25A1" : "_", _collapseBtnStyle))
		{
			_collapsed = !_collapsed;
			if (_currentTarget != null)
				_collapsedState[_currentTarget] = _collapsed;
		}

		// Close button
		var closeRect = new Rect(_windowRect.width - 27, 5, 22, 22);
		if (GUI.Button(closeRect, "X", _closeBtnStyle))
		{
			_showWindow = false;
			SaveWindowState();
		}

		// Drag from title bar
		GUI.DragWindow(titleRect);

		if (_collapsed)
			return;

		// Content area
		var y = TitleBarHeight + 8f;

		if (_entries.Count > 0)
		{
			y = DrawSectionHeader("PID CONTROL", y, _windowRect.width);

			foreach (var entry in _entries)
			{
				y = DrawMotorPID(entry, y, _windowRect.width);
			}
		}

		if (_visualizeEntries.Count > 0)
		{
			y = DrawVisualizeSection(y, _windowRect.width);
		}
	}

	// Draws a group label (e.g. "PID CONTROL") in a distinct accent color/caps
	// with an underline, so it reads clearly as a section separator rather than
	// blending in with the per-entry names (e.g. right_wheel_link) drawn below it.
	private float DrawSectionHeader(string label, float y, float width)
	{
		GUI.Label(new Rect(CardPadding + 2, y, width - CardPadding * 2, 16f), label, _sectionHeaderStyle);
		var lineY = y + 17f;
		GUI.DrawTexture(new Rect(CardPadding + 2, lineY, width - CardPadding * 4, 1), _accentTex);
		return y + SectionHeaderHeight;
	}

	private float PidSectionHeight()
	{
		return _entries.Count == 0
			? 0f
			: SectionHeaderHeight + _entries.Count * MotorCardHeight;
	}

	private float VisualizeSectionHeight()
	{
		return _visualizeEntries.Count == 0
			? 0f
			: SectionHeaderHeight + _visualizeEntries.Count * VisualizeRowHeight;
	}

	private float DrawVisualizeSection(float y, float width)
	{
		y = DrawSectionHeader("VISUALIZE", y, width);

		foreach (var entry in _visualizeEntries)
		{
			var rowRect = new Rect(CardPadding + 4, y, width - CardPadding * 2 - 4, VisualizeRowHeight);
			DrawVisualizeToggleRow(entry, rowRect);
			y += VisualizeRowHeight;
		}

		return y;
	}

	private void DrawVisualizeToggleRow(in VisualizeEntry entry, Rect rowRect)
	{
		const float boxSize = 16f;
		var boxRect = new Rect(rowRect.x, rowRect.y + (rowRect.height - boxSize) * 0.5f, boxSize, boxSize);
		var labelRect = new Rect(boxRect.xMax + 6f, rowRect.y, rowRect.width - boxSize - 6f, rowRect.height);

		var enabled = entry.Device.EnableVisualize;

		GUI.DrawTexture(boxRect, _fieldTex);
		if (enabled)
		{
			var checkRect = new Rect(boxRect.x + 2f, boxRect.y + 2f, boxSize - 4f, boxSize - 4f);
			GUI.DrawTexture(checkRect, _accentTex);
		}

		GUI.Label(labelRect, entry.Name, _dimLabelStyle);

		if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
		{
			entry.Device.EnableVisualize = !enabled;
		}
	}

	private float DrawMotorPID(in MotorEntry entry, float y, float width)
	{
		var pid = entry.Pid;
		var cardH = 96f;
		var cardRect = new Rect(CardPadding, y, width - CardPadding * 2, cardH);
		var innerPad = 2f;
		var rightEdge = cardRect.xMax - innerPad;

		// Card background (rounded)
		GUI.DrawTexture(cardRect, _cardTex, ScaleMode.StretchToFill);

		// Motor name
		var nameRect = new Rect(cardRect.x + innerPad, cardRect.y + 2, cardRect.width - innerPad * 2, 16);
		GUI.Label(nameRect, entry.Name, _motorNameStyle);

		// P, I, D row — evenly distributed with gaps between fields
		var rowY = cardRect.y + 24;
		var fieldGap = 10f;
		var availW = cardRect.width - innerPad * 2 - fieldGap * 2;
		var pairW = availW / 3f;

		DrawGainField("P", pid.PGain, cardRect.x + innerPad, rowY, pairW,
			v => pid.Change(v, pid.IGain, pid.DGain));
		DrawGainField("I", pid.IGain, cardRect.x + innerPad + pairW + fieldGap, rowY, pairW,
			v => pid.Change(pid.PGain, v, pid.DGain));
		DrawGainField("D", pid.DGain, cardRect.x + innerPad + (pairW + fieldGap) * 2, rowY, pairW,
			v => pid.Change(pid.PGain, pid.IGain, v));

		// Separator
		var sepY = rowY + 26;
		GUI.DrawTexture(new Rect(cardRect.x + innerPad, sepY, cardRect.width - innerPad * 2, 1), _fieldTex);

		// Range rows — fields aligned to same right edge
		var rangeY = sepY + 4;
		var rangeW = cardRect.width - innerPad * 2;
		DrawRangeRow("Integral", pid.IntegralRangeMin, pid.IntegralRangeMax,
			cardRect.x + innerPad, rangeY, rangeW, rightEdge, pid.SetIntegralRange);
		DrawRangeRow("Output", pid.OutputRangeMin, pid.OutputRangeMax,
			cardRect.x + innerPad, rangeY + 22, rangeW, rightEdge, pid.SetOutputRange);

		return y + cardH + 4;
	}

	private void DrawGainField(string label, double value, float x, float y, float width, System.Action<double> onChanged)
	{
		GUI.Label(new Rect(x, y + 2, LabelWidth, 20), label, _labelStyle);
		var text = GUI.TextField(new Rect(x + LabelWidth + 2, y, width - LabelWidth - 2, 20), value.ToString("F4"), _fieldStyle);

		if (double.TryParse(text, out var parsed) && parsed != value)
		{
			onChanged(parsed);
		}
	}

	private void DrawRangeRow(string label, double min, double max, float x, float y, float totalWidth, float rightEdge, System.Action<double, double> onChanged)
	{
		GUI.Label(new Rect(x, y + 1, RangeLabelWidth, 18), label, _dimLabelStyle);

		var fieldW = FieldWidth;
		var gap = 6f;
		var maxFieldX = rightEdge - fieldW;
		var minFieldX = maxFieldX - fieldW - gap;
		var minText = GUI.TextField(new Rect(minFieldX, y, fieldW, 20), min.ToString("F2"), _fieldStyle);
		var maxText = GUI.TextField(new Rect(maxFieldX, y, fieldW, 20), max.ToString("F2"), _fieldStyle);

		if (double.TryParse(minText, out var parsedMin) && double.TryParse(maxText, out var parsedMax)
			&& (parsedMin != min || parsedMax != max))
		{
			onChanged(parsedMin, parsedMax);
		}
	}
}
