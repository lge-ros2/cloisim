/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;

public abstract class MarkerBase
{
	public virtual void Print()
	{
	}
}

public abstract class MarkerTypeBase : MarkerBase
{
	[SerializeField] public float size = 0.01f;
	[SerializeField] public Vector3 point = Vector3.zero;
}

[Serializable]
public class MarkerLine: MarkerTypeBase
{
	[SerializeField] public Vector3 endpoint = Vector3.zero;

	public override void Print()
	{
		Debug.LogFormat("## MarkerLine: {0}, {1}, {2}", size, point, endpoint);
	}
}

[Serializable]
public class MarkerText: MarkerTypeBase, ISerializationCallbackReceiver
{
	public enum TextAlign : ushort {Left = 0, Center, Right};

	[SerializeField] private string align = string.Empty;
	[SerializeField] public string following = string.Empty;
	[SerializeField] public string text = string.Empty;
	[NonSerialized] public TextAlign textAlign = TextAlign.Left;

	public void OnAfterDeserialize()
	{
		try
		{
			textAlign = (TextAlign)Enum.Parse(typeof(TextAlign), align, true);
		}
		catch (ArgumentException)
		{
			textAlign = TextAlign.Left;
		}
	}

	public void OnBeforeSerialize()
	{
		align = textAlign.ToString().ToLower();
	}


	public override void Print()
	{
		Debug.LogFormat("## MarkerText: {0}, {1}, {2}, {3}", size, align, following, point);
	}
}

[Serializable]
public class MarkerBox: MarkerTypeBase
{
	public override void Print()
	{
		Debug.LogFormat("## MarkerBox: {0}, {1}", size, point);
	}
}

[Serializable]
public class MarkerSphere: MarkerTypeBase
{
	public override void Print()
	{
		Debug.LogFormat("## MarkerSphere: {0}, {1}", size, point);
	}
}

[Serializable]
public class Marker: MarkerBase, ISerializationCallbackReceiver
{
	[SerializeField] public string group = string.Empty;
	[SerializeField] public int id = -1;
	[SerializeField] private string type = string.Empty;
	[SerializeField] private string color = string.Empty;

	public enum Types : ushort {Unknown = 0, Line, Sphere, Box, Text};
	public enum Colors : ushort
	{Unknown = 0, Red, Green, Blue, Gray, Orange, Lime, Pink, Purple, Navy, Aqua, Cyan, Magenta, Yellow, Black};

	[NonSerialized] public Types markerType = Types.Unknown;
	[NonSerialized] public Colors markerColor = Colors.Unknown;

	public void OnAfterDeserialize()
	{
		try
		{
			markerType = (Types)Enum.Parse(typeof(Types), type, true);
		}
		catch (ArgumentException)
		{
			markerType = Types.Unknown;
		}

		try
		{
			markerColor = (Colors)Enum.Parse(typeof(Colors), color, true);
		}
		catch (ArgumentException)
		{
			markerColor = Colors.Unknown;
		}
	}

	public void OnBeforeSerialize()
	{
		type = markerType.ToString().ToLower();

		color = markerColor.ToString().ToLower();
	}

	public string MarkerName()
	{
		return group + SimulationService.Delimiter + id + SimulationService.Delimiter + type;
	}

	public Color GetColor()
	{
		Color targetColor;
		switch (markerColor)
		{
			case Colors.Red:
				targetColor = Color.red;
				break;

			case Colors.Green:
				targetColor = Color.green;
				break;

			case Colors.Blue:
				targetColor = Color.blue;
				break;

			case Colors.Gray:
				targetColor = Color.gray;
				break;

			case Colors.Orange:
				targetColor = new Color32(254, 161, 0, 1);
				break;

			case Colors.Lime:
				targetColor = new Color32(166, 254, 0, 1);
				break;

			case Colors.Pink:
				targetColor = new Color32(232, 0, 254, 1);
				break;

			case Colors.Purple:
				targetColor = new Color32(143, 0, 254, 1);
				break;

			case Colors.Navy:
				targetColor = new Color32(60, 0, 254, 1);
				break;

			case Colors.Aqua:
				targetColor = new Color32(0, 201, 254, 1);
				break;

			case Colors.Cyan:
				targetColor = Color.cyan;
				break;

			case Colors.Magenta:
				targetColor = Color.magenta;
				break;

			case Colors.Yellow:
				targetColor = Color.yellow;
				break;

			case Colors.Black:
			case Colors.Unknown:
			default:
				targetColor = Color.black;
				break;
		}

		targetColor.a = 1.0f;
		return targetColor;
	}

	public override void Print()
	{
		Debug.LogFormat("## Marker: {0}, {1}, {2}, {3}", group, id, type, color);
	}
}

[Serializable]
public class MarkerRequest : Marker
{
	[SerializeField] public MarkerLine line = null;
	[SerializeField] public MarkerText text = null;
	[SerializeField] public MarkerBox box = null;
	[SerializeField] public MarkerSphere sphere = null;

	public override void Print()
	{
		base.Print();

		switch (markerType)
		{
			case Types.Line:
				line.Print();
				break;

			case Types.Box:
				box.Print();
				break;

			case Types.Text:
				text.Print();
				break;

			case Types.Sphere:
				sphere.Print();
				break;

			case Types.Unknown:
			default:
				Debug.Log("Unknown marker type!!!");
				break;
		}
	}
}

[Serializable]
public class MarkerResponseLine : Marker
{
	[SerializeField] public MarkerLine marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

[Serializable]
public class MarkerResponseText : Marker
{
	[SerializeField] public MarkerText marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

[Serializable]
public class MarkerResponseSphere : Marker
{
	[SerializeField] public MarkerSphere marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

[Serializable]
public class MarkerResponseBox : Marker
{
	[SerializeField] public MarkerBox marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

[Serializable]
public class MarkerFilter
{
	[SerializeField] public string group = string.Empty;
	[SerializeField] public int id = -1;
	[SerializeField] public string type = string.Empty;

	public bool IsEmpty()
	{
		if (string.IsNullOrEmpty(group) && id == -1 && string.IsNullOrEmpty(type))
		{
			return true;
		}

		return false;
	}
}

[Serializable]
public class VisualMarkerRequest : MarkerBase, ISerializationCallbackReceiver
{
	[SerializeField] public string command = string.Empty;
	[SerializeField] public List<MarkerRequest> markers = null;
	[SerializeField] public MarkerFilter filter = null;

	public enum MarkerCommands : ushort {Unknown = 0, Add, Modify, Remove, List};
	[NonSerialized] public MarkerCommands markerCommand = MarkerCommands.Unknown;

	public void OnAfterDeserialize()
	{
		try {
			// Debug.Log(command);
			markerCommand = (MarkerCommands)Enum.Parse(typeof(MarkerCommands), command, true);
		}
		catch (ArgumentException)
		{
			// Debug.Log("X" + command);
			markerCommand = MarkerCommands.Unknown;
		}
	}

	public void OnBeforeSerialize()
	{
		command = markerCommand.ToString().ToLower();
	}

	public override void Print()
	{
		Debug.Log("====================================");
		Debug.LogFormat("## VisualMarkers: {0}", command);
		foreach (var marker in markers)
		{
			marker.Print();
		}
		Debug.Log("====================================");
	}
}

[Serializable]
public class VisualMarkerResponse : MarkerBase
{
	[SerializeField] public string command = string.Empty;
	[SerializeField] public string result = string.Empty;

	[SerializeField] public List<MarkerResponseLine> lines = null;
	[SerializeField] public List<MarkerResponseText> texts = null;
	[SerializeField] public List<MarkerResponseBox> boxes = null;
	[SerializeField] public List<MarkerResponseSphere> spheres = null;

	public VisualMarkerResponse()
	{
		// lines = new List<MarkerResponse<MarkerLine>>();
		// texts = new List<MarkerResponse<MarkerText>>();
		// boxes = new List<MarkerResponse<MarkerBox>>();
		// spheres = new List<MarkerResponse<MarkerSphere>>();
	}

	public override void Print()
	{
		Debug.LogFormat("## VisualMarkers: {0}, {1}", command, result);

		if (lines != null)
		{
			foreach (var line in lines)
			{
				line.Print();
			}
		}

		if (texts != null)
		{
			foreach (var text in texts)
			{
				text.Print();
			}
		}

		if (boxes != null)
		{
			foreach (var box in boxes)
			{
				box.Print();
			}
		}

		if (spheres != null)
		{
			foreach (var sphere in spheres)
			{
				sphere.Print();
			}
		}
	}
}

public class MarkerVisualizerService : WebSocketBehavior
{
	private MarkerVisualizer markerVisualizer = null;

	public MarkerVisualizerService(in MarkerVisualizer target)
	{
		markerVisualizer = target;
		markerVisualizer.RegisterResponseAction(SendResponse);
	}

	protected override void OnOpen()
	{
		Debug.Log("Open");
	}

	protected override void OnClose(CloseEventArgs e)
	{
		Debug.LogFormat("Close({0}), {1}", e.Code, e.Reason);
	}

	protected override void OnMessage(MessageEventArgs e)
	{
		if (e.RawData.Length == 0 || e.IsPing)
		{
			// Debug.LogFormat("length:{0}, {1}", e.RawData.Length, e.IsPing);
			return;
		}

		var request = JsonUtility.FromJson<VisualMarkerRequest>(e.Data);

		// request.Print();

		var isSuccessful = markerVisualizer.PushRequsetMarkers(request);

		if (!isSuccessful)
		{
			SendResponse();
		}
	}

	protected override void OnError(ErrorEventArgs e)
	{
		Debug.LogFormat("{0}::OnError : {1}", GetType().Name, e.Message);
		Sessions.CloseSession(ID);
	}

	void SendResponse()
	{
		var response = markerVisualizer.GetResponseMarkers();
		var responseJsonData = JsonUtility.ToJson(response, false);

		// response.Print();

		StringBuilder sb = new StringBuilder(responseJsonData);
		// Debug.Log(responseJsonData);
		sb.Replace(@",""lines"":[]", "");
		sb.Replace(@",""texts"":[]", "");
		sb.Replace(@",""boxes"":[]", "");
		sb.Replace(@",""spheres"":[]", "");
		// Debug.Log(sb.ToString());

		Send(sb.ToString());
	}
}