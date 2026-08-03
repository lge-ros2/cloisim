
/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;
using SDFormat;
using Stopwatch = System.Diagnostics.Stopwatch;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	protected void PublishTfThread(object threadObject)
	{
		var paramsObject = threadObject as CLOiSimPluginThread.ParamObject;
		var publisher = GetTransport().Get<Publisher>(paramsObject.targetPort);

		if (publisher == null)
		{
			return;
		}

		var tfList = paramsObject.param as List<TF>;
		var tfMessage = new messages.Pose
		{
			Header = new messages.Header()
			{
				Stamp = new messages.Time()
			},
			Position = new messages.Vector3d(),
			Orientation = new messages.Quaternion()
		};

		// Store parent frame ID in Header.Datas
		var parentFrameMap = new messages.Header.Map
		{
			Key = "parent_frame_id"
		};
		tfMessage.Header.Datas.Add(parentFrameMap);

		var deviceMessage = new DeviceMessage();
		const int EmptyTfPublishPeriod = 2000;
		const float publishFrequency = 50;
		const float updatePeriodSeconds = 1f / publishFrequency;

		// Pace each TF send across the update period (20ms @ 50Hz) to spread the
		// network load. With many TF frames (e.g. humanoid hand/finger joints) the
		// per-TF slice falls well under 1ms, where Thread.Sleep() is too coarse
		// (OS timer resolution is typically 1-15ms) to use directly: rounding it
		// down busy-spins and overflows the publisher's send high-watermark
		// ("error to send TF!!"), while clamping it up overshoots the target rate.
		// Use the same Stopwatch-based spin-yield deadline pacing as Device.cs's
		// high-res TX path (see HighResolutionThresholdPeriod) instead.
		var periodTicksPerEachTf = (tfList.Count == 0)
			? long.MaxValue
			: (long)(updatePeriodSeconds / tfList.Count * Stopwatch.Frequency);

		// Publish failures happen in bursts (e.g. subscriber disconnects while a model is being
		// torn down) and every TF in the list fails on the same pass. Logging per-TF here floods
		// the log with thousands of lines within milliseconds, so only log on the failing/recovering
		// edge of the streak.
		var wasPublishFailing = false;

		while (PluginThread.IsRunning)
		{
			for (var i = 0; i < tfList.Count; i++)
			{
				var deadline = Stopwatch.GetTimestamp() + periodTicksPerEachTf;
				var tf = tfList[i];

				parentFrameMap.Values.Clear();
				parentFrameMap.Values.Add(tf.ParentFrameID);
				tfMessage.Name = tf.ChildFrameID;

				var tfPose = tf.GetPose();
				tfMessage.Header.Stamp.SetCurrentTime();
				tfMessage.Position.Set(tfPose.position);
				tfMessage.Orientation.Set(tfPose.rotation);

				deviceMessage.SetMessage(tfMessage);
				if (PublishWithRetry(publisher, deviceMessage) == false)
				{
					if (!PluginThread.IsRunning)
					{
						break;
					}

					if (!wasPublishFailing)
					{
						Debug.LogWarning($"[{name}] error to send TF (e.g. {tf.ParentFrameID}, {tfMessage.Name}) — subscriber may be disconnected");
						wasPublishFailing = true;
					}
				}
				else
				{
					wasPublishFailing = false;
				}
				SpinWaitUntil(deadline);
			}

			if (tfList.Count == 0)
			{
				deviceMessage.SetMessage(tfMessage);
				if (PublishWithRetry(publisher, deviceMessage) == false)
				{
					if (PluginThread.IsRunning && !wasPublishFailing)
					{
						Debug.LogWarning($"[{name}] error to send TF ({tfMessage.Name}) — subscriber may be disconnected");
						wasPublishFailing = true;
					}
				}
				else
				{
					wasPublishFailing = false;
				}
				CLOiSimPluginThread.Sleep(EmptyTfPublishPeriod);
			}
		}
		deviceMessage.Dispose();
	}

	// Publishing over a non-blocking NetMQ PUB socket can transiently fail when the
	// send high-watermark is momentarily reached (slow/late subscriber). Yield briefly
	// and retry a few times before reporting an error, so short bursts don't drop TF.
	// Thread.Sleep(1) is avoided here: its actual delay depends on OS timer
	// resolution (often 1-15ms) and can dwarf the whole per-TF pacing budget,
	// starving the rest of the TF list on a single retry.
	private static bool PublishWithRetry(in Publisher publisher, in DeviceMessage message)
	{
		const int MaxRetry = 3;
		for (var attempt = 0; attempt < MaxRetry; attempt++)
		{
			if (publisher.Publish(message))
			{
				return true;
			}
			Thread.Yield();
		}
		return false;
	}

	// Stopwatch-based spin-yield wait, mirroring Device.cs's high-res TX pacing:
	// coarse Thread.Sleep(1) while there's plenty of time left, Yield as the
	// deadline nears, and a tight spin for the last fraction of a millisecond.
	private static void SpinWaitUntil(long deadlineTicks)
	{
		while (true)
		{
			var remaining = deadlineTicks - Stopwatch.GetTimestamp();
			if (remaining <= 0)
			{
				break;
			}

			if (remaining > Stopwatch.Frequency / 500) // > 2ms
			{
				Thread.Sleep(1);
			}
			else if (remaining > Stopwatch.Frequency / 2000) // > 0.5ms
			{
				Thread.Yield();
			}
			else
			{
				Thread.SpinWait(1);
			}
		}
	}

	protected static void SetCameraInfoResponse(ref DeviceMessage msCameraInfo, in messages.CameraSensor sensorInfo)
	{
		if (msCameraInfo == null || sensorInfo == null)
		{
			return;
		}

		msCameraInfo.SetMessage(sensorInfo);
	}

	protected static void SetTransformInfoResponse(ref DeviceMessage msTransformInfo, in string deviceName, in Pose devicePose)
	{
		SetTransformInfoResponse(ref msTransformInfo, deviceName, devicePose, null);
	}

	protected static void SetTransformInfoResponse(ref DeviceMessage msTransformInfo, in string deviceName, in Pose devicePose, in string parentLinkName)
	{
		if (msTransformInfo == null)
		{
			return;
		}

		var objectPose = new messages.Pose
		{
			Name = deviceName,
			Position = new messages.Vector3d(),
			Orientation = new messages.Quaternion()
		};

		objectPose.Position.Set(devicePose.position);
		objectPose.Orientation.Set(devicePose.rotation);

		var transformParam = new messages.Param();
		transformParam.Params["transform"] = new Any { Type = Any.ValueType.Pose3d, Pose3dValue = objectPose };

		if (!string.IsNullOrEmpty(parentLinkName))
		{
			var parentParam = new messages.Param();
			parentParam.Params["parent_frame_id"] = new Any { Type = Any.ValueType.String, StringValue = parentLinkName };
			transformParam.Childrens.Add(parentParam);
		}

		msTransformInfo.SetMessage(transformParam);
	}

	protected static void SetROS2CommonInfoResponse(ref DeviceMessage msRos2Info, in string topicName, in List<string> frameIdList)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2Param = new messages.Param();
		ros2Param.Params["ros2"] = new Any { Type = Any.ValueType.None };

		var topicParam = new messages.Param();
		topicParam.Params["topic_name"] = new Any { Type = Any.ValueType.String, StringValue = topicName };
		ros2Param.Childrens.Add(topicParam);

		foreach (var frameId in frameIdList)
		{
			var frameParam = new messages.Param();
			frameParam.Params["frame_id"] = new Any { Type = Any.ValueType.String, StringValue = frameId };
			ros2Param.Childrens.Add(frameParam);
		}

		msRos2Info.SetMessage(ros2Param);
	}

	protected static void SetEmptyResponse(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info != null)
		{
			var emptyMessage = new messages.Param();
			emptyMessage.Params["reset_odometry"] = new Any { Type = Any.ValueType.Boolean, BoolValue = true };
			msRos2Info.SetMessage(emptyMessage);
		}
	}

	private void SetCustomHandleRequestMessage()
	{
		_thread.HandleRequestTypeValue = delegate (in string requestType, in Any requestValue, ref DeviceMessage response)
		{
			switch (requestType)
			{
				case "request_ros2":
					SetRequestRos2Response(ref response);
					break;

				case "request_static_transforms":
					SetStaticTransformsResponse(ref response);
					break;

				default:
					HandleCustomRequestMessage(requestType, requestValue, ref response);
					break;
			}
		};

		_thread.HandleRequestTypeChildren = delegate (in string requestType, in List<messages.Param> requestChildren, ref DeviceMessage response)
		{
			HandleCustomRequestMessage(requestType, requestChildren, ref response);
		};
	}

	protected virtual void SetRequestRos2Response(ref DeviceMessage msRos2Info)
	{
		if (GetPluginParameters().IsValidNode("ros2"))
		{
			var topic_name = GetPluginParameters().GetValue<string>("ros2/topic_name[@add_parts_name_prefix='true']");
			if (string.IsNullOrEmpty(topic_name))
			{
				topic_name = GetPluginParameters().GetValue("ros2/topic_name", _partsName);
				topic_name = topic_name.Replace("{parts_name}", _partsName);
			}
			else
			{
				topic_name = _partsName + "/" + topic_name;
			}

			GetPluginParameters().GetValues<string>("ros2/frame_id", out var frameIdList);

			for (var i = 0; i < frameIdList.Count; i++)
			{
				frameIdList[i] = frameIdList[i].Replace("{parts_name}", _partsName);
			}

			SetROS2CommonInfoResponse(ref msRos2Info, topic_name, frameIdList);
		}
	}

	private void SetStaticTransformsResponse(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2Param = new messages.Param();
		ros2Param.Params["static_transforms"] = new Any { Type = Any.ValueType.None };

		foreach (var tf in _staticTfList)
		{
			var staticTfLink = new messages.Param();
			staticTfLink.Params["parent_frame_id"] = new Any { Type = Any.ValueType.String, StringValue = tf.ParentFrameID };

			{
				var tfPose = tf.GetPose();

				var poseMessage = new messages.Pose
				{
					Name = tf.ChildFrameID,
					Position = new messages.Vector3d(),
					Orientation = new messages.Quaternion()
				};

				poseMessage.Position.Set(tfPose.position);
				poseMessage.Orientation.Set(tfPose.rotation);

				var poseParam = new messages.Param();
				poseParam.Params["pose"] = new Any { Type = Any.ValueType.Pose3d, Pose3dValue = poseMessage };

				staticTfLink.Childrens.Add(poseParam);
				// Debug.Log(poseMessage.Name + ", " + poseMessage.Position + ", " + poseMessage.Orientation);
			}

			ros2Param.Childrens.Add(staticTfLink);
		}

		msRos2Info.SetMessage(ros2Param);
	}

	protected virtual void HandleCustomRequestMessage(in string requestType, in List<messages.Param> requestChildren, ref DeviceMessage response) { }
	protected virtual void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response) { }
}