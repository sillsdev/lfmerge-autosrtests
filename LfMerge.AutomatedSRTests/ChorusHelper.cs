// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;
using Chorus.VcsDrivers.Mercurial;
using Nini.Ini;
using SIL.Progress;

namespace LfMerge.AutomatedSRTests
{
	public abstract class ChorusHelper: IDisposable
	{
		private const string Patch = "/usr/bin/patch";

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

		public void ApplySinglePatch(int dbVersion, int patch, bool commit = true)
		{
			ApplySinglePatch(dbVersion.ToString(), patch, commit);
		}

		public void ApplySinglePatch(string dbVersion, int patch, bool commit = true)
		{
			ApplyPatch(Path.Combine(dbVersion, $"r{patch}.patch"), commit);
		}

		public static bool PatchExists(int dbVersion, int patch)
		{
			return File.Exists(Path.Combine(Settings.DataDir, dbVersion.ToString(), $"r{patch}.patch"));
		}

		/// <summary>
		/// Applies a patch to the mock hg repo
		/// </summary>
		/// <param name="patchFile">Patchfile and path, relative to <see cref="Settings.DataDir"/>.</param>
		/// <param name="commit"><c>true</c> to commit patch, <c>false</c> to just update the
		/// working directory.</param>
		/// <exception cref="FileNotFoundException">If <paramref name="patchFile"/> doesn't
		/// exist in <see cref="Settings.DataDir"/>.</exception>
		/// <exception cref="ApplicationException">If "hg import" returns a non-0 exit
		/// code</exception>
		private void ApplyPatch(string patchFile, bool commit = true)
		{
			var patchPath = Path.Combine(Settings.DataDir, patchFile);
			if (!File.Exists(patchPath))
				throw new FileNotFoundException("Can't find patchfile", patchPath);
			var commitArg = commit ? "" : "--no-commit";

			var command = $"hg import {commitArg} --import-branch {patchPath}";
			var result = HgRunner.Run(command, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{command}' returned {result.ExitCode}");
		}

		public void ApplyPatches(int dbVersion, int patch)
		{
			UpdateHgrc(dbVersion);
			for (var i = 0; i <= patch; i++)
			{
				ApplySinglePatch(dbVersion, i);
			}
		}

		public void ApplyReversePatch(string dbVersion, int patch, string commitMsg)
		{
			var patchPath = Path.Combine(Settings.DataDir, dbVersion, $"r{patch}.patch");
			if (!File.Exists(patchPath))
				throw new FileNotFoundException("Can't find patchfile", patchPath);

			TestHelper.Run(Patch, $"--reverse -p 1 -i {patchPath}", RepoDir);
			var hgCommand = $"hg commit --message \"{commitMsg}\"";
			var result = HgRunner.Run(hgCommand, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{hgCommand}' returned {result.ExitCode}");
		}

		public void RemoveNodeFromFile(string fileName, string xpath, string commitMsg)
		{
			var fullFileName = Path.Combine(RepoDir, fileName);
			if (!File.Exists(fullFileName))
				throw new FileNotFoundException("Can't find XML file", fullFileName);

			var doc = XDocument.Parse(File.ReadAllText(fullFileName));
			var node = doc.XPathSelectElement(xpath);
			if (node == null)
				return;

			node.Remove();
			doc.Save(fullFileName);

			HgRunner.Run($"hg add \"{fullFileName}\"", RepoDir, 10, new NullProgress());
			var hgCommand = $"hg commit --amend --message \"{commitMsg}\"";
			var result = HgRunner.Run(hgCommand, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{hgCommand}' returned {result.ExitCode}");
			}

		public void MergeWith(ChorusHelper other)
		{
			var hgCommand = $"hg pull {other.RepoDir}";
			var result = HgRunner.Run(hgCommand, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{hgCommand}' returned {result.ExitCode}");

			// Update to the revision we just pulled. This is necessary so that the notes
			// appears in the right order that we would get if we'd test manually
			hgCommand = "hg update tip";
			result = HgRunner.Run(hgCommand, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{hgCommand}' returned {result.ExitCode}");

			Environment.SetEnvironmentVariable("ChorusPathToRepository", RepoDir);
			hgCommand = "hg merge -t chorusmerge";
			result = HgRunner.Run(hgCommand, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{hgCommand}' returned {result.ExitCode}");

			hgCommand = "hg commit -m \"Merge\"";
			result = HgRunner.Run(hgCommand, RepoDir, 10, new NullProgress());
			if (result.ExitCode != 0)
				throw new ApplicationException($"'{hgCommand}' returned {result.ExitCode}");
		}
	}
}
