// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using NUnit.Framework;

namespace LfMerge.AutomatedSRTests.Tests
{
	[TestFixture]
	public class CommentsTests
	{
		private const string LexemeA =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'A' } },
				'senses' : [ {
					/* no definition */
					'gloss' : { 'en' : { 'value' : 'A' } }
				} ] }";

		private const string LexemeB =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'B' } },
				'senses' : [ {
					'definition' : { 'en' : { 'value' : 'B' } },
					'gloss' : { 'en' : { 'value' : '' } }
				} ] }";

		private const string LexemeC =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'C' } },
				'senses' : [ {
					/* no definition */
					'gloss' : { 'en' : { 'value' : 'C' } }
				} ] }";

		private const string LexemeD =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'D' } },
				'senses' : [ {
					'definition' : { 'en' : { 'value' : 'D' } },
					'gloss' : { 'en' : { 'value' : '' } }
			} ] }";

		private const string LexemeE =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'E' } },
				'senses' : [ {
					'gloss' : { 'en' : { 'value' : 'E' } }
			} ] }";

		private const string LexemeF =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'F' } },
				'senses' : [ {
					'gloss' : { 'en' : { 'value' : 'F' } }
			} ] }";

		private const string LexemeG =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'G' } },
				'senses' : [ {
					'gloss' : { 'en' : { 'value' : 'G' } }
			} ] }";

		private const string LexemeH =
			// language=json
			@"{ 'lexeme': { 'fr' : { 'value' : 'H' } },
				'senses' : [ {
					'gloss' : { 'en' : { 'value' : 'H' } }
			} ] }";

		private const string CommentsA_D =
			// language=json
			@"{ 'class' : 'question',
				'ref' : 'A',
				'message' : {
					'status': '',
					'value': 'FW comment on word A'
				} },
			{ 'class' : 'question',
				'ref' : 'B',
				'message' : {
					'status': 'open',
					'value': 'Comment on word B'
				} },
			{ 'class' : 'question',
				'ref' : 'C',
				'message' : {
					'status': '',
					'value': 'Comment about new word C'
				} },
			{ 'class' : 'question',
				'ref' : 'D',
				'message' : {
					'status': 'open',
					'value': 'Comment on word D'
				} },
			{ 'class' : 'question',
				'ref' : 'A',
				'message' : {
					'status': '',
					'value': 'Comment on A, FW first'
				} },
			{ 'class' : 'question',
				'ref' : 'A',
				'message' : {
					'status': 'open',
					'value': 'Comment on A, LF second'
				} }";

		private LanguageDepotHelper _FieldWorks;
		private MongoHelper _LanguageForge;
		private WebworkHelper _webwork;

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
			TestHelper.InitializeTestEnvironment(out _FieldWorks, out _LanguageForge, out _webwork);
		}

		[TearDown]
		public void TearDown()
		{
			TestHelper.ShutdownTestEnvironment(_FieldWorks, _LanguageForge, _webwork);
		}

		/// <summary>
		/// TEST  1: FW deletes B while LF adds a comment on B.
		///
		/// What should happen: B gets deleted (and comment remains but won't be shown in UI).
		/// </summary>
		[Test]
		public void Test01_FwDeletesEntryWhileLfAddsComment([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_FieldWorks.ApplyPatches(dbVersion, 3);
			_FieldWorks.ApplyReversePatch(dbVersion.ToString(), 3, "FW deletes B");
			_webwork.ApplyPatches(dbVersion, 3);
			_LanguageForge.RestoreDatabase(dbVersion, 4);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));

			// language=json
			const string expected = @"[ { 'lexicon': [
				{ 'lexeme': { 'fr' : { 'value' : 'A' } },
					'senses' : [ {
						/* no definition */
						'gloss' : { 'en' : { 'value' : 'A' } }
				} ] }
			]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST  2: LF deletes A while FW adds a comment on A.
		///
		/// What should happen: A remains, with a comment on it.
		/// </summary>
		[Test]
		public void Test02_LfDeletesEntryWhileFwAddsComment([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_FieldWorks.ApplyPatches(dbVersion, 4);
			_webwork.ApplyPatches(dbVersion, 3);
			_LanguageForge.RestoreDatabase(dbVersion, 2);

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
			]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'FW comment on word A'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST  3/5: FW adds comment on A, LF adds comment on A at the same time. FW does S/R
		/// first, then LF does.
		///
		/// What should happen: Both comments appear.
		/// </summary>
		/// <remarks>Can we test scenario 5 separately?</remarks>
		[Test]
		public void Test03_FwAndLfAddCommentOnA_FwSyncsFirst([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_FieldWorks.ApplyPatches(dbVersion, 11);
			_webwork.ApplyPatches(dbVersion, 10);
			_LanguageForge.RestoreDatabase(dbVersion, 11);

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
			]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'FW comment on word A'
					}
				}, { 'class' : 'question',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}, { 'class' : 'question',
					'ref' : 'C',
					'message' : {
						'status': '',
						'value': 'Comment about new word C'
					}
				}, { 'class' : 'question',
					'ref' : 'D',
					'message' : {
						'status': 'open',
						'value': 'Comment on word D'
					}
				}, { 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': 'open',
						'value': 'Comment on A, LF second'
					}
				}, { 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'Comment on A, FW first'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST  4/6: FW adds comment on A, LF adds comment on A at the same time. LF does S/R
		/// first, then FW does (and finally LF again).
		///
		/// What should happen: Both comments appear.
		/// </summary>
		/// <remarks><p>Can we test scenario 6 separately?</p>
		/// <p>Since we don't test S/R of FW here, we start out with the state that LD/FW is in
		/// after FW did the S/R, i.e. LD contains both comments, but LF hasn't pulled the
		/// changes from FW yet.</p></remarks>
		[Test]
		public void Test04_FwAndLfAddCommentOnA_LfSyncsFirst([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_webwork.ApplyPatches(dbVersion, 11);
			// apply patch 13 without 11
			_webwork.ApplySinglePatch(dbVersion, 13, false);
			_webwork.RemoveNodeFromFile("Lexicon.fwstub.ChorusNotes",
				"/notes/annotation[message='Comment on A, FW first']",
				"Patch 13 without 11 (Add comment on A, LF second)");
			_FieldWorks.ApplyPatches(dbVersion, 11);
			_FieldWorks.MergeWith(_webwork);
			_LanguageForge.RestoreDatabase(dbVersion, 12);

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
			]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'FW comment on word A'
					}
				}, { 'class' : 'question',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}, { 'class' : 'question',
					'ref' : 'C',
					'message' : {
						'status': '',
						'value': 'Comment about new word C'
					}
				}, { 'class' : 'question',
					'ref' : 'D',
					'message' : {
						'status': 'open',
						'value': 'Comment on word D'
					}
				}, { 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': 'open',
						'value': 'Comment on A, LF second'
					}
				}, { 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'Comment on A, FW first'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST  7: FW adds comment on A, LF adds comment on B. FW does S/R first, then LF does.
		///
		/// What should happen: Both comments appear.
		/// </summary>
		[Test]
		public void Test07_FwAddsCommentOnA_LfAddsCommentOnB_FwSyncsFirst([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_FieldWorks.ApplyPatches(dbVersion, 4);
			_webwork.ApplyPatches(dbVersion, 3);
			_LanguageForge.RestoreDatabase(dbVersion, 4);

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
			]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}, { 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'FW comment on word A'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST  8: FW adds comment on A, LF adds comment on B. LF does S/R first, then FW does
		/// (and finally LF again).
		///
		/// What should happen: Both comments appear.
		/// </summary>
		/// <remarks>Since we don't test S/R of FW here, we start out with the state that LD/FW is
		/// in after FW did the S/R, i.e. LD contains both comments, but LF hasn't pulled the
		/// changes from FW yet.</remarks>
		[Test]
		public void Test08_FwAddsCommentOnA_LfAddsCommentOnB_LfSyncsFirst([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_webwork.ApplyPatches(dbVersion, 4);
			// apply patch 6 without 4
			_webwork.ApplySinglePatch(dbVersion, 6, false);
			_webwork.RemoveNodeFromFile("Lexicon.fwstub.ChorusNotes",
				"/notes/annotation[message='FW comment on word A']",
				"Patch 6 without 4 (Add comment on B)");
			_FieldWorks.ApplyPatches(dbVersion, 4);
			_FieldWorks.MergeWith(_webwork);

			_LanguageForge.RestoreDatabase(dbVersion, 5);

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
			]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}, { 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'FW comment on word A'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST  9a: FW adds comment on E, S/R. LF replies. S/R.
		///
		/// What should happen: LF and FW should see both comments.
		/// </summary>
		/// <remarks>Since we don't test S/R of FW here, we start out with the state that LD/FW is
		/// in after FW did the S/R.</remarks>
		[Test]
		public void Test09a_FwAddsCommentOnE_LfReplies([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_webwork.ApplyPatches(dbVersion, 17);
			_FieldWorks.ApplyPatches(dbVersion, 17);
			_LanguageForge.RestoreDatabase(dbVersion, 18);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));

			// NOTE: although Chorus stores a status on every comment when we have replies,
			// only the last status determines the overall status of the comment!

			// language=json
			var expected = $"[ {{ 'notes': [ {CommentsA_D}, " +
				@"{ 'class' : 'question',
				'ref' : 'E',
				'message' : {
					'status': '',
					'value': 'FW comment on E'
				}, 'replies': [
					{ 'message': {
						'status': 'open',
						'value': 'LF reply on E'
					} }
				] }
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST  9b: FW adds comment on E, S/R. LF replies. S/R. FW replies. S/R.
		///
		/// What should happen: LF and FW should see all three comments.
		/// </summary>
		/// <remarks>Since we don't test S/R of FW here, we start out with the state that LD/FW is
		/// in after FW did the S/R.</remarks>
		[Test]
		public void Test09b_FwAddsCommentOnE_LfReplies_FwReplies([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_webwork.ApplyPatches(dbVersion, 19);
			_LanguageForge.RestoreDatabase(dbVersion, 19);
			_FieldWorks.ApplyPatches(dbVersion, 20);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));

			// language=json
			var expected = $"[ {{ 'notes': [ {CommentsA_D}, " +
			@"{ 'class' : 'question',
				'ref' : 'E',
				'message' : {
					'status': '',
					'value': 'FW comment on E'
				}, 'replies': [
					{ 'message': {
						'status': 'open',
						'value': 'LF reply on E'
					} },
					{ 'message': {
						'status': 'open',
						'value': 'FW reply on E'
					} }
				] }
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST 21: FW adds an entry C and a comment on C at the same time.
		///
		/// What should happen: Both the comment and the entry show up.
		/// </summary>
		[Test]
		public void Test21_FwAddsEntryCAndComment([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_FieldWorks.ApplyPatches(dbVersion, 7);
			_webwork.ApplyPatches(dbVersion, 6);
			_LanguageForge.RestoreDatabase(dbVersion, 6);

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
				{ 'lexeme': { 'fr' : { 'value' : 'C' } },
					'senses' : [ {
						/* no definition */
						'gloss' : { 'en' : { 'value' : 'C' } }
				} ] },
				{ 'lexeme': { 'fr' : { 'value' : 'A' } },
					'senses' : [ {
						/* no definition */
						'gloss' : { 'en' : { 'value' : 'A' } }
				} ] }
			]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'FW comment on word A'
					}
				}, { 'class' : 'question',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}, { 'class' : 'question',
					'ref' : 'C',
					'message' : {
						'status': '',
						'value': 'Comment about new word C'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

		/// <summary>
		/// TEST 22: LF adds an entry D and a comment on D at the same time.
		///
		/// What should happen: Both the comment and the entry show up.
		/// </summary>
		[Test]
		public void Test22_LfAddsEntryDAndComment([Range(Settings.MinModelVersion, Settings.MaxModelVersion)] int dbVersion)
		{
			// Setup
			_FieldWorks.ApplyPatches(dbVersion, 7);
			_webwork.ApplyPatches(dbVersion, 7);
			_LanguageForge.RestoreDatabase(dbVersion, 9);

			// Exercise
			LfMergeHelper.Run($"--project {Settings.DbName} --action=Synchronize");

			// Verify
			Assert.That(TestHelper.SRState, Is.EqualTo("IDLE"));

			// language=json
			var expected = @"[ { 'lexicon': [ " + $"{LexemeB}, {LexemeC}, {LexemeA}, {LexemeD}" +
			@"]}, { 'notes': [
				{ 'class' : 'question',
					'ref' : 'A',
					'message' : {
						'status': '',
						'value': 'FW comment on word A'
					}
				}, { 'class' : 'question',
					'ref' : 'B',
					'message' : {
						'status': 'open',
						'value': 'Comment on word B'
					}
				}, { 'class' : 'question',
					'ref' : 'C',
					'message' : {
						'status': '',
						'value': 'Comment about new word C'
					}
				}, { 'class' : 'question',
					'ref' : 'D',
					'message' : {
						'status': 'open',
						'value': 'Comment on word D'
					}
				}
			]}]";
			VerifyMongo.AssertData(expected);

			var expectedXml = JsonToXml.Convert(expected);
			VerifyLanguageDepot.AssertFilesContain(expectedXml);
		}

	}
}
