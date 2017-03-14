// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Chorus.VcsDrivers.Mercurial;
using LfMerge.AutomatedSRTests;
using Palaso.IO;
using Palaso.Linq;
using Palaso.Progress;

namespace LfMerge.TestUtil
{
	internal static class Program
	{
		public static void Main(string[] args)
		{
			var tuple = Options.ParseCommandLineArgs(args);
			if (tuple.Item1 == null)
				return;

			var verb = tuple.Item1;

			switch (verb)
			{
				case "restore":
					Restore(tuple.Item2 as Options.RestoreOptions);
					break;
				case "save":
					Save(tuple.Item2 as Options.SaveOptions);
					break;
			}
		}

		private static void Restore(Options.RestoreOptions options)
		{
			Settings.TempDir = options.WorkDir;
			if (options.LanguageDepotVersion.HasValue)
				RestoreLanguageDepot(options);

			if (options.MongoVersion.HasValue)
				RestoreMongoDb(options);
		}

		private static void Save(Options.SaveOptions options)
		{
			Settings.TempDir = options.WorkDir;

			if (options.SaveLanguageDepot)
				SaveLanguageDepot(options);

			if (options.SaveMongoDb)
				SaveMongoDb(options);
		}

		private static void SaveLanguageDepot(Options.SaveOptions options)
		{
			var patchDir = Path.Combine(Settings.DataDir, options.ModelVersion);
			var hgDir = string.IsNullOrEmpty(options.WorkDir)
				? Path.Combine(Settings.TempDir, "LanguageDepot")
				: options.WorkDir;
			Directory.CreateDirectory(patchDir);

			var output = Run("hg", "log --template \"{rev} \"", hgDir);
			var revs = output.Trim().Trim('\r', '\n').Split(' ').Reverse();
			foreach (var rev in revs)
			{
				var patchFile = Path.Combine(patchDir, $"r{rev}.patch");
				if (File.Exists(patchFile))
					continue;

				output = Run("hg", $"export -r {rev}", hgDir);
				File.WriteAllText(patchFile, output);
				Console.WriteLine($"Saved file {patchFile}");
			}

			Console.WriteLine($"Successfully saved patches in {patchDir}");
		}

		private static void RestoreLanguageDepot(Options.RestoreOptions options)
		{
			var dir = Path.Combine(Settings.TempDir, "LanguageDepot");
			DirectoryUtilities.DeleteDirectoryRobust(dir);
			using (var ld = new LanguageDepotHelper(true))
			{
				for (var i = 0; i <= options.LanguageDepotVersion.Value; i++)
				{
					ld.ApplyPatch(Path.Combine(options.ModelVersion, $"r{i}.patch"));
				}
			}

			// Now in FLEx get project from colleague from USB
			Console.WriteLine("Successfully restored languagedepot test data for model " +
				$"{options.ModelVersion}");
		}

		private static void SaveMongoDb(Options.SaveOptions options)
		{
			var patchFiles = Directory.GetFiles(options.WorkDir, "*.patch");
			var lastNumber = 0;
			if (patchFiles != null && patchFiles.Length > 0)
			{
				Array.Sort(patchFiles);
				var patchNoStr = Path.GetFileName(patchFiles[patchFiles.Length - 1]).Substring(0, 4);
				lastNumber = int.Parse(patchNoStr);
			}
			using (var mongoHelper = new MongoHelper(options.Project, true))
			{
				mongoHelper.SaveDatabase(options.WorkDir, options.CommitMsg, lastNumber + 1);
			}

			Console.WriteLine("Successfully saved mongo database");
		}

		private static void RestoreMongoDb(Options.RestoreOptions options)
		{
			MongoHelper.Initialize();
			using (var mongoHelper = new MongoHelper(options.Project, true))
			{
				mongoHelper.RestoreDatabase($"r{options.MongoVersion}");
			}

			Console.WriteLine($"Successfully restored mongo database at version {options.MongoVersion}");
		}

		private static string Run(string command, string args, string workDir)
		{
			// NOTE: don't replace this method with HgRunner.Run()! That trims leading/trailing
			// spaces and causes the patch files to fail to import!
			using (var process = new Process())
			{
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.WorkingDirectory = workDir;
				process.StartInfo.FileName = command;
				process.StartInfo.Arguments = args;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;

				var output = new StringBuilder();
				var stderr = new StringBuilder();

				using (var outputWaitHandle = new AutoResetEvent(false))
				using (var errorWaitHandle = new AutoResetEvent(false))
				{
					process.OutputDataReceived += (sender, e) =>
					{
						if (e.Data == null)
							outputWaitHandle.Set();
						else
							output.AppendLine(e.Data);
					};
					process.ErrorDataReceived += (sender, e) =>
					{
						if (e.Data == null)
							errorWaitHandle.Set();
						else
							stderr.AppendLine(e.Data);
					};

					process.Start();

					process.BeginErrorReadLine();
					process.BeginOutputReadLine();
					process.WaitForExit();
					errorWaitHandle.WaitOne();
					outputWaitHandle.WaitOne();

					if (process.ExitCode == 0)
						return output.ToString();

					var msg = $"Running '{command} {args}' returned {process.ExitCode}.\nStderr:\n${stderr}";
					throw new ApplicationException(msg);
				}
			}
		}

	}
}