// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	[TestFixture]
	public class VerifyMongoTests
	{
		private MongoHelper _mongo;

		[SetUp]
		public void SetUp()
		{
			Settings.DataDirName = "comments-data";
			Settings.DbName = "test-comment-sr";
			MongoHelper.Initialize();
			_mongo = new MongoHelper($"sf_{Settings.DbName}");
		}

		[TearDown]
		public void TearDown()
		{
			_mongo.Dispose();
			MongoHelper.Cleanup();
			Settings.Cleanup();
		}

		[Test]
		public void AssertData()
		{
			// Setup

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
				]}, { 'notes': [
				{ 'class' : 'note',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}, { 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': 'open',
						'value': 'FW comment on word A'
					}
				}
			]}]";
			_mongo.RestoreDatabase(Settings.MaxModelVersion, 6);

			// Execute/Verify
			VerifyMongo.AssertData(expected);
		}
	}
}