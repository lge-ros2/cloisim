using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UGUI = UnityEngine.UI;

namespace RuntimeGizmos
{
	//To be safe, if you are changing any transforms hierarchy, such as parenting an object to something,
	//you should call ClearTargets before doing so just to be sure nothing unexpected happens... as well as call UndoRedoManager.Clear()
	//For example, if you select an object that has children, move the children elsewhere, deselect the original object, then try to add those old children to the selection, I think it wont work.

	[RequireComponent(typeof(Camera))]
	public partial class TransformGizmo : MonoBehaviour
	{
		[Header("Color properties")]
		public Color xColor = new Color(1, 0, 0, 1f);
		public Color yColor = new Color(0, 1, 0, 1f);
		public Color zColor = new Color(0, 0, 1, 1f);
		public Color allColor = new Color(.7f, .7f, .7f, 1f);
		public Color selectedColor = new Color(1, 1, 0, 1f);
		public Color hoverColor = new Color(1, .75f, 0, 1f);

		[Header("Outline properties")]
		public Color outlineColor = new Color(1.0f, 0.5f, 0.0f, 1.0f);
		[Range(1f, 10f)]
		public float outlineWidth = 5.0f;

		private AxisVectors handleLines = new AxisVectors();
		private AxisVectors handlePlanes = new AxisVectors();
		private AxisVectors handleTriangles = new AxisVectors();
		private AxisVectors handleSquares = new AxisVectors();
		private AxisVectors circlesLines = new AxisVectors();

		private HashSet<Renderer> highlightedRenderers = new HashSet<Renderer>();
		private List<Renderer> renderersBuffer = new List<Renderer>();

		private static Material lineMaterial;
		private static Material shadedMaterial;
		private static Material selectionMaskMaterial;
		private static Material selectionOutline2DMaterial;
		private static Material _gizmoCompositeMaterial;

		private static readonly int _ClipRectCountID = Shader.PropertyToID("_ClipRectCount");
		private static readonly int _ClipRectsID = Shader.PropertyToID("_ClipRects");
		private const int MaxClipRects = 64;
		private static int _clipRectUploadCapacity = MaxClipRects;

		private readonly List<Vector4> _overlayClipRects = new(MaxClipRects);
		private readonly Vector3[] _uiWorldCorners = new Vector3[4];
		private UIDocument _hudDocument;

		private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			if (camera.Equals(Camera.main))
			{
				OnPostRender();
			}
		}

		void OnPostRender()
		{
			if (mainTargetRoot == null || manuallyHandleGizmo)
			{
				return;
			}

			var camera = myCamera != null ? myCamera : Camera.main;
			CollectOverlayClipRects(camera);
			var needClip = camera != null && _overlayClipRects.Count > 0;
			RenderTexture gizmoRT = null;
			RenderBuffer savedColor = default;
			RenderBuffer savedDepth = default;

			if (needClip)
			{
				savedColor = Graphics.activeColorBuffer;
				savedDepth = Graphics.activeDepthBuffer;
				gizmoRT = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.ARGB32);
				Graphics.SetRenderTarget(gizmoRT);
				GL.Clear(true, true, Color.clear);
			}

			DrawSelectionOutline(camera);
			DrawGizmoHandles();

			if (needClip && gizmoRT != null)
			{
				Graphics.SetRenderTarget(savedColor, savedDepth);
				CompositeGizmoRT(gizmoRT);
				RenderTexture.ReleaseTemporary(gizmoRT);
			}
		}

		void CollectOverlayClipRects(Camera camera)
		{
			_overlayClipRects.Clear();

			if (camera == null)
			{
				return;
			}

			if (PIDTunerWindow.IsVisible)
			{
				AddTopLeftClipRect(PIDTunerWindow.ActiveWindowRect, camera, true);
			}

			AddUiToolkitClipRects(camera);
			AddOverlayCanvasClipRects(camera);
		}

		void AddUiToolkitClipRects(Camera camera)
		{
			if (_hudDocument == null)
			{
				var uiController = FindAnyObjectByType<UIController>();
				if (uiController != null)
				{
					_hudDocument = uiController.GetComponent<UIDocument>();
				}
			}

			var root = _hudDocument?.rootVisualElement;
			if (root == null)
			{
				return;
			}

			AddUiToolkitClipRects(root, camera);
		}

		void AddUiToolkitClipRects(VisualElement element, Camera camera)
		{
			if (_overlayClipRects.Count >= MaxClipRects || !ShouldClip(element))
			{
				return;
			}

			var allowFullscreen = element.name == "LoadingOverlay";
			if (allowFullscreen || ShouldClipSelf(element))
			{
				AddTopLeftClipRect(element.worldBound, camera, allowFullscreen);
			}

			if (allowFullscreen)
			{
				return;
			}

			foreach (var child in element.Children())
			{
				if (_overlayClipRects.Count >= MaxClipRects)
				{
					return;
				}

				AddUiToolkitClipRects(child, camera);
			}
		}

		static bool ShouldClip(VisualElement element)
		{
			if (element == null)
			{
				return false;
			}

			if (element.resolvedStyle.display == DisplayStyle.None ||
				element.resolvedStyle.visibility == Visibility.Hidden)
			{
				return false;
			}

			var rect = element.worldBound;
			return rect.width > 1f && rect.height > 1f;
		}

		static bool ShouldClipSelf(VisualElement element)
		{
			if (element is Button ||
				element is TextField ||
				element is Toggle ||
				element is EnumField ||
				element is ScrollView ||
				element is GroupBox)
			{
				return true;
			}

			var style = element.resolvedStyle;
			if (style.backgroundColor.a > 0.001f)
			{
				return true;
			}

			return (style.borderLeftWidth > 0f && style.borderLeftColor.a > 0.001f) ||
				(style.borderRightWidth > 0f && style.borderRightColor.a > 0.001f) ||
				(style.borderTopWidth > 0f && style.borderTopColor.a > 0.001f) ||
				(style.borderBottomWidth > 0f && style.borderBottomColor.a > 0.001f);
		}

		void AddOverlayCanvasClipRects(Camera camera)
		{
			var mainCanvas = Main.UIMainCanvas;
			if (mainCanvas == null)
			{
				return;
			}

			var graphics = mainCanvas.GetComponentsInChildren<UGUI.Graphic>(false);
			foreach (var graphic in graphics)
			{
				if (_overlayClipRects.Count >= MaxClipRects)
				{
					return;
				}

				if (!ShouldClip(graphic))
				{
					continue;
				}

				if (!TryGetCanvasGraphicBounds(graphic, out var screenRect))
				{
					continue;
				}

				AddNormalizedClipRect(screenRect, camera);
			}
		}

		static bool ShouldClip(UGUI.Graphic graphic)
		{
			return graphic != null &&
				graphic.enabled &&
				graphic.gameObject.activeInHierarchy &&
				!graphic.canvasRenderer.cull &&
				graphic.rectTransform.rect.width > 1f &&
				graphic.rectTransform.rect.height > 1f &&
				graphic.color.a > 0.001f;
		}

		bool TryGetCanvasGraphicBounds(UGUI.Graphic graphic, out Rect screenRect)
		{
			screenRect = default;
			var rectTransform = graphic.rectTransform;
			rectTransform.GetWorldCorners(_uiWorldCorners);

			var eventCamera = graphic.canvas != null && graphic.canvas.renderMode != RenderMode.ScreenSpaceOverlay
				? graphic.canvas.worldCamera
				: null;

			Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, _uiWorldCorners[0]);
			Vector2 max = min;
			for (var index = 1; index < _uiWorldCorners.Length; index++)
			{
				var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, _uiWorldCorners[index]);
				min = Vector2.Min(min, screenPoint);
				max = Vector2.Max(max, screenPoint);
			}

			screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
			return screenRect.width > 1f && screenRect.height > 1f;
		}

		void AddTopLeftClipRect(Rect rect, Camera camera, bool allowFullscreen = false)
		{
			var screenRect = Rect.MinMaxRect(
				rect.xMin,
				camera.pixelHeight - rect.yMax,
				rect.xMax,
				camera.pixelHeight - rect.yMin);

			AddNormalizedClipRect(screenRect, camera, allowFullscreen);
		}

		void AddNormalizedClipRect(Rect screenRect, Camera camera, bool allowFullscreen = false)
		{
			if (_overlayClipRects.Count >= MaxClipRects)
			{
				return;
			}

			var pixelWidth = camera.pixelWidth;
			var pixelHeight = camera.pixelHeight;
			if (pixelWidth <= 0 || pixelHeight <= 0)
			{
				return;
			}

			if (!allowFullscreen &&
				screenRect.width >= pixelWidth * 0.98f &&
				screenRect.height >= pixelHeight * 0.98f)
			{
				return;
			}

			var padding = 1f;// Mathf.Max(2f, outlineWidth);
			screenRect.xMin = Mathf.Clamp(screenRect.xMin - padding, 0f, pixelWidth);
			screenRect.xMax = Mathf.Clamp(screenRect.xMax + padding, 0f, pixelWidth);
			screenRect.yMin = Mathf.Clamp(screenRect.yMin - padding, 0f, pixelHeight);
			screenRect.yMax = Mathf.Clamp(screenRect.yMax + padding, 0f, pixelHeight);

			if (screenRect.width <= 1f || screenRect.height <= 1f)
			{
				return;
			}

			_overlayClipRects.Add(new Vector4(
				screenRect.xMin / pixelWidth,
				screenRect.yMin / pixelHeight,
				screenRect.xMax / pixelWidth,
				screenRect.yMax / pixelHeight));
		}

		void DrawSelectionOutline(Camera camera)
		{
			if (highlightedRenderers.Count == 0 || selectionMaskMaterial == null || selectionOutline2DMaterial == null)
				return;

			if (camera == null)
				return;

			var prevColor = Graphics.activeColorBuffer;
			var prevDepth = Graphics.activeDepthBuffer;

			var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.Default;
			var maskRT = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 0, format, RenderTextureReadWrite.Linear);

			Graphics.SetRenderTarget(maskRT);
			GL.Clear(false, true, Color.clear);

			// Step 1: Draw masks
			if (selectionMaskMaterial.SetPass(0))
			{
				foreach (var render in highlightedRenderers)
				{
					if (render == null) continue;
					Mesh mesh = null;
					var mf = render.GetComponent<MeshFilter>();
					if (mf != null)
						mesh = mf.sharedMesh;
					else
					{
						var smr = render as SkinnedMeshRenderer;
						if (smr != null)
							mesh = smr.sharedMesh;
					}
					if (mesh != null)
					{
						for (var i = 0; i < mesh.subMeshCount; i++)
						{
							Graphics.DrawMeshNow(mesh, render.transform.localToWorldMatrix, i);
						}
					}
				}
			}

			// Restore render target (either the gizmoRT or the screen)
			Graphics.SetRenderTarget(prevColor, prevDepth);

			// Step 2: Draw outline post-process
			selectionOutline2DMaterial.SetTexture("_MaskTex", maskRT);
			selectionOutline2DMaterial.SetColor("_OutlineColor", outlineColor);
			selectionOutline2DMaterial.SetFloat("_OutlineWidth", outlineWidth);

			if (selectionOutline2DMaterial.SetPass(0))
			{
				GL.PushMatrix();
				GL.LoadOrtho();
				GL.Begin(GL.QUADS);
				GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
				GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
				GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
				GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
				GL.End();
				GL.PopMatrix();
			}

			RenderTexture.ReleaseTemporary(maskRT);
		}

		void DrawGizmoHandles()
		{
			var transformingColor = isTransforming ? selectedColor : hoverColor;

			var xColor = (nearAxis == Axis.X) ? transformingColor : this.xColor;
			var yColor = (nearAxis == Axis.Y) ? transformingColor : this.yColor;
			var zColor = (nearAxis == Axis.Z) ? transformingColor : this.zColor;
			var allColor = (nearAxis == Axis.Any) ? transformingColor : this.allColor;

			var camDir = transform.forward;

			// Clear depth buffer so gizmo faces self-occlude without being hidden by scene geometry
			GL.Clear(true, false, Color.clear);

			if (shadedMaterial != null && shadedMaterial.SetPass(0))
			{
				// Axis shafts with per-face shading
				DrawQuadsShaded(handleLines.z, GetColor(TransformType.Move, this.zColor, zColor, hasTranslatingAxisPlane), camDir);
				DrawQuadsShaded(handleLines.x, GetColor(TransformType.Move, this.xColor, xColor, hasTranslatingAxisPlane), camDir);
				DrawQuadsShaded(handleLines.y, GetColor(TransformType.Move, this.yColor, yColor, hasTranslatingAxisPlane), camDir);

				// Arrow tips with per-face shading
				DrawTrianglesShaded(handleTriangles.x, GetColor(TransformType.Move, this.xColor, xColor, hasTranslatingAxisPlane), camDir);
				DrawTrianglesShaded(handleTriangles.y, GetColor(TransformType.Move, this.yColor, yColor, hasTranslatingAxisPlane), camDir);
				DrawTrianglesShaded(handleTriangles.z, GetColor(TransformType.Move, this.zColor, zColor, hasTranslatingAxisPlane), camDir);

				// Rotation circles with per-face shading
				DrawQuadsShaded(circlesLines.all, GetColor(TransformType.Rotate, this.allColor, allColor), camDir);
				DrawQuadsShaded(circlesLines.x, GetColor(TransformType.Rotate, this.xColor, xColor), camDir);
				DrawQuadsShaded(circlesLines.y, GetColor(TransformType.Rotate, this.yColor, yColor), camDir);
				DrawQuadsShaded(circlesLines.z, GetColor(TransformType.Rotate, this.zColor, zColor), camDir);
			}

			// Plane handles stay flat (semi-transparent overlays)
			if (lineMaterial.SetPass(0))
			{
				DrawQuads(handlePlanes.z, GetColor(TransformType.Move, this.zColor, zColor, planesOpacity, !hasTranslatingAxisPlane));
				DrawQuads(handlePlanes.x, GetColor(TransformType.Move, this.xColor, xColor, planesOpacity, !hasTranslatingAxisPlane));
				DrawQuads(handlePlanes.y, GetColor(TransformType.Move, this.yColor, yColor, planesOpacity, !hasTranslatingAxisPlane));
			}
		}

		void CompositeGizmoRT(RenderTexture gizmoRT)
		{
			if (_gizmoCompositeMaterial == null)
			{
				var shader = Shader.Find("Hidden/GizmoComposite");
				if (shader != null)
				{
					_gizmoCompositeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
				}
			}

			if (_gizmoCompositeMaterial == null)
				return;

			if (_overlayClipRects.Count < _clipRectUploadCapacity)
			{
				_clipRectUploadCapacity = _overlayClipRects.Count;
			}

			var clipRectCount = Mathf.Min(_overlayClipRects.Count, _clipRectUploadCapacity);
			_gizmoCompositeMaterial.SetTexture("_GizmoTex", gizmoRT);
			_gizmoCompositeMaterial.SetInt(_ClipRectCountID, clipRectCount);
			if (clipRectCount > 0)
			{
				_gizmoCompositeMaterial.SetVectorArray(_ClipRectsID, _overlayClipRects.GetRange(0, clipRectCount));
			}

			if (_gizmoCompositeMaterial.SetPass(0))
			{
				GL.PushMatrix();
				GL.LoadOrtho();
				GL.Begin(GL.QUADS);
				GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
				GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
				GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
				GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
				GL.End();
				GL.PopMatrix();
			}
		}

		Color GetColor(TransformType type, Color normalColor, Color nearColor, bool forceUseNormal = false)
		{
			return GetColor(type, normalColor, nearColor, false, 1, forceUseNormal);
		}

		Color GetColor(TransformType type, Color normalColor, Color nearColor, float alpha, bool forceUseNormal = false)
		{
			return GetColor(type, normalColor, nearColor, true, alpha, forceUseNormal);
		}

		Color GetColor(TransformType type, Color normalColor, Color nearColor, bool setAlpha, float alpha, bool forceUseNormal = false)
		{
			Color color;
			if (!forceUseNormal && TranslatingTypeContains(type, false))
			{
				color = nearColor;
			}
			else
			{
				color = normalColor;
			}

			if (setAlpha)
			{
				color.a = alpha;
			}

			return color;
		}

		void SetLines()
		{
			SetHandleLines();
			SetHandlePlanes();
			SetHandleTriangles();
			SetHandleSquares();
			SetCircles(GetAxisInfo(), circlesLines);
		}

		void SetHandleLines()
		{
			handleLines.Clear();

			if (TranslatingTypeContains(TransformType.Move))
			{
				float lineWidth = handleWidth * GetDistanceMultiplier();

				float xLineLength = 0;
				float yLineLength = 0;
				float zLineLength = 0;
				if (TranslatingTypeContains(TransformType.Move))
				{
					xLineLength = yLineLength = zLineLength = GetHandleLength(TransformType.Move);
				}

				AddCylinder(pivotPoint, axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, xLineLength, lineWidth, handleLines.x);
				AddCylinder(pivotPoint, axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, yLineLength, lineWidth, handleLines.y);
				AddCylinder(pivotPoint, axisInfo.zDirection, axisInfo.xDirection, axisInfo.yDirection, zLineLength, lineWidth, handleLines.z);
			}
		}
		int AxisDirectionMultiplier(Vector3 direction, Vector3 otherDirection)
		{
			return ExtVector3.IsInDirection(direction, otherDirection) ? 1 : -1;
		}

		void SetHandlePlanes()
		{
			handlePlanes.Clear();

			if (TranslatingTypeContains(TransformType.Move))
			{
				var pivotToCamera = myCamera.transform.position - pivotPoint;
				var cameraXSign = Mathf.Sign(Vector3.Dot(axisInfo.xDirection, pivotToCamera));
				var cameraYSign = Mathf.Sign(Vector3.Dot(axisInfo.yDirection, pivotToCamera));
				var cameraZSign = Mathf.Sign(Vector3.Dot(axisInfo.zDirection, pivotToCamera));

				var planeSize = this.planeSize;
				if (transformType == TransformType.All) { planeSize *= allMoveHandleLengthMultiplier; }
				planeSize *= GetDistanceMultiplier();

				var xDirection = axisInfo.xDirection * planeSize * cameraXSign;
				var yDirection = axisInfo.yDirection * planeSize * cameraYSign;
				var zDirection = axisInfo.zDirection * planeSize * cameraZSign;

				var xPlaneCenter = pivotPoint + (yDirection + zDirection);
				var yPlaneCenter = pivotPoint + (xDirection + zDirection);
				var zPlaneCenter = pivotPoint + (xDirection + yDirection);

				AddQuad(xPlaneCenter, axisInfo.yDirection, axisInfo.zDirection, planeSize, handlePlanes.x);
				AddQuad(yPlaneCenter, axisInfo.xDirection, axisInfo.zDirection, planeSize, handlePlanes.y);
				AddQuad(zPlaneCenter, axisInfo.xDirection, axisInfo.yDirection, planeSize, handlePlanes.z);
			}
		}

		void SetHandleTriangles()
		{
			handleTriangles.Clear();

			if (TranslatingTypeContains(TransformType.Move))
			{
				float triangleLength = triangleSize * GetDistanceMultiplier();
				AddCone(axisInfo.GetXAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, triangleLength, handleTriangles.x);
				AddCone(axisInfo.GetYAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, triangleLength, handleTriangles.y);
				AddCone(axisInfo.GetZAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.zDirection, axisInfo.yDirection, axisInfo.xDirection, triangleLength, handleTriangles.z);
			}
		}

		void AddCone(Vector3 axisEnd, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size, List<Vector3> resultsBuffer)
		{
			const int sides = 12;
			var tip = axisEnd + (axisDirection * (size * 2f));
			var radius = size / 2f;

			for (var i = 0; i < sides; i++)
			{
				var angle0 = (2f * Mathf.PI * i) / sides;
				var angle1 = (2f * Mathf.PI * (i + 1)) / sides;

				var offset0 = (axisOtherDirection1 * Mathf.Cos(angle0) + axisOtherDirection2 * Mathf.Sin(angle0)) * radius;
				var offset1 = (axisOtherDirection1 * Mathf.Cos(angle1) + axisOtherDirection2 * Mathf.Sin(angle1)) * radius;

				// Side face
				resultsBuffer.Add(axisEnd + offset0);
				resultsBuffer.Add(axisEnd + offset1);
				resultsBuffer.Add(tip);

				// Base cap
				resultsBuffer.Add(axisEnd);
				resultsBuffer.Add(axisEnd + offset1);
				resultsBuffer.Add(axisEnd + offset0);
			}
		}

		void AddCylinder(Vector3 axisStart, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float length, float width, List<Vector3> resultsBuffer)
		{
			const int sides = 8;
			var axisEnd = axisStart + (axisDirection * length);

			for (var i = 0; i < sides; i++)
			{
				var angle0 = (2f * Mathf.PI * i) / sides;
				var angle1 = (2f * Mathf.PI * (i + 1)) / sides;

				var offset0 = (axisOtherDirection1 * Mathf.Cos(angle0) + axisOtherDirection2 * Mathf.Sin(angle0)) * width;
				var offset1 = (axisOtherDirection1 * Mathf.Cos(angle1) + axisOtherDirection2 * Mathf.Sin(angle1)) * width;

				// Side quad
				resultsBuffer.Add(axisStart + offset0);
				resultsBuffer.Add(axisEnd + offset0);
				resultsBuffer.Add(axisEnd + offset1);
				resultsBuffer.Add(axisStart + offset1);
			}
		}

		void SetHandleSquares()
		{
			handleSquares.Clear();
		}

		void AddSquares(Vector3 axisStart, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size, List<Vector3> resultsBuffer)
		{
			AddQuads(axisStart, axisDirection, axisOtherDirection1, axisOtherDirection2, size, size * .5f, resultsBuffer);
		}

		void AddQuads(Vector3 axisStart, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float length, float width, List<Vector3> resultsBuffer)
		{
			Vector3 axisEnd = axisStart + (axisDirection * length);
			AddQuads(axisStart, axisEnd, axisOtherDirection1, axisOtherDirection2, width, resultsBuffer);
		}

		void AddQuads(Vector3 axisStart, Vector3 axisEnd, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float width, List<Vector3> resultsBuffer)
		{
			Square baseRectangle = GetBaseSquare(axisStart, axisOtherDirection1, axisOtherDirection2, width);
			Square baseRectangleEnd = GetBaseSquare(axisEnd, axisOtherDirection1, axisOtherDirection2, width);

			resultsBuffer.Add(baseRectangle.bottomLeft);
			resultsBuffer.Add(baseRectangle.topLeft);
			resultsBuffer.Add(baseRectangle.topRight);
			resultsBuffer.Add(baseRectangle.bottomRight);

			resultsBuffer.Add(baseRectangleEnd.bottomLeft);
			resultsBuffer.Add(baseRectangleEnd.topLeft);
			resultsBuffer.Add(baseRectangleEnd.topRight);
			resultsBuffer.Add(baseRectangleEnd.bottomRight);

			for (int i = 0; i < 4; i++)
			{
				resultsBuffer.Add(baseRectangle[i]);
				resultsBuffer.Add(baseRectangleEnd[i]);
				resultsBuffer.Add(baseRectangleEnd[i + 1]);
				resultsBuffer.Add(baseRectangle[i + 1]);
			}
		}

		void AddQuad(Vector3 axisStart, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float width, List<Vector3> resultsBuffer)
		{
			Square baseRectangle = GetBaseSquare(axisStart, axisOtherDirection1, axisOtherDirection2, width);

			resultsBuffer.Add(baseRectangle.bottomLeft);
			resultsBuffer.Add(baseRectangle.topLeft);
			resultsBuffer.Add(baseRectangle.topRight);
			resultsBuffer.Add(baseRectangle.bottomRight);
		}

		Square GetBaseSquare(Vector3 axisEnd, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size)
		{
			Square square;
			var offsetUp = (axisOtherDirection1 * size) + (axisOtherDirection2 * size);
			var offsetDown = (axisOtherDirection1 * size) - (axisOtherDirection2 * size);
			//These might not really be the proper directions, as in the bottomLeft might not really be at the bottom left...
			square.bottomLeft = axisEnd + offsetDown;
			square.topLeft = axisEnd + offsetUp;
			square.bottomRight = axisEnd - offsetUp;
			square.topRight = axisEnd - offsetDown;
			return square;
		}

		void SetCircles(AxisInfo axisInfo, AxisVectors axisVectors)
		{
			axisVectors.Clear();

			if (TranslatingTypeContains(TransformType.Rotate))
			{
				float circleLength = GetHandleLength(TransformType.Rotate);
				AddCircle(pivotPoint, axisInfo.xDirection, circleLength, axisVectors.x);
				AddCircle(pivotPoint, axisInfo.yDirection, circleLength, axisVectors.y);
				AddCircle(pivotPoint, axisInfo.zDirection, circleLength, axisVectors.z);
				AddCircle(pivotPoint, (pivotPoint - transform.position).normalized, circleLength, axisVectors.all, false);
			}
		}

		void AddCircle(Vector3 origin, Vector3 axisDirection, float size, List<Vector3> resultsBuffer, bool depthTest = true)
		{
			var up = axisDirection.normalized * size;
			var forward = Vector3.Slerp(up, -up, .5f);
			var right = Vector3.Cross(up, forward).normalized * size;

			var matrix = new Matrix4x4();

			matrix[0] = right.x;
			matrix[1] = right.y;
			matrix[2] = right.z;

			matrix[4] = up.x;
			matrix[5] = up.y;
			matrix[6] = up.z;

			matrix[8] = forward.x;
			matrix[9] = forward.y;
			matrix[10] = forward.z;

			var lastPoint = origin + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
			var nextPoint = Vector3.zero;
			float multiplier = 360f / circleDetail;

			var plane = new Plane((transform.position - pivotPoint).normalized, pivotPoint);

			float circleHandleWidth = handleWidth * GetDistanceMultiplier();

			for (int i = 0; i < circleDetail + 1; i++)
			{
				nextPoint.x = Mathf.Cos(i * multiplier * Mathf.Deg2Rad);
				nextPoint.z = Mathf.Sin(i * multiplier * Mathf.Deg2Rad);
				nextPoint.y = 0;

				nextPoint = origin + matrix.MultiplyPoint3x4(nextPoint);

				if (!depthTest || plane.GetSide(lastPoint))
				{
					Vector3 centerPoint = (lastPoint + nextPoint) * .5f;
					Vector3 upDirection = (centerPoint - origin).normalized;
					AddQuads(lastPoint, nextPoint, upDirection, axisDirection, circleHandleWidth, resultsBuffer);
				}

				lastPoint = nextPoint;
			}
		}

#if false
		void DrawLines(List<Vector3> lines, Color color)
		{
			if (lines.Count == 0)
				return;

			GL.Begin(GL.LINES);
			GL.Color(color);

			for (int i = 0; i < lines.Count; i += 2)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
			}

			GL.End();
		}
#endif

		void DrawTriangles(List<Vector3> lines, Color color)
		{
			if (lines.Count == 0)
				return;

			GL.Begin(GL.TRIANGLES);
			GL.Color(color);

			for (int i = 0; i < lines.Count; i += 3)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
				GL.Vertex(lines[i + 2]);
			}

			GL.End();
		}

		void DrawQuads(List<Vector3> lines, Color color)
		{
			if (lines.Count == 0)
				return;

			GL.Begin(GL.QUADS);
			GL.Color(color);

			for (int i = 0; i < lines.Count; i += 4)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
				GL.Vertex(lines[i + 2]);
				GL.Vertex(lines[i + 3]);
			}

			GL.End();
		}

		void DrawQuadsShaded(List<Vector3> verts, Color baseColor, Vector3 camDir)
		{
			if (verts.Count == 0)
				return;

			GL.Begin(GL.QUADS);

			for (int i = 0; i < verts.Count; i += 4)
			{
				var v0 = verts[i];
				var v1 = verts[i + 1];
				var v2 = verts[i + 2];
				var v3 = verts[i + 3];

				var normal = Vector3.Cross(v1 - v0, v3 - v0).normalized;
				var nDotL = Vector3.Dot(normal, -camDir);
				var shade = 0.35f + 0.65f * (nDotL * 0.5f + 0.5f);

				GL.Color(new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, baseColor.a));
				GL.Vertex(v0);
				GL.Vertex(v1);
				GL.Vertex(v2);
				GL.Vertex(v3);
			}

			GL.End();
		}

		void DrawTrianglesShaded(List<Vector3> verts, Color baseColor, Vector3 camDir)
		{
			if (verts.Count == 0)
				return;

			GL.Begin(GL.TRIANGLES);

			for (int i = 0; i < verts.Count; i += 3)
			{
				var v0 = verts[i];
				var v1 = verts[i + 1];
				var v2 = verts[i + 2];

				var normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
				var nDotL = Vector3.Dot(normal, -camDir);
				var shade = 0.35f + 0.65f * (nDotL * 0.5f + 0.5f);

				GL.Color(new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, baseColor.a));
				GL.Vertex(v0);
				GL.Vertex(v1);
				GL.Vertex(v2);
			}

			GL.End();
		}

#if false
		void DrawFilledCircle(List<Vector3> lines, Color color)
		{
			if (lines.Count == 0)
				return;

			Vector3 center = Vector3.zero;
			for (int i = 0; i < lines.Count; i++)
			{
				center += lines[i];
			}
			center /= lines.Count;

			GL.Begin(GL.TRIANGLES);
			GL.Color(color);

			for (int i = 0; i + 1 < lines.Count; i++)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
				GL.Vertex(center);
			}

			GL.End();
		}
#endif

		void SetMaterial()
		{
			if (lineMaterial == null)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				if (shader != null)
				{
					lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
					lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
					lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
					lineMaterial.SetInt("_ZWrite", 0);
					lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
				}
				else
				{
					lineMaterial = Resources.Load<Material>("Materials/Lines");
				}
			}

			if (shadedMaterial == null)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				if (shader != null)
				{
					shadedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
					shadedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
					shadedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					shadedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
					shadedMaterial.SetInt("_ZWrite", 1);
					shadedMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
				}
			}

			if (selectionMaskMaterial == null)
			{
				var maskShader = Shader.Find("Hidden/SelectionMaskSolid");
				if (maskShader != null)
				{
					selectionMaskMaterial = new Material(maskShader)
					{
						name = "SelectionMaskSolid"
					};
				}
			}

			if (selectionOutline2DMaterial == null)
			{
				var outlineShader = Shader.Find("Hidden/OutlinePostProcess");
				if (outlineShader != null)
				{
					selectionOutline2DMaterial = new Material(outlineShader)
					{
						name = "OutlinePostProcess"
					};
				}
			}
		}


		void GetTargetRenderers(in Transform target, List<Renderer> renderers)
		{
			renderers.Clear();
			if (target != null)
			{
				target.GetComponentsInChildren(false, renderers);
			}
		}

		void ClearAllHighlightedRenderers()
		{
			foreach (var target in targetRoots)
			{
				RemoveTargetHighlightedRenderers(target.Key);
			}

			//In case any are still left, such as if they changed parents or what not when they were highlighted.
			renderersBuffer.Clear();
			renderersBuffer.AddRange(highlightedRenderers);
			RemoveHighlightedRenderers(renderersBuffer);
		}

		void RemoveTargetHighlightedRenderers(Transform target)
		{
			GetTargetRenderers(target, renderersBuffer);

			RemoveHighlightedRenderers(renderersBuffer);
		}

		void RemoveHighlightedRenderers(in List<Renderer> renderers)
		{
			for (int i = 0; i < renderers.Count; i++)
			{
				Renderer render = renderers[i];
				if (render != null)
				{
					highlightedRenderers.Remove(render);
				}
			}

			renderers.Clear();
		}

		void AddTargetHighlightedRenderers(Transform target)
		{
			if (target != null)
			{
				GetTargetRenderers(target, renderersBuffer);

				for (int i = 0; i < renderersBuffer.Count; i++)
				{
					var render = renderersBuffer[i];

					if (!highlightedRenderers.Contains(render))
					{
						highlightedRenderers.Add(render);
					}
				}
			}
		}
	}
}