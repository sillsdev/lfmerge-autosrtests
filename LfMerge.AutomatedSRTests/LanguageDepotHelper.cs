// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System.IO;
using Palaso.IO;

namespace LfMerge.AutomatedSRTests
{
	public class LanguageDepotHelper: ChorusHelper
	{
		private readonly bool _keepDir;

		public LanguageDepotHelper(bool keepDir = false)
		{
			_keepDir = keepDir;
			RepoDir = Path.Combine(Settings.TempDir, "LanguageDepot");
			LdDirectory = RepoDir;
			Directory.CreateDirectory(RepoDir);
			Initialize();
		}

		#region Dispose functionality
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (!disposing)
				return;

			if (string.IsNullOrEmpty(RepoDir) || _keepDir)
				return;

			DirectoryUtilities.DeleteDirectoryRobust(RepoDir);
			RepoDir = null;
		}

		#endregion

		public static string LdDirectory { get; private set; }
	}
}
