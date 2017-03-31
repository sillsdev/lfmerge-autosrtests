// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System.IO;
using Palaso.IO;

namespace LfMerge.AutomatedSRTests
{
	public class WebworkHelper: ChorusHelper
	{
		public WebworkHelper(string dbName)
		{
			RepoDir = Path.Combine(LfMergeHelper.BaseDir, "webwork", dbName);
			Directory.CreateDirectory(RepoDir);
			Initialize();
		}

		#region Dispose functionality
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (!disposing)
				return;

			if (string.IsNullOrEmpty(RepoDir))
				return;

			DirectoryUtilities.DeleteDirectoryRobust(RepoDir);
			RepoDir = null;
		}
		#endregion
	}
}
