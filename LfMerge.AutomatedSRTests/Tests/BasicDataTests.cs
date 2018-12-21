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

		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			TestHelper.SetupFixture("data", "autosrtests");
		}

		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			TestHelper.TearDownFixture();
		}

		[SetUp]
		public void Setup()
		{
			TestHelper.InitializeTestEnvironment(out _languageDepot, out _mongo, out _webWork);
		}

		[TearDown]
		public void TearDown()
		{
			TestHelper.ShutdownTestEnvironment(_languageDepot, _mongo, _webWork);
		}

		[Test]
		public void Clone([ValueSource(typeof(ModelVersionValue), nameof(ModelVersionValue.GetValues))] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase(dbVersion, 1);
			_languageDepot.ApplyPatches(dbVersion, 1);
			// don't setup webwork directory

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --clone --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));
		}

		[Test]
		public void NoConflicts([ValueSource(typeof(ModelVersionValue), nameof(ModelVersionValue.GetValues))] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase(dbVersion, 2);
			_languageDepot.ApplyPatches(dbVersion, 2);
			_webWork.ApplyPatches(dbVersion, 1);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));
			// language=json
			const string expected = @"[ { 'lexicon': [
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
				]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		[Test]
		public void EditWinsOverDelete([ValueSource(typeof(ModelVersionValue), nameof(ModelVersionValue.GetValues))] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase(dbVersion, 4);
			_languageDepot.ApplyPatches(dbVersion, 4);
			_webWork.ApplyPatches(dbVersion, 3);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));

			// language=json
			const string expected = @"[ { 'lexicon': [
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
				]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

	}
}
