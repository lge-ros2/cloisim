/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CLOiSim.EditorTools
{
	internal sealed class TestResultsPathMirror : ICallbacks
	{
		private const string ResultsFileName = "TestResults.xml";

		public int priority => 0;

		[InitializeOnLoadMethod]
		private static void Register()
		{
			var api = ScriptableObject.CreateInstance<TestRunnerApi>();
			api.RegisterCallbacks(new TestResultsPathMirror());
		}

		public void RunStarted(ITestAdaptor testsToRun)
		{
		}

		public void RunFinished(ITestResultAdaptor result)
		{
			TryMirrorResults();
		}

		public void TestStarted(ITestAdaptor test)
		{
		}

		public void TestFinished(ITestResultAdaptor result)
		{
		}

		private static void TryMirrorResults()
		{
			var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			if (string.IsNullOrEmpty(appDataPath))
			{
				return;
			}

			var companyName = Application.companyName;
			var productName = Application.productName;
			var sourcePath = Path.Combine(
				appDataPath,
				"unity3d",
				SanitizePathSegment(companyName),
				productName,
				ResultsFileName);
			var destinationPath = Path.Combine(
				appDataPath,
				"unity3d",
				companyName,
				productName,
				ResultsFileName);

			if (!File.Exists(sourcePath) || string.Equals(sourcePath, destinationPath, StringComparison.Ordinal))
			{
				return;
			}

			try
			{
				var destinationDirectory = Path.GetDirectoryName(destinationPath);
				if (string.IsNullOrEmpty(destinationDirectory))
				{
					return;
				}

				Directory.CreateDirectory(destinationDirectory);
				if (File.Exists(destinationPath))
				{
					File.Delete(destinationPath);
				}

				File.Move(sourcePath, destinationPath);

				var sourceCompanyPath = Path.Combine(
					appDataPath,
					"unity3d",
					SanitizePathSegment(companyName));
				var sourceProductPath = Path.Combine(sourceCompanyPath, productName);
				DeleteDirectoryIfEmpty(sourceProductPath);
				DeleteDirectoryIfEmpty(sourceCompanyPath);
				Debug.Log($"[TestResultsPathMirror] Moved test results to: {destinationPath}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[TestResultsPathMirror] Failed to move test results: {ex.Message}");
			}
		}

		private static void DeleteDirectoryIfEmpty(string directoryPath)
		{
			if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
			{
				return;
			}

			if (Directory.EnumerateFileSystemEntries(directoryPath).GetEnumerator().MoveNext())
			{
				return;
			}

			Directory.Delete(directoryPath, false);
		}

		private static string SanitizePathSegment(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return value;
			}

			return value.Replace('.', '_');
		}
	}
}
#endif