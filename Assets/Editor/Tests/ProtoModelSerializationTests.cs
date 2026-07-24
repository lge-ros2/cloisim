/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using NUnit.Framework;
using ProtoBuf;
using messages = cloisim.msgs;

namespace CLOiSim.Tests.EditMode
{
	/// <summary>
	/// Verifies that CloisimProtoModel (precompiled, IL2CPP-safe) produces the exact
	/// same wire bytes as ProtoBuf.Serializer's reflection-based RuntimeTypeModel.Default
	/// (Mono-only, used here purely as an oracle). If this test passes, the compiled
	/// model is a drop-in replacement for the reflection path with no wire-format
	/// drift, so cloisim_ros keeps parsing identical bytes regardless of which backend
	/// produced them.
	/// </summary>
	public class ProtoModelSerializationTests
	{
		private static readonly CloisimProtoModel Model = new CloisimProtoModel();

		private static byte[] SerializeCompiled<T>(T instance)
		{
			using var stream = new MemoryStream();
			Model.Serialize(stream, instance);
			return stream.ToArray();
		}

		private static byte[] SerializeReflection<T>(T instance)
		{
			using var stream = new MemoryStream();
			Serializer.Serialize(stream, instance);
			return stream.ToArray();
		}

		[Test]
		public void WorldStatistics_CompiledAndReflectionModel_ProduceIdenticalBytes()
		{
			var instance = new messages.WorldStatistics
			{
				Header = new messages.Header { Stamp = new messages.Time { Sec = 12, Nsec = 345 } },
				SimTime = new messages.Time { Sec = 12, Nsec = 345 },
				RealTime = new messages.Time { Sec = 12, Nsec = 999 },
				Paused = false,
				Iterations = 100,
				ModelCount = 3,
				RealTimeFactor = 1.0d,
				Stepping = false,
			};

			var compiled = SerializeCompiled(instance);
			var reflected = SerializeReflection(instance);

			Assert.That(compiled, Is.EqualTo(reflected));
		}

		[Test]
		public void WorldStatistics_RoundTripsThroughCompiledModel()
		{
			var instance = new messages.WorldStatistics
			{
				Header = new messages.Header { Stamp = new messages.Time { Sec = 1, Nsec = 2 } },
				SimTime = new messages.Time { Sec = 1, Nsec = 2 },
				Iterations = 555,
				ModelCount = 9,
				RealTimeFactor = 0.5d,
			};

			var bytes = SerializeCompiled(instance);

			using var stream = new MemoryStream(bytes);
			var result = Model.Deserialize<messages.WorldStatistics>(stream, default);

			Assert.That(result.Header.Stamp.Sec, Is.EqualTo(instance.Header.Stamp.Sec));
			Assert.That(result.SimTime.Sec, Is.EqualTo(instance.SimTime.Sec));
			Assert.That(result.Iterations, Is.EqualTo(instance.Iterations));
			Assert.That(result.ModelCount, Is.EqualTo(instance.ModelCount));
			Assert.That(result.RealTimeFactor, Is.EqualTo(instance.RealTimeFactor));
		}

		[Test]
		public void Twist_CompiledAndReflectionModel_ProduceIdenticalBytes()
		{
			var instance = new messages.Twist
			{
				Linear = new messages.Vector3d { X = 1.5d, Y = -2.25d, Z = 0d },
				Angular = new messages.Vector3d { X = 0d, Y = 0d, Z = 3.14d },
			};

			var compiled = SerializeCompiled(instance);
			var reflected = SerializeReflection(instance);

			Assert.That(compiled, Is.EqualTo(reflected));
		}

		[Test]
		public void JointCmdV_CompiledAndReflectionModel_ProduceIdenticalBytes()
		{
			var instance = new messages.JointCmdV();
			instance.JointCmds.Add(new messages.JointCmd
			{
				Name = "joint_1",
				Position = new messages.Pid { TargetOptional = new messages.Double { Data = 1.23d } },
				Velocity = new messages.Pid { TargetOptional = new messages.Double { Data = 4.56d } },
			});

			var compiled = SerializeCompiled(instance);
			var reflected = SerializeReflection(instance);

			Assert.That(compiled, Is.EqualTo(reflected));
		}
	}
}
