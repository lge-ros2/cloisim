/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System;

namespace Segmentation
{
	public class Tag : MonoBehaviour
	{
		[SerializeField]
		private bool _hide = false;

		[SerializeField]
		private string _className = string.Empty;

		[SerializeField]
		private UInt16 _classId = 0;

		[SerializeField]
		private string _tagName = string.Empty;

		public string TagName
		{
			set => _tagName = value;
			get => (string.IsNullOrEmpty(_tagName) ? this.name : _tagName);
		}

		public int TagId
		{
			get => this.gameObject.GetInstanceID();
		}

		public int TagLayer
		{
			get => this.gameObject.layer;
		}

		public UInt16 ClassId
		{
			get => _classId;
		}

		public bool Hide
		{
			get => _hide;
			set
			{
				_hide = value;
				HideLabelForMaterialPropertyBlock(value);
			}
		}

		void OnDestroy()
		{
			// Debug.Log($"Destroy segmentation tag {this.name}");
			Main.SegmentationManager.RemoveClass(_className, this);
		}

		public void Refresh()
		{
			var mpb = new MaterialPropertyBlock();

			// Debug.Log(Main.SegmentationManager.Mode);
			var color = new Color();
			switch (Main.SegmentationManager.Mode)
			{
				case Segmentation.Manager.ReplacementMode.ObjectId:
					color = ColorEncoding.EncodeIDAsColor(TagId);
					break;

				case Segmentation.Manager.ReplacementMode.ObjectName:
					color = ColorEncoding.EncodeNameAsColor(TagName);
					break;

				case Segmentation.Manager.ReplacementMode.LayerId:
					color = ColorEncoding.EncodeLayerAsColor(TagLayer);
					break;

				default:
					color = Color.black;
					break;
			}

			_classId = (UInt16)(color.grayscale * UInt16.MaxValue);
			
			mpb.SetInt("_SegmentationValue", (int)_classId);
			mpb.SetInt("_Hide", 0);

			// Debug.Log($"{TagName}: mode={Main.SegmentationManager.Mode} color={color} {_classId}");
			// Debug.Log($"{TagName} : {grayscale} > {_classId}");

			AllocateMaterialPropertyBlock(mpb);

			UpdateClass();
		}

		private void UpdateClass()
		{
			switch (Main.SegmentationManager.Mode)
			{
				case Segmentation.Manager.ReplacementMode.ObjectId:
					_className = TagId.ToString();
					break;

				case Segmentation.Manager.ReplacementMode.ObjectName:
					_className = TagName;
					break;

				case Segmentation.Manager.ReplacementMode.LayerId:
					_className = TagLayer.ToString();
					break;

				default:
					return;
			}
			// Debug.Log("UpdateClass(): " + _className);
			Main.SegmentationManager.AddClass(_className, this);
		}

		private void AllocateMaterialPropertyBlock(in MaterialPropertyBlock mpb)
		{
			var renderers = GetComponentsInChildren<Renderer>();
			// Debug.Log($"{this.name} {renderers.Length}");
			foreach (var renderer in renderers)
			{
				// Debug.Log($"{this.name} material length {renderer.materials.Length}");
				for (var i = 0; i < renderer.materials.Length; i++)
				{
					var existMpb = new MaterialPropertyBlock();
					renderer.GetPropertyBlock(existMpb, i);
					var hide = existMpb.GetInt("_Hide");
					// Debug.Log($"{this.name} {i} hide={hide}");
					mpb.SetInt("_Hide", hide);
					renderer.SetPropertyBlock(mpb, i);
				}
			}

			var terrains = GetComponentsInChildren<Terrain>();
			foreach (var terrain in terrains)
			{
				terrain.SetSplatMaterialPropertyBlock(mpb);
			}
		}

		/// <summary>
		/// Hides the label in material property block.
		/// </summary>
		/// <param name="value">if set to <c>true</c>, hide this segmentation.</param>
		public void HideLabelForMaterialPropertyBlock(in bool value)
		{
			// Debug.Log("HideLabelForMaterialPropertyBlock: " + name);
			var mpb = new MaterialPropertyBlock();
			var renderers = GetComponentsInChildren<Renderer>();
			foreach (var renderer in renderers)
			{
				for (var i = 0; i < renderer.materials.Length; i++)
				{
					renderer.GetPropertyBlock(mpb, i);
					mpb.SetInt("_Hide", value ? 1 : 0);
					renderer.SetPropertyBlock(mpb, i);
				}
			}
		}
	}
}
