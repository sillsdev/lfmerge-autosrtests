// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	[TestFixture]
	public class Tests
	{
		private const int MinVersion = 7000068;
		private const int MaxVersion = 7000070;

		private LanguageDepotHelper _languageDepot;
		private MongoHelper _mongo;

		private static string SRState
		{
			get
			{
				var stateFile = Path.Combine(LfMergeHelper.BaseDir, "state", "autosrtests.state");
				Assert.That(File.Exists(stateFile), Is.True, $"Statefile '{stateFile}' doesn't exist");
				var stateFileContent = JObject.Parse(File.ReadAllText(stateFile));
				var state = stateFileContent["SRState"].ToString();
				return state;
			}
		}

		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			MongoHelper.Initialize();
		}

		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			MongoHelper.Cleanup();
		}

		[SetUp]
		public void Setup()
		{
			_languageDepot = new LanguageDepotHelper();
			_mongo = new MongoHelper("sf_autosrtests");
			_mongo.RestoreDatabase();
		}

		[TearDown]
		public void TearDown()
		{
			_mongo.Dispose();
			_languageDepot.Dispose();
			Settings.Cleanup();
		}

		[Test]
		public void Clone([Range(MinVersion, MaxVersion)] int dbVersion)
		{
			// Setup
			_languageDepot.ApplyPatch(Path.Combine(dbVersion.ToString(), "r1.patch"));

			// Exercise
			LfMergeHelper.Run("--project autosrtests --clone --action=Synchronize");

			// Verify
			Assert.That(SRState, Is.EqualTo("IDLE"));
		}

	}
}
