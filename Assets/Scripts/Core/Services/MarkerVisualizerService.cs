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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

public abstract class MarkerBase
{
	public virtual void Print()
	{
	}
}

public abstract class MarkerTypeBase : MarkerBase
{
	public float size = 0.01f;
	public Vector3 point = Vector3.zero;
}

public class MarkerLine: MarkerTypeBase
{
	public Vector3 endpoint = Vector3.zero;

	public override void Print()
	{
		Debug.LogFormat("## MarkerLine: {0}, {1}, {2}", size, point, endpoint);
	}
}

public class MarkerText: MarkerTypeBase
{
	public enum TextAlign : ushort {Left = 0, Center, Right};

	[JsonConverter(typeof(StringEnumConverter))]
	public TextAlign align = TextAlign.Left;
	public string following = string.Empty;
	public string text = string.Empty;

	public override void Print()
	{
		Debug.LogFormat("## MarkerText: {0}, {1}, {2}, {3}", size, align, following, point);
	}
}

public class MarkerBox: MarkerTypeBase
{
	public override void Print()
	{
		Debug.LogFormat("## MarkerBox: {0}, {1}", size, point);
	}
}

public class MarkerSphere: MarkerTypeBase
{
	public override void Print()
	{
		Debug.LogFormat("## MarkerSphere: {0}, {1}", size, point);
	}
}

[Serializable()]
public class Marker: MarkerBase
{
	public enum Types : ushort {Unknown = 0, Line, Sphere, Box, Text};
	public enum Colors : ushort {Unknown = 0, Red, Green, Blue, Gray, Orange, Lime, Pink, Purple, Navy, Aqua, Cyan, Magenta, Yellow, Black};

	public string group = string.Empty;
	public int id = -1;

	[JsonConverter(typeof(StringEnumConverter))]
	public Types type = Types.Unknown;

	[JsonConverter(typeof(StringEnumConverter))]
	public Colors color = Colors.Unknown;

	public string MarkerName()
	{
		return group + SimulationService.Delimiter + id + SimulationService.Delimiter + type;
	}

	public Color GetColor()
	{
		Color targetColor;
		switch (color)
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
		Debug.LogFormat("## Marker: {0}, {1}, {2}, Color({3})", group, id, type, color);
	}
}

public class MarkerRequest : Marker
{
	public MarkerLine line = null;
	public MarkerText text = null;
	public MarkerBox box = null;
	public MarkerSphere sphere = null;

	public override void Print()
	{
		base.Print();

		switch (type)
		{
			case Types.Line:
				if (line != null)
				{
					line.Print();
				}
				break;

			case Types.Box:
				if (box != null)
				{
					box.Print();
				}
				break;

			case Types.Text:
				if (text != null)
				{
					text.Print();
				}
				break;

			case Types.Sphere:
				if (sphere != null)
				{
					sphere.Print();
				}
				break;

			case Types.Unknown:
			default:
				Debug.Log("Unknown marker type!!!");
				break;
		}
	}
}

public class MarkerResponseLine : Marker
{
	public MarkerLine marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

public class MarkerResponseText : Marker
{
	public MarkerText marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

public class MarkerResponseSphere : Marker
{
	public MarkerSphere marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

public class MarkerResponseBox : Marker
{
	public MarkerBox marker = null;

	public override void Print()
	{
		base.Print();
		marker.Print();
	}
}

public class MarkerFilter
{
	public string group = string.Empty;
	public int id = -1;
	public string type = string.Empty;

	public bool IsEmpty()
	{
		if (string.IsNullOrEmpty(group) && id == -1 && string.IsNullOrEmpty(type))
		{
			return true;
		}

		return false;
	}
}

public class VisualMarkerRequest : MarkerBase
{
	public enum MarkerCommands : ushort { Unknown = 0, Add, Modify, Remove, List };

	[JsonConverter(typeof(StringEnumConverter))]
	public MarkerCommands command { get; set; }

	public List<MarkerRequest> markers = null;

	public MarkerFilter filter = null;

	public override void Print()
	{
		Debug.Log("====================================");
		Debug.LogFormat("## VisualMarkers: {0}", command);
		if (markers != null)
		{
			foreach (var marker in markers)
			{
				marker.Print();
			}
		}
		Debug.Log("====================================");
	}
}

public class VisualMarkerResponse : MarkerBase
{
	public string command = string.Empty;
	public string result = string.Empty;

	public List<MarkerResponseLine> lines = null;
	public List<MarkerResponseText> texts = null;
	public List<MarkerResponseBox> boxes = null;
	public List<MarkerResponseSphere> spheres = null;

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
	public MarkerVisualizer markerVisualizer = null;

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

		var request = JsonConvert.DeserializeObject<VisualMarkerRequest>(e.Data);

		request.Print();

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
		var responseJsonData = JsonConvert.SerializeObject(response, Formatting.Indented);

		// response.Print();

		var sb = new StringBuilder(responseJsonData);
		// Debug.Log(responseJsonData);
		sb.Replace(@",""lines"":[]", "");
		sb.Replace(@",""texts"":[]", "");
		sb.Replace(@",""boxes"":[]", "");
		sb.Replace(@",""spheres"":[]", "");
		// Debug.Log(sb.ToString());

		Send(sb.ToString());
	}
}