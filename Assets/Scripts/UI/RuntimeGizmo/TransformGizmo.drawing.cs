using UnityEngine;
using System.Collections.Generic;

namespace RuntimeGizmos
{
	//To be safe, if you are changing any transforms hierarchy, such as parenting an object to something,
	//you should call ClearTargets before doing so just to be sure nothing unexpected happens... as well as call UndoRedoManager.Clear()
	//For example, if you select an object that has children, move the children elsewhere, deselect the original object, then try to add those old children to the selection, I think it wont work.

	[RequireComponent(typeof(Camera))]
	public partial class TransformGizmo : MonoBehaviour
	{
		[Header("Color properties")]
		public Color xColor = new Color(1, 0, 0, 0.8f);
		public Color yColor = new Color(0, 1, 0, 0.8f);
		public Color zColor = new Color(0, 0, 1, 0.8f);
		public Color allColor = new Color(.7f, .7f, .7f, 0.8f);
		public Color selectedColor = new Color(1, 1, 0, 0.8f);
		public Color hoverColor = new Color(1, .75f, 0, 0.8f);


		private AxisVectors handleLines = new AxisVectors();
		private AxisVectors handlePlanes = new AxisVectors();
		private AxisVectors handleTriangles = new AxisVectors();
		private AxisVectors handleSquares = new AxisVectors();
		private AxisVectors circlesLines = new AxisVectors();

		private HashSet<Renderer> highlightedRenderers = new HashSet<Renderer>();
		private List<Renderer> renderersBuffer = new List<Renderer>();
		private List<Material> materialsBuffer = new List<Material>();

		private static Material lineMaterial;
		private static Material outlineMaterial;

		void OnPostRender()
		{
			if (mainTargetRoot == null || manuallyHandleGizmo)
			{
				return;
			}

			lineMaterial.SetPass(0);

			var transformingColor = (isTransforming)? selectedColor : hoverColor;

			Color xColor = (nearAxis == Axis.X)? transformingColor:this.xColor;
			Color yColor = (nearAxis == Axis.Y)? transformingColor:this.yColor;
			Color zColor = (nearAxis == Axis.Z)? transformingColor:this.zColor;
			Color allColor = (nearAxis == Axis.Any)? transformingColor:this.allColor;

			// Note: The order of drawing the axis decides what gets drawn over what.
			DrawQuads(handleLines.z, GetColor(TransformType.Move, this.zColor, zColor, hasTranslatingAxisPlane));
			DrawQuads(handleLines.x, GetColor(TransformType.Move, this.xColor, xColor, hasTranslatingAxisPlane));
			DrawQuads(handleLines.y, GetColor(TransformType.Move, this.yColor, yColor, hasTranslatingAxisPlane));

			DrawTriangles(handleTriangles.x, GetColor(TransformType.Move, this.xColor, xColor, hasTranslatingAxisPlane));
			DrawTriangles(handleTriangles.y, GetColor(TransformType.Move, this.yColor, yColor, hasTranslatingAxisPlane));
			DrawTriangles(handleTriangles.z, GetColor(TransformType.Move, this.zColor, zColor, hasTranslatingAxisPlane));

			DrawQuads(handlePlanes.z, GetColor(TransformType.Move, this.zColor, zColor, planesOpacity, !hasTranslatingAxisPlane));
			DrawQuads(handlePlanes.x, GetColor(TransformType.Move, this.xColor, xColor, planesOpacity, !hasTranslatingAxisPlane));
			DrawQuads(handlePlanes.y, GetColor(TransformType.Move, this.yColor, yColor, planesOpacity, !hasTranslatingAxisPlane));

			DrawQuads(circlesLines.all, GetColor(TransformType.Rotate, this.allColor, allColor));
			DrawQuads(circlesLines.x, GetColor(TransformType.Rotate, this.xColor, xColor));
			DrawQuads(circlesLines.y, GetColor(TransformType.Rotate, this.yColor, yColor));
			DrawQuads(circlesLines.z, GetColor(TransformType.Rotate, this.zColor, zColor));
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

				AddQuads(pivotPoint, axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, xLineLength, lineWidth, handleLines.x);
				AddQuads(pivotPoint, axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, yLineLength, lineWidth, handleLines.y);
				AddQuads(pivotPoint, axisInfo.zDirection, axisInfo.xDirection, axisInfo.yDirection, zLineLength, lineWidth, handleLines.z);
			}
		}
		int AxisDirectionMultiplier(Vector3 direction, Vector3 otherDirection)
		{
			return ExtVector3.IsInDirection(direction, otherDirection)? 1 : -1;
		}

		void SetHandlePlanes()
		{
			handlePlanes.Clear();

			if (TranslatingTypeContains(TransformType.Move))
			{
				Vector3 pivotToCamera = myCamera.transform.position - pivotPoint;
				float cameraXSign = Mathf.Sign(Vector3.Dot(axisInfo.xDirection, pivotToCamera));
				float cameraYSign = Mathf.Sign(Vector3.Dot(axisInfo.yDirection, pivotToCamera));
				float cameraZSign = Mathf.Sign(Vector3.Dot(axisInfo.zDirection, pivotToCamera));

				float planeSize = this.planeSize;
				if (transformType == TransformType.All) { planeSize *= allMoveHandleLengthMultiplier; }
				planeSize *= GetDistanceMultiplier();

				Vector3 xDirection = (axisInfo.xDirection * planeSize) * cameraXSign;
				Vector3 yDirection = (axisInfo.yDirection * planeSize) * cameraYSign;
				Vector3 zDirection = (axisInfo.zDirection * planeSize) * cameraZSign;

				Vector3 xPlaneCenter = pivotPoint + (yDirection + zDirection);
				Vector3 yPlaneCenter = pivotPoint + (xDirection + zDirection);
				Vector3 zPlaneCenter = pivotPoint + (xDirection + yDirection);

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
				AddTriangles(axisInfo.GetXAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, triangleLength, handleTriangles.x);
				AddTriangles(axisInfo.GetYAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, triangleLength, handleTriangles.y);
				AddTriangles(axisInfo.GetZAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.zDirection, axisInfo.yDirection, axisInfo.xDirection, triangleLength, handleTriangles.z);
			}
		}

		void AddTriangles(Vector3 axisEnd, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size, List<Vector3> resultsBuffer)
		{
			Vector3 endPoint = axisEnd + (axisDirection * (size * 2f));
			Square baseSquare = GetBaseSquare(axisEnd, axisOtherDirection1, axisOtherDirection2, size / 2f);

			resultsBuffer.Add(baseSquare.bottomLeft);
			resultsBuffer.Add(baseSquare.topLeft);
			resultsBuffer.Add(baseSquare.topRight);
			resultsBuffer.Add(baseSquare.topLeft);
			resultsBuffer.Add(baseSquare.bottomRight);
			resultsBuffer.Add(baseSquare.topRight);

			for (int i = 0; i < 4; i++)
			{
				resultsBuffer.Add(baseSquare[i]);
				resultsBuffer.Add(baseSquare[i + 1]);
				resultsBuffer.Add(endPoint);
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
			var offsetUp = ((axisOtherDirection1 * size) + (axisOtherDirection2 * size));
			var offsetDown = ((axisOtherDirection1 * size) - (axisOtherDirection2 * size));
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
				nextPoint.x = Mathf.Cos((i * multiplier) * Mathf.Deg2Rad);
				nextPoint.z = Mathf.Sin((i * multiplier) * Mathf.Deg2Rad);
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

		void SetMaterial()
		{
			if (lineMaterial == null)
			{
				lineMaterial = new Material(Shader.Find("Custom/Lines"));
				outlineMaterial = new Material(Shader.Find("Custom/Outline"));
			}
		}


		void GetTargetRenderers(in Transform target, List<Renderer> renderers)
		{
			renderers.Clear();
			if (target != null)
			{
				target.GetComponentsInChildren<Renderer>(false, renderers);
			}
		}

		void ClearAllHighlightedRenderers()
		{
			foreach(var target in targetRoots)
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
					materialsBuffer.Clear();
					materialsBuffer.AddRange(render.sharedMaterials);

					if (materialsBuffer.Contains(outlineMaterial))
					{
						materialsBuffer.Remove(outlineMaterial);
						render.materials = materialsBuffer.ToArray();
					}
				}

				highlightedRenderers.Remove(render);
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
						materialsBuffer.Clear();
						materialsBuffer.AddRange(render.sharedMaterials);

						if (!materialsBuffer.Contains(outlineMaterial))
						{
							materialsBuffer.Add(outlineMaterial);
							render.materials = materialsBuffer.ToArray();
						}

						highlightedRenderers.Add(render);
					}
				}

				materialsBuffer.Clear();
			}
		}
	}
}