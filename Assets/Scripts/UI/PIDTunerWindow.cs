/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public class PIDTunerWindow : MonoBehaviour
{
	private struct MotorEntry
	{
		public string Name;
		public Motor Motor;
		public PID Pid;
	}

	private RuntimeGizmos.TransformGizmo _gizmo;
	private Transform _currentTarget;
	private readonly List<MotorEntry> _entries = new();

	private bool _showWindow = false;
	private bool _collapsed = false;
	private bool _closedByUser = false;
	private readonly Dictionary<Transform, bool> _collapsedState = new();
	private readonly Dictionary<Transform, Vector2> _windowPosState = new();
	private Rect _windowRect = new Rect(20, 200, 300, 400);

	public static bool IsMouseOver { get; private set; }
	public static Rect ActiveWindowRect { get; private set; }
	public static bool IsVisible { get; private set; }
	public static bool IsEditing { get; private set; }
	private Vector2 _scrollPos;

	private const float TitleBarHeight = 28f;
	private const float FieldWidth = 64f;
	private const float LabelWidth = 12f;
	private const float RangeLabelWidth = 48f;
	private const float WindowWidth = 260f;
	private const int CornerRadius = 6;
	private const float CardPadding = 6f;

	// Colors
	private static readonly Color BgColor = new Color(0.16f, 0.16f, 0.20f, 0.94f);
	private static readonly Color TitleBarColor = new Color(0.22f, 0.22f, 0.28f, 1f);
	private static readonly Color CardColor = new Color(0.20f, 0.20f, 0.26f, 1f);
	private static readonly Color AccentColor = new Color(0.35f, 0.55f, 0.95f, 1f);
	private static readonly Color TextColor = new Color(0.88f, 0.88f, 0.92f, 1f);
	private static readonly Color DimTextColor = new Color(0.55f, 0.55f, 0.62f, 1f);
	private static readonly Color FieldBgColor = new Color(0.12f, 0.12f, 0.15f, 1f);
	private static readonly Color ButtonHoverColor = new Color(0.30f, 0.30f, 0.38f, 1f);
	private static readonly Color SeparatorColor = new Color(0.30f, 0.30f, 0.38f, 0.5f);

	private Texture2D _bgTex;
	private Texture2D _titleBarTex;
	private Texture2D _cardTex;
	private Texture2D _fieldTex;
	private Texture2D _btnNormalTex;
	private Texture2D _btnHoverTex;
	private Texture2D _accentTex;

	private GUIStyle _titleStyle;
	private GUIStyle _motorNameStyle;
	private GUIStyle _labelStyle;
	private GUIStyle _dimLabelStyle;
	private GUIStyle _fieldStyle;
	private GUIStyle _btnStyle;
	private GUIStyle _windowBgStyle;
	private GUIStyle _cardStyle;
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

		var target = _gizmo.mainTargetRoot;

		if (target == null)
		{
			if (_currentTarget != null)
			{
				SaveWindowState();
				_currentTarget = null;
				_showWindow = false;
				_closedByUser = false;
			}
			return;
		}

		if (target != _currentTarget)
		{
			SaveWindowState();
			_currentTarget = target;
			_closedByUser = false;
			RestoreWindowState();
			RefreshMotors();
		}
		else if (!_showWindow && !_closedByUser && _entries.Count > 0)
		{
			_showWindow = true;
		}
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

	private void RefreshMotors()
	{
		_entries.Clear();

		if (_currentTarget == null)
		{
			_showWindow = false;
			return;
		}

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

		_showWindow = _entries.Count > 0;
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
		_btnNormalTex = MakeTex(new Color(0, 0, 0, 0));
		_btnHoverTex = MakeTex(ButtonHoverColor);
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
			normal = { textColor = AccentColor },
			padding = new RectOffset(2, 0, 2, 2),
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

		_btnStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 14,
			fixedWidth = 24,
			fixedHeight = 24,
			alignment = TextAnchor.MiddleCenter,
			normal = { textColor = DimTextColor },
			hover = { textColor = TextColor, background = _btnHoverTex },
			padding = new RectOffset(0, 0, 0, 2),
			margin = new RectOffset(0, 0, 3, 0),
		};

		_windowBgStyle = new GUIStyle
		{
			normal = { background = _bgTex },
			border = new RectOffset(CornerRadius, CornerRadius, CornerRadius, CornerRadius),
			padding = new RectOffset(0, 0, 0, 0),
		};

		_cardStyle = new GUIStyle
		{
			normal = { background = _cardTex },
			padding = new RectOffset(8, 8, 6, 6),
			margin = new RectOffset(6, 6, 3, 3),
			border = new RectOffset(0, 0, 0, 0),
		};

		_stylesInitialized = true;
	}

	void OnGUI()
	{
		if (!_showWindow || _entries.Count == 0)
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
			: TitleBarHeight + 6 + _entries.Count * 100 + 6;
		contentHeight = Mathf.Min(contentHeight, Screen.height * 0.7f);

		_windowRect.width = WindowWidth;
		_windowRect.height = contentHeight;

		// Clamp to screen before drawing
		_windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
		_windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - TitleBarHeight);

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
		var modelName = _currentTarget != null ? _currentTarget.name : "PID";
		var titleContentRect = new Rect(8, 0, _windowRect.width - 64, TitleBarHeight);
		GUI.Label(titleContentRect, "PID Control [" + modelName + "]", _titleStyle);

		// Collapse button
		var collapseRect = new Rect(_windowRect.width - 54, 3, 24, 24);
		if (GUI.Button(collapseRect, _collapsed ? "\u25a1" : "\u2014", _btnStyle))
		{
			_collapsed = !_collapsed;
			if (_currentTarget != null)
				_collapsedState[_currentTarget] = _collapsed;
		}

		// Close button
		var closeRect = new Rect(_windowRect.width - 28, 3, 24, 24);
		if (GUI.Button(closeRect, "\u2715", _btnStyle))
		{
			_showWindow = false;
			_closedByUser = true;
			SaveWindowState();
		}

		// Drag from title bar
		GUI.DragWindow(titleRect);

		if (_collapsed)
			return;

		// Content area
		var contentY = TitleBarHeight + 4;
		var contentRect = new Rect(0, contentY, _windowRect.width, _windowRect.height - contentY);
		var viewHeight = _entries.Count * 100 + 8;
		var viewRect = new Rect(0, 0, contentRect.width - 14, viewHeight);

		_scrollPos = GUI.BeginScrollView(contentRect, _scrollPos, viewRect);

		var y = 4f;
		foreach (var entry in _entries)
		{
			y = DrawMotorPID(entry, y, viewRect.width);
		}

		GUI.EndScrollView();
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
