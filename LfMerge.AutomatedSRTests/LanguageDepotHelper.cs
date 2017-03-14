// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using Chorus.VcsDrivers.Mercurial;
using Palaso.IO;
using Palaso.Progress;

namespace LfMerge.AutomatedSRTests
{
	public class LanguageDepotHelper: IDisposable
	{
		private bool _keepDir;

		public LanguageDepotHelper(bool keepDir = false)
		{
			_keepDir = keepDir;
			LdDirectory = Path.Combine(Settings.TempDir, "LanguageDepot");
			Directory.CreateDirectory(LdDirectory);
			HgRepository.CreateRepositoryInExistingDir(LdDirectory, new NullProgress());

			ApplyPatch("r0.patch");
		}

		#region Dispose functionality
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (!string.IsNullOrEmpty(LdDirectory) && !_keepDir)
					DirectoryUtilities.DeleteDirectoryRobust(LdDirectory);
			}
			LdDirectory = null;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~LanguageDepotHelper()
		{
			Dispose(false);
		}
		#endregion

		public static string LdDirectory { get; private set; }

		/// <summary>
		/// Applies a patch to the mock LanguageDepot hg repo
		/// </summary>
		/// <param name="patchFile">Patchfile and path, relative to <see cref="Settings.DataDir"/>.</param>
		/// <exception cref="FileNotFoundException">If <paramref name="patchFile"/> doesn't
		/// exist in <see cref="Settings.DataDir"/>.</exception>
		/// <exception cref="ApplicationException">If "hg import" returns a non-0 exit
		/// code</exception>
		public void ApplyPatch(string patchFile)
		{
			var patchPath = Path.Combine(Settings.DataDir, patchFile);
			if (!File.Exists(patchPath))
				throw new FileNotFoundException("Can't find patchfile", patchPath);

			var command = $"hg import --import-branch {patchPath}";
			var result = HgRunner.Run(command, LdDirectory, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{command}' returned {result.ExitCode}");
		}

		public void ApplyPatches(int dbVersion, int patch)
		{
			for (var i = 1; i <= patch; i++)
			{
				ApplyPatch(Path.Combine(dbVersion.ToString(), $"r{i}.patch"));
			}
		}
	}
}
