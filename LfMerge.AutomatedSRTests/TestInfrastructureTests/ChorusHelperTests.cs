// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Chorus.VcsDrivers.Mercurial;
using NUnit.Framework;
using SIL.Progress;

namespace LfMerge.AutomatedSRTests
{
	[TestFixture]
	public class ChorusHelperTests
	{
		private string _repoDir;

		[SetUp]
		public void SetUp()
		{
			Settings.DataDirName = "comments-data";
			Settings.DbName = "test-comment-sr";
			_repoDir = Path.Combine(Settings.TempDir, "LanguageDepot");
		}

		[TearDown]
		public void TearDown()
		{
			Settings.Cleanup();
		}

		[Test]
		public void RemoveNodeFromFile_NonexistingFileThrows()
		{
			using (var chorusHelper = new LanguageDepotHelper())
			{
				Assert.That(() => chorusHelper.RemoveNodeFromFile("NonExistingFile.txt", "/root", "File doesn't exist"),
					Throws.TypeOf<FileNotFoundException>());
			}
		}

		[Test]
		public void RemoveNodeFromFile_RemovesNodeAndAmendsTip()
		{
			using (var chorusHelper = new LanguageDepotHelper())
			{
				chorusHelper.ApplyPatches(Settings.MaxModelVersion, 6);
				var prevTip = GetTipRevision(_repoDir);

				// SUT
				chorusHelper.RemoveNodeFromFile("Lexicon.fwstub.ChorusNotes",
					"/notes/annotation[message='Comment on word B']", "Remove note for B");

				// Verify
				Assert.That(GetTipRevision(_repoDir), Is.EqualTo(prevTip));
				var notes = File.ReadAllText(Path.Combine(Settings.TempDir, "LanguageDepot",
					"Lexicon.fwstub.ChorusNotes"));
				Assert.That(notes, Is.StringContaining("FW comment on word A"));
				Assert.That(notes, Is.Not.StringContaining("Comment on word B"));
			}
		}

		[Test]
		public void RemoveNodeFromFile_NonexistingXpathDoesNothing()
		{
			using (var chorusHelper = new LanguageDepotHelper())
			{
				chorusHelper.ApplyPatches(Settings.MaxModelVersion, 6);
				var prevTip = GetTipRevision(_repoDir);

				// SUT
				Assert.That(() => chorusHelper.RemoveNodeFromFile("Lexicon.fwstub.ChorusNotes",
					"/notes/annotation[message='Comment on word X']", "Remove note fox X"),
					Throws.Nothing);

				// Verify
				Assert.That(GetTipRevision(_repoDir), Is.EqualTo(prevTip));
				var notes = File.ReadAllText(Path.Combine(_repoDir,
					"Lexicon.fwstub.ChorusNotes"));
				Assert.That(notes, Is.StringContaining("FW comment on word A"));
				Assert.That(notes, Is.StringContaining("Comment on word B"));
			}
		}

		private static int GetTipRevision(string repoDir)
		{
			var result = HgRunner.Run("hg tip --template \"{rev}\"", repoDir, 10, new NullProgress());
			return int.Parse(result.StandardOutput);
		}
	}
}
