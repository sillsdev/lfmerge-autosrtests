// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests.Tests
{
	[TestFixture]
	public class CommentsTests
	{
		private LanguageDepotHelper _languageDepot;
		private MongoHelper _mongo;
		private WebworkHelper _webWork;

		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			TestHelper.SetupFixture("comments-data", "test-comment-sr");
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

		/// <summary>
		/// TEST  1: FW deletes B while LF adds a comment on B.
		///
		/// What should happen: B remains, with a comment on it.
		/// </summary>
		[Test]
		[Ignore("WIP: This test is not complete yet")]
		public void FwDeletesEntryWhileLfAddsComment([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_mongo.RestoreDatabase(dbVersion, 5);
			_languageDepot.ApplyPatches(dbVersion, 2);
			_webWork.ApplyPatches(dbVersion, 3);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));

			// language=json
			const string expected = @"[ { 'lexicon': [
				{ 'lexeme': { 'fr' : { 'value' : 'B' } },
					'senses' : [ {
						'definition' : { 'en' : { 'value' : 'B' } },
						'gloss' : { 'en' : { 'value' : '' } }
					} ] },
				{ 'lexeme': { 'fr' : { 'value' : 'A' } },
					'senses' : [ {
						/* no definition */
						'gloss' : { 'en' : { 'value' : 'A' } }
					} ] }
				]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

	}
}
