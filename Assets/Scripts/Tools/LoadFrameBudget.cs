/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using UE = UnityEngine;

/// <summary>
/// Frame-budgeted waiting for load-time coroutines (main thread only).
/// Instead of paying one whole frame for every background wait, the main thread
/// blocks briefly on the task while the current frame still has budget and only
/// yields once the budget is used up.
/// Budget per frame: CLOISIM_LOAD_FRAME_BUDGET_MS (default 33ms).
/// </summary>
public static class LoadFrameBudget
{
	private const double DefaultBudgetMs = 33.0;
	private const double MaxBlockSliceMs = 4.0;

	public static readonly double BudgetMs = ReadBudgetMs();

	// Fallback frame-start tracking, used when Time.unscaledTime is not a valid frame start.
	private static int _fallbackFrame = -1;
	private static long _fallbackStartTicks;

	private static double ReadBudgetMs()
	{
		var text = Environment.GetEnvironmentVariable("CLOISIM_LOAD_FRAME_BUDGET_MS");
		if (!string.IsNullOrEmpty(text) &&
			double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) &&
			value > 0)
		{
			return value;
		}
		return DefaultBudgetMs;
	}

	/// <summary>Main-thread milliseconds spent in the current frame so far.</summary>
	public static double ElapsedInFrameMs
	{
		get
		{
			// Time.unscaledTimeAsDouble is the real-time timestamp at the beginning of the
			// current frame (valid while Update/coroutines run), so the difference to
			// realtimeSinceStartup is the time this frame has already consumed.
			var frameStart = UE.Time.unscaledTimeAsDouble;
			var elapsed = (UE.Time.realtimeSinceStartupAsDouble - frameStart) * 1000.0;
			if (frameStart > 0.0 && elapsed >= 0.0)
			{
				return elapsed;
			}

			// Not in a regular frame (e.g. before the first frame): measure from the first query of the frame.
			var now = Stopwatch.GetTimestamp();
			var frame = UE.Time.frameCount;
			if (frame != _fallbackFrame)
			{
				_fallbackFrame = frame;
				_fallbackStartTicks = now;
			}
			return (now - _fallbackStartTicks) * 1000.0 / Stopwatch.Frequency;
		}
	}

	public static double RemainingMs => BudgetMs - ElapsedInFrameMs;

	/// <summary>True when the current frame has used up its budget.</summary>
	public static bool ShouldYield => ElapsedInFrameMs >= BudgetMs;

	/// <summary>
	/// Returns true when the task is completed. While the frame still has budget it blocks
	/// (in slices of at most 4ms) on the task's wait handle instead of returning immediately,
	/// so short background jobs finish without costing a frame. Returns false when the budget
	/// is exhausted and the caller should yield a frame.
	/// Uses the wait handle rather than Task.Wait to avoid inlining the task on the main thread.
	/// </summary>
	public static bool TryWait(Task task)
	{
		while (!task.IsCompleted)
		{
			var remaining = RemainingMs;
			if (remaining <= 0.0)
			{
				return false;
			}

			var sliceMs = (int)Math.Ceiling(Math.Min(MaxBlockSliceMs, remaining));
			((IAsyncResult)task).AsyncWaitHandle.WaitOne(sliceMs);
		}
		return true;
	}

	/// <summary>Coroutine form of <see cref="TryWait"/>: finishes when the task is completed.</summary>
	public static IEnumerator WaitFor(Task task)
	{
		while (!TryWait(task))
		{
			yield return null;
		}
	}

	/// <summary>Gives the frame back only if main-thread work already used up the budget.</summary>
	public static IEnumerator YieldIfOverBudget()
	{
		if (ShouldYield)
		{
			yield return null;
		}
	}
}
