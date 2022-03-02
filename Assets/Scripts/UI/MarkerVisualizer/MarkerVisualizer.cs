/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using TMPro;

public partial class MarkerVisualizer : MonoBehaviour
{
	private UnityEvent responseEvent = new UnityEvent();
	private const string targetRootName = "Markers";
	private const string mainCameraName = "Main Camera";
	private const string commonShaderName = "UI/Unlit/Text";
	private Shader commonShader = null;
	private Camera mainCamera = null;

	private GameObject rootMarkers = null;
	private Hashtable registeredMarkers = new Hashtable();
	private Hashtable registeredObjectsForFollowingText = new Hashtable();
	private Hashtable followingTextMarkers = new Hashtable();

#region Request
	private VisualMarkerRequest request = null;
#endregion

#region Response
	private VisualMarkerResponse response = new VisualMarkerResponse();
#endregion

	void Awake()
	{
		rootMarkers = GameObject.Find(targetRootName);
		commonShader = Shader.Find(commonShaderName);
		mainCamera = GameObject.Find(mainCameraName).GetComponent<Camera>();
	}

	void Start()
	{
		StartCoroutine(HandleFollowingText());
	}

	void LateUpdate()
	{
		if (request != null && !request.command.Equals(VisualMarkerRequest.MarkerCommands.Unknown))
		{
			StartCoroutine(HandleRequsetMarkers());
		}
	}

	void OnDestroy()
	{
		StopCoroutine(HandleFollowingText());
	}

	private IEnumerator HandleFollowingText()
	{
		const float UpdatePeriodForFollowingText = 0.3f;
		var waitForSecs = new WaitForSeconds(UpdatePeriodForFollowingText);
		var newPos = Vector3.zero;
		while (true)
		{
			foreach (DictionaryEntry textMarker in followingTextMarkers)
			{
				yield return null;

				// Look at camera
				var textObject = (textMarker.Value as TextMeshPro).gameObject;
				textObject.transform.LookAt(mainCamera.transform);

				yield return null;

				// Text marker follows Objects
				var markerName = textObject.name;
				var followingTargetObject = registeredObjectsForFollowingText[markerName] as GameObject;

				yield return null;

				if (followingTargetObject != null)
				{
					var rectTransform = textObject.GetComponent<RectTransform>();
					var followingObjectPosition = followingTargetObject.transform.position;
					var textPosition = rectTransform.localPosition;

					newPos.Set(followingObjectPosition.x, textPosition.y, followingObjectPosition.z);
					rectTransform.position = newPos;
				}
				yield return null;
			}

			yield return waitForSecs;
		}
	}

	public void RegisterResponseAction(in UnityAction call)
	{
		if (responseEvent != null)
		{
			responseEvent.AddListener(call);
		}
		else
		{
			Debug.LogWarning("event is not ready!!");
		}
	}

	private void DoneMarkerRequested(in VisualMarkerRequest.MarkerCommands command, in bool result)
	{
		response.command = command.ToString().ToLower();
		response.result = (result)? SimulationService.SUCCESS : SimulationService.FAIL;
		responseEvent.Invoke();

		request = null; // remove requested message
	}

	private void SetDefaultMeshRenderer(in Renderer renderer)
	{
		if (renderer != null)
		{
			renderer.material = new Material(commonShader);
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			renderer.lightProbeUsage = LightProbeUsage.Off;
			renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
		}
	}

	private IEnumerator HandleRequsetMarkers()
	{
		if (request == null)
		{
			yield return null;
		}

		var result = false;

		switch (request.command)
		{
			case VisualMarkerRequest.MarkerCommands.Add:
				result = AddMarkers();
				break;

			case VisualMarkerRequest.MarkerCommands.Modify:
				result = ModifyMarkers();
				break;

			case VisualMarkerRequest.MarkerCommands.Remove:
				result = RemoveMarkers();
				break;

			case VisualMarkerRequest.MarkerCommands.List:
				result = ListMarkers();
				break;

			case VisualMarkerRequest.MarkerCommands.Unknown:
				break;

			default:
				Debug.Log("invalid type");
				break;
		}


		DoneMarkerRequested(request.command, result);

		yield return null;
	}

	public bool PushRequsetMarkers(in VisualMarkerRequest markerRequest)
	{
		if (markerRequest.command.Equals(VisualMarkerRequest.MarkerCommands.List) && markerRequest.markers.Count > 0)
		{
			request = null;
			response.command = string.Empty;
			response.result = SimulationService.FAIL;
			response.lines = null;
			response.texts = null;
			response.boxes = null;
			response.spheres = null;
			return false;
		}

		request = markerRequest;

		return true;
	}

	public VisualMarkerResponse GetResponseMarkers()
	{
		return response;
	}
}