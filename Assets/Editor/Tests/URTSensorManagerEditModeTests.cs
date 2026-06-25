/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace CLOiSim.Tests.EditMode
{
	/// <summary>
	/// EditMode tests for URTSensorManager.
	/// GPU-heavy paths (accel struct build, fence creation) are exercised via
	/// pure-logic reflection tests wherever the real GPU context is unavailable.
	/// Tests that do allocate GraphicsBuffers clean up after themselves.
	/// </summary>
	public class URTSensorManagerGrowScratchTests
	{
		private static readonly FieldInfo s_instanceField =
			typeof(URTSensorManager).GetField("s_instance",
				BindingFlags.NonPublic | BindingFlags.Static);

		private GameObject _managerGo;

		[SetUp]
		public void SetUp()
		{
			// Inject a lightweight Manager instance so DeferScratchFree can
			// enqueue rather than immediately disposing, matching runtime behaviour.
			_managerGo = new GameObject("URTMgrTest");
			var mgr = _managerGo.AddComponent<URTSensorManager>();
			s_instanceField.SetValue(null, mgr);
		}

		[TearDown]
		public void TearDown()
		{
			s_instanceField.SetValue(null, null);
			if (_managerGo != null)
				UnityEngine.Object.DestroyImmediate(_managerGo);
		}

		[Test]
		public void GrowScratch_ReturnsCurrentBufferWhenRequiredIsZero()
		{
			using var buf = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 4, 4);
			var result = URTSensorManager.GrowScratch(buf, 0UL);
			Assert.That(result, Is.SameAs(buf));
		}

		[Test]
		public void GrowScratch_ReturnsCurrentBufferWhenCapacityAlreadySufficient()
		{
			// 8 ints × 4 bytes = 32 bytes capacity
			using var buf = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 8, 4);
			var result = URTSensorManager.GrowScratch(buf, 32UL);
			Assert.That(result, Is.SameAs(buf));
		}

		[Test]
		public void GrowScratch_ReturnsNullWhenBothCurrentAndRequiredAreNull()
		{
			var result = URTSensorManager.GrowScratch(null, 0UL);
			Assert.That(result, Is.Null);
		}

		[Test]
		public void GrowScratch_AllocatesNewBufferWithAtLeastTwoXHeadroomWhenInsufficient()
		{
			// 1 int = 4 bytes capacity — far below required 200
			var small = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, 4);
			const ulong required = 200UL;

			var grown = URTSensorManager.GrowScratch(small, required);
			try
			{
				Assert.That(grown, Is.Not.SameAs(small), "Must return a new, larger buffer");
				var grownBytes = (ulong)((long)grown.count * grown.stride);
				Assert.That(grownBytes, Is.GreaterThanOrEqualTo(required),
					"Grown buffer must satisfy the requirement");
				Assert.That(grownBytes, Is.GreaterThanOrEqualTo(required * 2),
					"Grown buffer must include 2x headroom");
			}
			finally
			{
				grown?.Dispose();
				// small was handed off to DeferScratchFree; the deferred queue on
				// the test instance holds it — DestroyImmediate in TearDown clears it.
			}
		}

		[Test]
		public void GrowScratch_NullCurrentBuffer_AllocatesNewBuffer()
		{
			const ulong required = 64UL;
			var grown = URTSensorManager.GrowScratch(null, required);
			try
			{
				Assert.That(grown, Is.Not.Null);
				var grownBytes = (ulong)((long)grown.count * grown.stride);
				Assert.That(grownBytes, Is.GreaterThanOrEqualTo(required));
			}
			finally
			{
				grown?.Dispose();
			}
		}
	}

	public class URTSensorManagerBackendSelectionTests
	{
		private static readonly MethodInfo s_selectBackend =
			typeof(URTSensorManager).GetMethod("SelectBackend",
				BindingFlags.NonPublic | BindingFlags.Static);

		private string _savedEnv;

		[SetUp]
		public void SaveEnv() =>
			_savedEnv = Environment.GetEnvironmentVariable("CLOISIM_URT_BACKEND");

		[TearDown]
		public void RestoreEnv() =>
			Environment.SetEnvironmentVariable("CLOISIM_URT_BACKEND", _savedEnv);

		[Test]
		public void SelectBackend_ComputeOverride_ReturnsComputeBackend()
		{
			Environment.SetEnvironmentVariable("CLOISIM_URT_BACKEND", "compute");
			var backend = s_selectBackend.Invoke(null, null);
			// RayTracingBackend.Compute == 1 by enum definition
			Assert.That((int)backend, Is.EqualTo(1));
		}

		[Test]
		public void SelectBackend_UnknownOverride_FallsBackToDefaultSelection()
		{
			Environment.SetEnvironmentVariable("CLOISIM_URT_BACKEND", "unknown_value");
			// Should not throw; returns Compute or Hardware depending on system.
			Assert.DoesNotThrow(() => s_selectBackend.Invoke(null, null));
		}

		[Test]
		public void SelectBackend_EmptyEnvVar_DoesNotThrow()
		{
			Environment.SetEnvironmentVariable("CLOISIM_URT_BACKEND", "");
			Assert.DoesNotThrow(() => s_selectBackend.Invoke(null, null));
		}
	}

	public class URTSensorManagerDoubleBufferStateTests
	{
		private static readonly FieldInfo s_instanceField =
			typeof(URTSensorManager).GetField("s_instance",
				BindingFlags.NonPublic | BindingFlags.Static);

		private static readonly FieldInfo s_readIdxField =
			typeof(URTSensorManager).GetField("_readIdx",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly FieldInfo s_writeIdxField =
			typeof(URTSensorManager).GetField("_writeIdx",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly FieldInfo s_perStructDirtyField =
			typeof(URTSensorManager).GetField("_perStructDirty",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly FieldInfo s_hasTraceFenceField =
			typeof(URTSensorManager).GetField("_hasTraceFence",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly FieldInfo s_frameOfLastTraceField =
			typeof(URTSensorManager).GetField("_frameOfLastTrace",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly MethodInfo s_isPriorTraceConsumed =
			typeof(URTSensorManager).GetMethod("IsPriorTraceConsumed",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly FieldInfo s_diagHistoryField =
			typeof(URTSensorManager).GetField("_diagHistory",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly MethodInfo s_diagRecord =
			typeof(URTSensorManager).GetMethod("DiagRecord",
				BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly FieldInfo s_diagRingSize =
			typeof(URTSensorManager).GetField("DiagRingSize",
				BindingFlags.NonPublic | BindingFlags.Static);

		private GameObject _managerGo;
		private URTSensorManager _mgr;

		[SetUp]
		public void SetUp()
		{
			_managerGo = new GameObject("URTMgrTest");
			_mgr = _managerGo.AddComponent<URTSensorManager>();
			s_instanceField.SetValue(null, _mgr);
		}

		[TearDown]
		public void TearDown()
		{
			s_instanceField.SetValue(null, null);
			if (_managerGo != null)
				UnityEngine.Object.DestroyImmediate(_managerGo);
		}

		[Test]
		public void InitialState_ReadIdxIsZeroWriteIdxIsOne()
		{
			Assert.That((int)s_readIdxField.GetValue(_mgr), Is.EqualTo(0));
			Assert.That((int)s_writeIdxField.GetValue(_mgr), Is.EqualTo(1));
		}

		[Test]
		public void InitialState_BothStructsDirty()
		{
			var dirty = (bool[])s_perStructDirtyField.GetValue(_mgr);
			Assert.That(dirty[0], Is.True, "Struct 0 must start dirty");
			Assert.That(dirty[1], Is.True, "Struct 1 must start dirty");
		}

		[Test]
		public void InitialState_NeitherStructHasTraceFence()
		{
			var hasTraceFence = (bool[])s_hasTraceFenceField.GetValue(_mgr);
			Assert.That(hasTraceFence[0], Is.False);
			Assert.That(hasTraceFence[1], Is.False);
		}

		[Test]
		public void InitialState_FrameOfLastTraceIsMinusOneForBothStructs()
		{
			var frames = (int[])s_frameOfLastTraceField.GetValue(_mgr);
			Assert.That(frames[0], Is.EqualTo(-1));
			Assert.That(frames[1], Is.EqualTo(-1));
		}

		[Test]
		public void MarkSceneDirty_SetsBothDirtyFlags()
		{
			// Clear dirty manually so we can verify MarkSceneDirty sets them
			var dirty = (bool[])s_perStructDirtyField.GetValue(_mgr);
			dirty[0] = false;
			dirty[1] = false;

			URTSensorManager.MarkSceneDirty();

			dirty = (bool[])s_perStructDirtyField.GetValue(_mgr);
			Assert.That(dirty[0], Is.True, "Struct 0 must be dirtied");
			Assert.That(dirty[1], Is.True, "Struct 1 must be dirtied");
		}

		[Test]
		public void IsPriorTraceConsumed_NeverTracedStruct_ReturnsTrue()
		{
			// _frameOfLastTrace[0] == -1 && _hasTraceFence[0] == false → consumed
			var result = (bool)s_isPriorTraceConsumed.Invoke(_mgr, new object[] { 0 });
			Assert.That(result, Is.True,
				"A struct that was never traced is trivially safe to mutate");
		}

		[Test]
		public void IsPriorTraceConsumed_BothStructsConsumedInitially()
		{
			Assert.That((bool)s_isPriorTraceConsumed.Invoke(_mgr, new object[] { 0 }), Is.True);
			Assert.That((bool)s_isPriorTraceConsumed.Invoke(_mgr, new object[] { 1 }), Is.True);
		}

		[Test]
		public void DiagRecord_RingBuffer_NeverExceedsDiagRingSize()
		{
			const int ringSize = 16; // Must match DiagRingSize constant
			const int insertions = 30;

			for (var i = 0; i < insertions; i++)
				s_diagRecord.Invoke(_mgr, new object[] { $"entry {i}" });

			var history = (Queue<string>)s_diagHistoryField.GetValue(_mgr);
			Assert.That(history.Count, Is.EqualTo(ringSize),
				"Ring buffer must cap at DiagRingSize regardless of insertion count");
		}

		[Test]
		public void DiagRecord_RingBuffer_RetainsLatestEntries()
		{
			for (var i = 0; i < 20; i++)
				s_diagRecord.Invoke(_mgr, new object[] { $"entry {i}" });

			var history = (Queue<string>)s_diagHistoryField.GetValue(_mgr);
			var entries = history.ToArray();

			// Last 16 of 20 entries should be entries 4-19
			Assert.That(entries[0], Is.EqualTo("entry 4"));
			Assert.That(entries[entries.Length - 1], Is.EqualTo("entry 19"));
		}

		[Test]
		public void AccelStructGeneration_StartsAtZero()
		{
			// Generation is per-instance; fresh component should start at 0
			var genField = typeof(URTSensorManager).GetField("_rtAccelStructGeneration",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.That((int)genField.GetValue(_mgr), Is.EqualTo(0));
		}
	}
}
