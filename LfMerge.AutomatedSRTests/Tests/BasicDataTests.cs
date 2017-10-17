// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests.Tests
{
	[TestFixture]
	public class BasicDataTests
	{
		private LanguageDepotHelper _languageDepot;
		private MongoHelper _mongo;
		private WebworkHelper _webWork;

		private static string SRState
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
			_mongo = new MongoHelper($"sf_{Settings.DbName}");
			_webWork = new WebworkHelper(Settings.DbName);
		}

		[TearDown]
		public void TearDown()
		{
			_mongo.Dispose();
			_languageDepot.Dispose();
			_webWork.Dispose();
			LfMergeHelper.Cleanup();
		}

		[Test]
		public void Clone([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase("r1", dbVersion);
			_languageDepot.ApplyPatches(dbVersion, 1);
			// don't setup webwork directory

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --clone --action=Synchronize");

			// Verify
			Assert.That(SRState, Is.EqualTo("IDLE"));
		}

		[Test]
		public void NoConflicts([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase("r2", dbVersion);
			_languageDepot.ApplyPatches(dbVersion, 2);
			_webWork.ApplyPatches(dbVersion, 1);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

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
		public void EditWinsOverDelete([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase("r4", dbVersion);
			_languageDepot.ApplyPatches(dbVersion, 4);
			_webWork.ApplyPatches(dbVersion, 3);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(SRState, Is.EqualTo("IDLE"));

			// language=json
			const string expected = @"[
				{ 'lexeme': { 'fr' : { 'value' : 'lf1modified<br/>' } },
					'senses' : [ {
						'definition' : { 'en' : { 'value' : 'Word added by LF<br/>' } },
						'gloss' : { 'en' : { 'value' : '' } },
						'partOfSpeech' : { 'value' : 'n' }
					} ] },
				{ 'lexeme': { 'fr' : { 'value' : 'flexmodified' } },
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
