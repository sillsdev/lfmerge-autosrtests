// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Chorus.VcsDrivers.Mercurial;
using Nini.Ini;
using Palaso.Progress;

namespace LfMerge.AutomatedSRTests
{
	public abstract class ChorusHelper : IDisposable
	{
		#region Dispose functionality
		protected virtual void Dispose(bool disposing)
		{
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~ChorusHelper()
		{
			Dispose(false);
		}
		#endregion

		protected void Initialize()
		{
			HgRepository.CreateRepositoryInExistingDir(RepoDir, new NullProgress());
		}

		private void UpdateHgrc(int dbVersion)
		{
			var doc = new IniDocument(Path.Combine(RepoDir, ".hg", "hgrc"), IniFileType.MercurialStyle);
			var mergeToolsSection = doc.Sections.GetOrCreate("merge-tools");
			mergeToolsSection.Set("chorusmerge.executable", $"/usr/lib/lfmerge/{dbVersion}/chorusmerge");
			doc.SaveAndThrowIfCannot();
		}

		protected string RepoDir { get; set; }

		/// <summary>
		/// Applies a patch to the mock hg repo
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
			var result = HgRunner.Run(command, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{command}' returned {result.ExitCode}");
		}

		public void ApplyPatches(int dbVersion, int patch)
		{
			UpdateHgrc(dbVersion);
			for (var i = 0; i <= patch; i++)
			{
				ApplyPatch(Path.Combine(dbVersion.ToString(), $"r{i}.patch"));
			}
		}
	}
}
