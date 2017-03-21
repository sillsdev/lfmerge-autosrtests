// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using CommandLine;
using LfMerge.AutomatedSRTests;
using Palaso.IO;
using Palaso.Linq;

namespace LfMerge.TestUtil
{
	internal static class Program
	{
		private static MongoHelper _mongoHelper;

		public static void Main(string[] args)
		{
			var tuple = Options.ParseCommandLineArgs(args);
			if (tuple.Item1 == null)
				return;

			var verb = tuple.Item1;

			switch (verb)
			{
				case "restore":
					var restoreOptions = tuple.Item2 as Options.RestoreOptions;
					if (restoreOptions.LanguageDepotVersion.HasValue &&
						string.IsNullOrEmpty(restoreOptions.WorkDir))
					{
						Console.WriteLine(restoreOptions.GetUsage());
						return;
					}

					Restore(restoreOptions);
					break;
				case "save":
					var saveOptions = tuple.Item2 as Options.SaveOptions;
					if (saveOptions.SaveLanguageDepot &&
						string.IsNullOrEmpty(saveOptions.WorkDir))
					{
						Console.WriteLine(saveOptions.GetUsage());
						return;
					}

					Save(saveOptions);
					break;
				case "merge":
					var mergeOptions = tuple.Item2 as Options.MergeOptions;
					if (string.IsNullOrEmpty(mergeOptions.WorkDir))
					{
						{
							Console.WriteLine(mergeOptions.GetUsage());
							return;
						}
					}

					Merge(mergeOptions);
					break;
			}

			_mongoHelper?.Dispose();
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

			if (!options.SaveMongoDb)
				return;

			MongoHelper.Initialize(options.ModelVersion);
			SaveMongoDb(options);
		}

		private static void Merge(Options.MergeOptions options)
		{
			DirectoryUtilities.DeleteDirectoryRobust(options.WorkDir);
			Settings.TempDir = options.WorkDir;

			// restore previous data
			var restoreOptions = new Options.RestoreOptions(options);
			RestoreLanguageDepot(restoreOptions);
			RestoreMongoDb(restoreOptions);

			// run merge
			LfMergeHelper.Run($"--project {options.Project} --clone --action=Synchronize");
			Console.WriteLine("Successfully merged test data");

			// save merged data
			var saveOptions = new Options.SaveOptions(options)
			{
				WorkDir = Path.Combine(options.WorkDir, "LanguageDepot"),
				CommitMsg = options.CommitMsg ?? "Merged test data"
			};
			SaveLanguageDepot(saveOptions);
			SaveMongoDb(saveOptions);
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

				Run("hg", $"export -r {rev} --output {patchFile}", hgDir);
				Console.WriteLine($"Saved file {patchFile}");
			}

			Console.WriteLine($"Successfully saved language depot patches in {patchDir}");
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
			if (!options.Project.StartsWith("sf_"))
				options.Project = "sf_" + options.Project;

			var patchFiles = Directory.GetFiles(MongoHelper.GetMongoPatchDir(options.ModelVersion), "*.patch");
			var lastNumber = 0;
			if (patchFiles != null && patchFiles.Length > 0)
			{
				Array.Sort(patchFiles);
				var patchNoStr = Path.GetFileName(patchFiles[patchFiles.Length - 1]).Substring(0, 4);
				lastNumber = int.Parse(patchNoStr);
			}
			if (_mongoHelper == null)
				_mongoHelper = new MongoHelper(options.Project, true, false);
			_mongoHelper.SaveDatabase(MongoHelper.GetMongoPatchDir(options.ModelVersion), options.ModelVersion,
				 options.CommitMsg, lastNumber + 1);

			Console.WriteLine("Successfully saved mongo database");
		}

		private static void RestoreMongoDb(Options.RestoreOptions options)
		{
			if (!options.Project.StartsWith("sf_"))
				options.Project = "sf_" + options.Project;

			MongoHelper.Initialize(options.ModelVersion, options.MongoVersion);
			if (_mongoHelper == null)
				_mongoHelper = new MongoHelper(options.Project, true);
			_mongoHelper.RestoreDatabase("master", options.ModelVersion);

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