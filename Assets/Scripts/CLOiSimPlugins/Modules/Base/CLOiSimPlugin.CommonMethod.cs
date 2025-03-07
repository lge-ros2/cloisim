
/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	protected void PublishTfThread(System.Object threadObject)
	{
		var paramsObject = threadObject as CLOiSimPluginThread.ParamObject;
		var publisher = GetTransport().Get<Publisher>(paramsObject.targetPort);

		if (publisher == null)
		{
			return;
		}

		var tfList = paramsObject.param as List<TF>;
		var tfMessage = new messages.TransformStamped();
		tfMessage.Header = new messages.Header();
		tfMessage.Header.Stamp = new messages.Time();
		tfMessage.Transform = new messages.Pose();
		tfMessage.Transform.Position = new messages.Vector3d();
		tfMessage.Transform.Orientation = new messages.Quaternion();

		var deviceMessage = new DeviceMessage();
		const int EmptyTfPublishPeriod = 2000;
		const float publishFrequency = 50;
		const int updatePeriod = (int)(1f / publishFrequency * 1000f);
		var updatePeriodPerEachTf = (tfList.Count == 0) ? int.MaxValue : (int)(updatePeriod / tfList.Count);
		// Debug.Log("PublishTfThread: " + updatePeriod + " , " + updatePeriodPerEachTf);

		while (PluginThread.IsRunning)
		{
			for (var i = 0; i < tfList.Count; i++)
			{
				var tf = tfList[i];

				tfMessage.Header.StrId = tf.ParentFrameID;
				tfMessage.Transform.Name = tf.ChildFrameID;

				var tfPose = tf.GetPose();
				tfMessage.Header.Stamp.SetCurrentTime();
				tfMessage.Transform.Position.Set(tfPose.position);
				tfMessage.Transform.Orientation.Set(tfPose.rotation);

				deviceMessage.SetMessage<messages.TransformStamped>(tfMessage);
				if (publisher.Publish(deviceMessage) == false)
				{
					Debug.Log(tfMessage.Header.StrId + ", " + tfMessage.Transform.Name + " error to send TF!!");
				}
				else
				{
					// Debug.Log(tfMessage.Header.Stamp.Sec + "." + tfMessage.Header.Stamp.Nsec + ": " + tfMessage.Header.StrId + ", " + tfMessage.Transform.Name);
				}
				CLOiSimPluginThread.Sleep(updatePeriodPerEachTf);
			}

			if (tfList.Count == 0)
			{
				deviceMessage.SetMessage<messages.TransformStamped>(tfMessage);
				if (publisher.Publish(deviceMessage) == false)
				{
					Debug.Log(tfMessage.Header.StrId + ", " + tfMessage.Transform.Name + " error to send TF!!");
				}
				CLOiSimPluginThread.Sleep(EmptyTfPublishPeriod);
			}
		}
		deviceMessage.Dispose();
	}

	protected static void SetCameraInfoResponse(ref DeviceMessage msCameraInfo, in messages.CameraSensor sensorInfo)
	{
		if (msCameraInfo == null || sensorInfo == null)
		{
			return;
		}

		msCameraInfo.SetMessage<messages.CameraSensor>(sensorInfo);
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

		var objectPose = new messages.Pose();
		objectPose.Name = deviceName;
		objectPose.Position = new messages.Vector3d();
		objectPose.Orientation = new messages.Quaternion();

		objectPose.Position.Set(devicePose.position);
		objectPose.Orientation.Set(devicePose.rotation);

		var objectTransformInfo = new messages.Param();
		objectTransformInfo.Name = "transform";
		objectTransformInfo.Value = new Any { Type = Any.ValueType.Pose3d, Pose3dValue = objectPose };

		if (!string.IsNullOrEmpty(parentLinkName))
		{
			var parentLinkParam = new messages.Param();
			parentLinkParam.Name = "parent_frame_id";
			parentLinkParam.Value = new Any { Type = Any.ValueType.String, StringValue = parentLinkName };
			objectTransformInfo.Childrens.Add(parentLinkParam);
		}

		msTransformInfo.SetMessage<messages.Param>(objectTransformInfo);
	}

	protected static void SetROS2CommonInfoResponse(ref DeviceMessage msRos2Info, in string topicName, in List<string> frameIdList)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "ros2";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.None };

		var ros2TopicName = new messages.Param();
		ros2TopicName.Name = "topic_name";
		ros2TopicName.Value = new Any { Type = Any.ValueType.String, StringValue = topicName };
		ros2CommonInfo.Childrens.Add(ros2TopicName);

		foreach (var frameId in frameIdList)
		{
			var ros2FrameId = new messages.Param();
			ros2FrameId.Name = "frame_id";
			ros2FrameId.Value = new Any { Type = Any.ValueType.String, StringValue = frameId };
			ros2CommonInfo.Childrens.Add(ros2FrameId);
		}

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}

	protected static void SetEmptyResponse(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info != null)
		{
			var emptyMessage = new messages.Param();
			emptyMessage.Name = "reset_odometry";
			emptyMessage.Value = new Any { Type = Any.ValueType.Boolean, BoolValue = true };
			msRos2Info.SetMessage<messages.Param>(emptyMessage);
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
				topic_name = GetPluginParameters().GetValue<string>("ros2/topic_name", _partsName);
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

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "static_transforms";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.None };

		foreach (var tf in staticTfList)
		{
			var ros2StaticTransformLink = new messages.Param();
			ros2StaticTransformLink.Name = "parent_frame_id";
			ros2StaticTransformLink.Value = new Any { Type = Any.ValueType.String, StringValue = tf.ParentFrameID };

			{
				var tfPose = tf.GetPose();

				var poseMessage = new messages.Pose();
				poseMessage.Position = new messages.Vector3d();
				poseMessage.Orientation = new messages.Quaternion();

				poseMessage.Name = tf.ChildFrameID;
				poseMessage.Position.Set(tfPose.position);
				poseMessage.Orientation.Set(tfPose.rotation);

				var ros2StaticTransformElement = new messages.Param();
				ros2StaticTransformElement.Name = "pose";
				ros2StaticTransformElement.Value = new Any { Type = Any.ValueType.Pose3d, Pose3dValue = poseMessage };

				ros2StaticTransformLink.Childrens.Add(ros2StaticTransformElement);
				// Debug.Log(poseMessage.Name + ", " + poseMessage.Position + ", " + poseMessage.Orientation);
			}

			ros2CommonInfo.Childrens.Add(ros2StaticTransformLink);
		}

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}

	protected virtual void HandleCustomRequestMessage(in string requestType, in List<messages.Param> requestChildren, ref DeviceMessage response) { }
	protected virtual void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response) { }
}