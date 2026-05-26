/*
- Copyright (c) 2026 LG Electronics Inc.
- SPDX-License-Identifier: MIT
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CLOiSim.Tests.EditMode
{
    public class TfTreeValidationEditModeTests
    {
        private const string TF_STATIC_DUMP_PATH = "Assets/Editor/Tests/TestData/tf_static_good.txt";
        private const string TF_DUMP_PATH = "Assets/Editor/Tests/TestData/tf_good.txt";

        private const float MAX_REASONABLE_HAND_INTERNAL_SEGMENT_LENGTH_METERS = 0.20f;
        private const float MIN_LENGTH_FOR_RATIO_CHECK_METERS = 0.001f;
        private const float MAX_LEFT_RIGHT_SEGMENT_LENGTH_RATIO = 4.0f;

        private struct TfEdge
        {
            public string sourcePath;
            public int lineNumber;

            public string parentFrameId;
            public string childFrameId;

            public Vector3 translation;
            public Quaternion rotation;

            public float TranslationMagnitude => translation.magnitude;

            public override string ToString()
            {
                return
                    $"{parentFrameId} -> {childFrameId}, " +
                    $"translation=({translation.x:F6}, {translation.y:F6}, {translation.z:F6}), " +
                    $"length={TranslationMagnitude:F6}, " +
                    $"rotation=({rotation.x:F6}, {rotation.y:F6}, {rotation.z:F6}, {rotation.w:F6}), " +
                    $"source={sourcePath}:{lineNumber}";
            }
        }

        [Test]
        public void TfStaticDump_ChildFrameHasSingleParent()
        {
            var edges = LoadRequiredDump(TF_STATIC_DUMP_PATH);

            AssertEveryChildHasSingleParent(edges);
        }

        [Test]
        public void TfDump_ChildFrameHasSingleParent()
        {
            var edges = LoadRequiredDump(TF_DUMP_PATH);

            AssertEveryChildHasSingleParent(edges);
        }

        [Test]
        public void TfDump_HandInternalTransformsDoNotCrossBindLeftAndRightHands()
        {
            var edges = LoadRequiredDump(TF_DUMP_PATH);

            var invalidEdges = edges
                .Where(IsHandRelatedEdge)
                .Where(edge =>
                {
                    var parentSide = GetHandSide(edge.parentFrameId);
                    var childSide = GetHandSide(edge.childFrameId);

                    return parentSide != HandSide.None &&
                           childSide != HandSide.None &&
                           parentSide != childSide;
                })
                .ToList();

            Assert.That(
                invalidEdges,
                Is.Empty,
                "Hand TF edges must not cross-bind left_hand and right_hand frames.\n" +
                FormatEdges(invalidEdges));
        }

        [Test]
        public void TfDump_HandInternalTransformsStayWithinReasonablePhysicalScale()
        {
            var edges = LoadRequiredDump(TF_DUMP_PATH);

            var invalidEdges = edges
                .Where(IsHandInternalEdge)
                .Where(edge => edge.TranslationMagnitude > MAX_REASONABLE_HAND_INTERNAL_SEGMENT_LENGTH_METERS)
                .ToList();

            Assert.That(
                invalidEdges,
                Is.Empty,
                "Hand-internal TF segments are too long. " +
                "This usually means the local-name fallback resolved a parent link from the opposite hand or another duplicated scope.\n" +
                FormatEdges(invalidEdges));
        }

        [Test]
        public void TfDump_RightHandInternalTransformsDoNotContainKnownCrossScopeRegressionLength()
        {
            var edges = LoadRequiredDump(TF_DUMP_PATH);

            var invalidEdges = edges
                .Where(edge => IsRightHandFrame(edge.parentFrameId))
                .Where(edge => IsRightHandFrame(edge.childFrameId))
                .Where(edge => edge.TranslationMagnitude > MAX_REASONABLE_HAND_INTERNAL_SEGMENT_LENGTH_METERS)
                .ToList();

            Assert.That(
                invalidEdges,
                Is.Empty,
                "Right-hand internal TF contains an unrealistic segment length. " +
                "A value around 0.4m is a strong signal that the parent frame was resolved from the opposite hand.\n" +
                FormatEdges(invalidEdges));
        }

        [Test]
        public void TfDump_LeftAndRightMirroredHandSegmentsHaveComparableLengths()
        {
            var edges = LoadRequiredDump(TF_DUMP_PATH);

            var handInternalEdges = edges
                .Where(IsHandInternalEdge)
                .ToList();

            var leftBySuffix = handInternalEdges
                .Where(edge => IsLeftHandFrame(edge.parentFrameId) && IsLeftHandFrame(edge.childFrameId))
                .GroupBy(GetMirrorKey)
                .ToDictionary(group => group.Key, group => group.First());

            var rightBySuffix = handInternalEdges
                .Where(edge => IsRightHandFrame(edge.parentFrameId) && IsRightHandFrame(edge.childFrameId))
                .GroupBy(GetMirrorKey)
                .ToDictionary(group => group.Key, group => group.First());

            var failures = new List<string>();

            foreach (var pair in leftBySuffix)
            {
                if (!rightBySuffix.TryGetValue(pair.Key, out var rightEdge))
                    continue;

                var leftEdge = pair.Value;
                var leftLength = leftEdge.TranslationMagnitude;
                var rightLength = rightEdge.TranslationMagnitude;

                if (leftLength < MIN_LENGTH_FOR_RATIO_CHECK_METERS ||
                    rightLength < MIN_LENGTH_FOR_RATIO_CHECK_METERS)
                {
                    continue;
                }

                var ratio = Mathf.Max(leftLength, rightLength) / Mathf.Min(leftLength, rightLength);

                if (ratio > MAX_LEFT_RIGHT_SEGMENT_LENGTH_RATIO)
                {
                    failures.Add(
                        $"Mirror key: {pair.Key}\n" +
                        $"Left : {leftEdge}\n" +
                        $"Right: {rightEdge}\n" +
                        $"Ratio: {ratio:F3}");
                }
            }

            Assert.That(
                failures,
                Is.Empty,
                "Mirrored left/right hand TF segment lengths are not comparable. " +
                "This protects against resolving a right-hand parent from the left-hand scope, or vice versa.\n" +
                string.Join("\n\n", failures));
        }

        [Test]
        public void TfDump_HandInternalTransformsDoNotCollapseToIdentityForFingerSegments()
        {
            var edges = LoadRequiredDump(TF_DUMP_PATH);

            var invalidEdges = edges
                .Where(IsFingerSegmentEdge)
                .Where(edge => edge.TranslationMagnitude < MIN_LENGTH_FOR_RATIO_CHECK_METERS)
                .ToList();

            Assert.That(
                invalidEdges,
                Is.Empty,
                "Finger segment TFs must not collapse to zero-length identity transforms. " +
                "This protects against unresolved parent frames returning Pose.identity.\n" +
                FormatEdges(invalidEdges));
        }

        private static void AssertEveryChildHasSingleParent(IReadOnlyList<TfEdge> edges)
        {
            var invalidChildren = edges
                .GroupBy(edge => edge.childFrameId)
                .Select(group => new
                {
                    childFrameId = group.Key,
                    parents = group
                        .Select(edge => edge.parentFrameId)
                        .Distinct()
                        .OrderBy(parent => parent)
                        .ToList(),
                    edges = group.ToList()
                })
                .Where(item => item.parents.Count > 1)
                .ToList();

            Assert.That(
                invalidChildren,
                Is.Empty,
                "Each child_frame_id must have exactly one parent frame.\n" +
                string.Join(
                    "\n\n",
                    invalidChildren.Select(item =>
                        $"child_frame_id={item.childFrameId}\n" +
                        $"parents={string.Join(", ", item.parents)}\n" +
                        FormatEdges(item.edges))));
        }

        private static IReadOnlyList<TfEdge> LoadRequiredDump(string path)
        {
            Assert.That(
                File.Exists(path),
                Is.True,
                $"Required TF dump file does not exist: {path}\n" +
                "Capture a known-good dump and commit it as test data.");

            var edges = ParseTfDump(path);

            Assert.That(
                edges.Count,
                Is.GreaterThan(0),
                $"No TF edges were parsed from dump file: {path}");

            return edges;
        }

        private static IReadOnlyList<TfEdge> ParseTfDump(string path)
        {
            var lines = File.ReadAllLines(path);
            var edges = new List<TfEdge>();

            string parentFrameId = null;
            string childFrameId = null;

            float tx = 0f;
            float ty = 0f;
            float tz = 0f;

            float rx = 0f;
            float ry = 0f;
            float rz = 0f;
            float rw = 1f;

            bool hasTx = false;
            bool hasTy = false;
            bool hasTz = false;

            bool hasRx = false;
            bool hasRy = false;
            bool hasRz = false;
            bool hasRw = false;

            int edgeStartLine = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var normalizedLine = NormalizeYamlLine(lines[i]);

                if (StartsWithKey(normalizedLine, "frame_id"))
                {
                    parentFrameId = ExtractValue(normalizedLine);
                    edgeStartLine = i + 1;
                    continue;
                }

                if (StartsWithKey(normalizedLine, "child_frame_id"))
                {
                    childFrameId = ExtractValue(normalizedLine);
                    continue;
                }

                if (StartsWithKey(normalizedLine, "x"))
                {
                    var value = ParseFloatValue(normalizedLine);

                    if (!hasTx)
                    {
                        tx = value;
                        hasTx = true;
                    }
                    else if (!hasRx)
                    {
                        rx = value;
                        hasRx = true;
                    }

                    continue;
                }

                if (StartsWithKey(normalizedLine, "y"))
                {
                    var value = ParseFloatValue(normalizedLine);

                    if (!hasTy)
                    {
                        ty = value;
                        hasTy = true;
                    }
                    else if (!hasRy)
                    {
                        ry = value;
                        hasRy = true;
                    }

                    continue;
                }

                if (StartsWithKey(normalizedLine, "z"))
                {
                    var value = ParseFloatValue(normalizedLine);

                    if (!hasTz)
                    {
                        tz = value;
                        hasTz = true;
                    }
                    else if (!hasRz)
                    {
                        rz = value;
                        hasRz = true;
                    }

                    continue;
                }

                if (StartsWithKey(normalizedLine, "w"))
                {
                    rw = ParseFloatValue(normalizedLine);
                    hasRw = true;
                }

                if (parentFrameId != null &&
                    childFrameId != null &&
                    hasTx && hasTy && hasTz &&
                    hasRx && hasRy && hasRz && hasRw)
                {
                    edges.Add(new TfEdge
                    {
                        sourcePath = path,
                        lineNumber = edgeStartLine,
                        parentFrameId = NormalizeFrameId(parentFrameId),
                        childFrameId = NormalizeFrameId(childFrameId),
                        translation = new Vector3(tx, ty, tz),
                        rotation = NormalizeQuaternion(new Quaternion(rx, ry, rz, rw))
                    });

                    parentFrameId = null;
                    childFrameId = null;

                    tx = 0f;
                    ty = 0f;
                    tz = 0f;

                    rx = 0f;
                    ry = 0f;
                    rz = 0f;
                    rw = 1f;

                    hasTx = false;
                    hasTy = false;
                    hasTz = false;

                    hasRx = false;
                    hasRy = false;
                    hasRz = false;
                    hasRw = false;
                }
            }

            return edges;
        }

        private static string NormalizeYamlLine(string line)
        {
            return (line ?? string.Empty)
                .Trim()
                .Replace("\\_", "_");
        }

        private static bool StartsWithKey(string line, string key)
        {
            return line.StartsWith(key + ":", StringComparison.Ordinal);
        }

        private static string ExtractValue(string line)
        {
            var index = line.IndexOf(':');

            if (index < 0)
                return string.Empty;

            return line.Substring(index + 1).Trim();
        }

        private static float ParseFloatValue(string line)
        {
            var value = ExtractValue(line);

            Assert.That(
                float.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed),
                Is.True,
                $"Failed to parse float value from line: {line}");

            return parsed;
        }

        private static string NormalizeFrameId(string frameId)
        {
            return string.IsNullOrEmpty(frameId)
                ? string.Empty
                : frameId.Replace("::", "_").TrimStart('/');
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            var mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

            if (mag < 1e-8f)
                return Quaternion.identity;

            var inv = 1.0f / mag;

            return new Quaternion(
                q.x * inv,
                q.y * inv,
                q.z * inv,
                q.w * inv);
        }

        private static bool IsHandRelatedEdge(TfEdge edge)
        {
            return GetHandSide(edge.parentFrameId) != HandSide.None ||
                   GetHandSide(edge.childFrameId) != HandSide.None;
        }

        private static bool IsHandInternalEdge(TfEdge edge)
        {
            var parentSide = GetHandSide(edge.parentFrameId);
            var childSide = GetHandSide(edge.childFrameId);

            return parentSide != HandSide.None &&
                   childSide != HandSide.None &&
                   parentSide == childSide;
        }

        private static bool IsFingerSegmentEdge(TfEdge edge)
        {
            if (!IsHandInternalEdge(edge))
                return false;

            if (edge.parentFrameId.EndsWith("_hand_base_link", StringComparison.Ordinal))
                return false;

            return edge.childFrameId.Contains("_index_") ||
                   edge.childFrameId.Contains("_middle_") ||
                   edge.childFrameId.Contains("_ring_") ||
                   edge.childFrameId.Contains("_pinky_") ||
                   edge.childFrameId.Contains("_thumb_");
        }

        private static bool IsLeftHandFrame(string frameId)
        {
            return NormalizeFrameId(frameId).StartsWith("left_hand_", StringComparison.Ordinal);
        }

        private static bool IsRightHandFrame(string frameId)
        {
            return NormalizeFrameId(frameId).StartsWith("right_hand_", StringComparison.Ordinal);
        }

        private static HandSide GetHandSide(string frameId)
        {
            var normalized = NormalizeFrameId(frameId);

            if (normalized.StartsWith("left_hand_", StringComparison.Ordinal))
                return HandSide.Left;

            if (normalized.StartsWith("right_hand_", StringComparison.Ordinal))
                return HandSide.Right;

            return HandSide.None;
        }

        private static string GetMirrorKey(TfEdge edge)
        {
            return StripHandSidePrefix(edge.parentFrameId) + "->" + StripHandSidePrefix(edge.childFrameId);
        }

        private static string StripHandSidePrefix(string frameId)
        {
            var normalized = NormalizeFrameId(frameId);

            if (normalized.StartsWith("left_hand_", StringComparison.Ordinal))
                return normalized.Substring("left_hand_".Length);

            if (normalized.StartsWith("right_hand_", StringComparison.Ordinal))
                return normalized.Substring("right_hand_".Length);

            return normalized;
        }

        private static string FormatEdges(IEnumerable<TfEdge> edges)
        {
            return string.Join("\n", edges.Select(edge => edge.ToString()));
        }

        private enum HandSide
        {
            None,
            Left,
            Right
        }
    }
}