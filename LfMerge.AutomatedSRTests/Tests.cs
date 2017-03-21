// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	[TestFixture]
	public class Tests
	{
		public const string DbName = "autosrtests";
		private const int MinVersion = 7000068;
		private const int MaxVersion = 7000070;

		private LanguageDepotHelper _languageDepot;
		private MongoHelper _mongo;

		private static string SRState
		{
			get
			{
				var stateFile = Path.Combine(LfMergeHelper.BaseDir, "state", $"{DbName}.state");
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
			Settings.Cleanup();
		}

		[SetUp]
		public void Setup()
		{
			_languageDepot = new LanguageDepotHelper();
			_mongo = new MongoHelper($"sf_{DbName}");
		}

		[TearDown]
		public void TearDown()
		{
			_mongo.Dispose();
			_languageDepot.Dispose();
			LfMergeHelper.Cleanup();
		}

		[Test]
		public void Clone([Range(MinVersion, MaxVersion)] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase("r1", dbVersion);
			_languageDepot.ApplyPatches(dbVersion, 1);

			// Exercise
			LfMergeHelper.Run($"--project {DbName} --clone --action=Synchronize");

			// Verify
			Assert.That(SRState, Is.EqualTo("IDLE"));
		}

		[Test]
		public void NoConflicts([Range(MinVersion, MaxVersion)] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase("r2", dbVersion);
			_languageDepot.ApplyPatches(dbVersion, 2);

			// Exercise
			LfMergeHelper.Run($"--project {DbName} --clone --action=Synchronize");

			// Verify
			Assert.That(SRState, Is.EqualTo("IDLE"));
			// language=json
			const string expected = @"[
				{ 'lexeme': { 'fr' : { 'value' : 'lf1<br/>' } },
					'senses' : [ {
						'definition' : { 'en' : { 'value' : 'Word added by LF<br/>' } },
						'gloss' : { 'en' : { 'value' : '' } },
						'partOfSpeech' : { 'value' : 'n' }
					} ] },
				{ 'lexeme': { 'fr' : { 'value' : 'flex' } },
					'senses' : [ {
						/* no definition */
						'gloss' : { 'en' : { 'value' : 'created in FLEx' } },
						'partOfSpeech' : { 'value' : 'adv1' }
					} ] },
				]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		[Test]
		public void FlexDeletesLfEdits_EntryRemains([Range(MinVersion, MaxVersion)] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase("r4", dbVersion);
			_languageDepot.ApplyPatches(dbVersion, 4);

			// Exercise
			LfMergeHelper.Run($"--project {DbName} --clone --action=Synchronize");

			// Verify
			Assert.That(SRState, Is.EqualTo("IDLE"));

			// REVIEW: after restoring version 3 the "Part Of Speech" showed up as "Unknown" in
			// LF. Setting it to "Noun" resulted in having it the value "n1" in mongo!

			// language=json
			const string expected = @"[
				{ 'lexeme': { 'fr' : { 'value' : 'lf1modified<br/>' } },
					'senses' : [ {
						'definition' : { 'en' : { 'value' : 'Word added by LF<br/>' } },
						'gloss' : { 'en' : { 'value' : '' } },
						'partOfSpeech' : { 'value' : 'n1' }
					} ] },
				{ 'lexeme': { 'fr' : { 'value' : 'flex' } },
					'senses' : [ {
						/* no definition */
						'gloss' : { 'en' : { 'value' : 'created in FLEx' } },
						'partOfSpeech' : { 'value' : 'adv1' }
					} ] },
				]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

	}
}
