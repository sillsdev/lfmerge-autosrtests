// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	public static class TestHelper
	{
		public static string SRState
		{
			get
			{
				var stateFile = Path.Combine(LfMergeHelper.BaseDir, "state", $"{Settings.DbName}.state");
				Assert.That(File.Exists(stateFile), Is.True, $"Statefile '{stateFile}' doesn't exist");
				var stateFileContent = JObject.Parse(File.ReadAllText(stateFile));
				var state = stateFileContent["SRState"].ToString();
				return state;
			}
		}

		public static void SetupFixture(string dataDirName, string dbName)
		{
			Settings.DataDirName = dataDirName;
			Settings.DbName = dbName;
			MongoHelper.Initialize();
		}

		public static void TearDownFixture()
		{
			MongoHelper.Cleanup();
			Settings.Cleanup();
		}

		public static void InitializeTestEnvironment(out LanguageDepotHelper languageDepot,
			out MongoHelper mongo, out WebworkHelper webwork)
		{
			languageDepot = new LanguageDepotHelper();
			mongo = new MongoHelper($"sf_{Settings.DbName}");
			webwork = new WebworkHelper(Settings.DbName);
		}

		public static void ShutdownTestEnvironment(LanguageDepotHelper languageDepot,
			MongoHelper mongo, WebworkHelper webwork)
		{
			mongo.Dispose();
			languageDepot.Dispose();
			webwork.Dispose();
			LfMergeHelper.Cleanup();
		}
	}
}
