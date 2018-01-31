// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
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

			var commonOptions = tuple.Item2 as Options.CommonOptions;
			if (commonOptions?.DataDir != null)
			{
				Settings.DataDirName = commonOptions.DataDir;
			}

			var verb = tuple.Item1;

			switch (verb)
			{
				case "restore":
					var restoreOptions = tuple.Item2 as Options.RestoreOptions;
					if (restoreOptions.LanguageDepotVersion.HasValue &&
						(string.IsNullOrEmpty(restoreOptions.WorkDir) ||
							restoreOptions.ModelVersion <= 0))
					{
						Console.WriteLine(restoreOptions.GetUsage());
						return;
					}

					Restore(restoreOptions);
					break;
				case "save":
					var saveOptions = tuple.Item2 as Options.SaveOptions;
					if (saveOptions.SaveLanguageDepot &&
						(string.IsNullOrEmpty(saveOptions.WorkDir) ||
							saveOptions.ModelVersion <= 0))
					{
						Console.WriteLine(saveOptions.GetUsage());
						return;
					}

					Save(saveOptions);
					break;
				case "merge":
					var mergeOptions = tuple.Item2 as Options.MergeOptions;
					if (string.IsNullOrEmpty(mergeOptions.WorkDir) ||
						mergeOptions.ModelVersion <= 0)
					{
						{
							Console.WriteLine(mergeOptions.GetUsage());
							return;
						}
					}

					Merge(mergeOptions);
					break;
				case "wizard":
					var wizardOptions = tuple.Item2 as Options.WizardOptions;

					Wizard(wizardOptions);
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

			MongoHelper.Initialize(options.ModelVersion.ToString());
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
			SaveLanguageDepotNoOpPatchIfNecessary(options.ModelVersion, options.LanguageDepotVersion + 1);
			SaveMongoDb(saveOptions);
		}

		private static void Wizard(Options.WizardOptions wizardOptions)
		{
			var workdir = wizardOptions.WorkDir;
			for (var modelVersion = wizardOptions.MinModel; modelVersion <= wizardOptions.MaxModel; modelVersion++)
			{
				Console.WriteLine("--------------------------------------------------------------");
				Console.WriteLine($"Processing model version {modelVersion}");

				if (string.IsNullOrEmpty(workdir))
				{
					wizardOptions.WorkDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
					Settings.TempDir = wizardOptions.WorkDir;
					Directory.CreateDirectory(wizardOptions.WorkDir);
				}

				wizardOptions.ModelVersion = modelVersion;
				var outputDir = Path.Combine(wizardOptions.FwRoot, $"Output{modelVersion}");
				if (!Directory.Exists(outputDir))
				{
					Console.WriteLine($"Can't find FW output directory {outputDir}");
					continue;
				}
				if (!File.Exists(Path.Combine(outputDir, "fwenviron")))
					File.Copy(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location),
						"fwenviron"), Path.Combine(outputDir, "fwenviron"));

				Console.WriteLine($"Restoring version {wizardOptions.LanguageDepotVersion} " +
					$"of chorus repo for model {modelVersion}");
				RestoreLanguageDepot(new Options.RestoreOptions(wizardOptions));
				DirectoryUtilities.CopyDirectoryWithException(Path.Combine(wizardOptions.WorkDir, "LanguageDepot", ".hg"),
					Path.Combine(wizardOptions.UsbDirectory, $"test-{modelVersion}", ".hg"), true);
				// Delete FW project
				DirectoryUtilities.DeleteDirectoryRobust(
					Path.Combine(wizardOptions.FwProjectDirectory, $"test-{modelVersion}"));

				Console.WriteLine($"Now get project 'test-{modelVersion}' from USB stick and make changes and afterwards do a s/r with the USB stick.");
				// for whatever reason we have to pass -i, otherwise 7000069 won't be able to bring
				// up the s/r dialog!
				Run("/bin/bash", $"-i -c \"cd {outputDir} && . fwenviron && env && cd Output_$(uname -m)/Debug && mono --debug FieldWorks.exe > /dev/null\"",
					outputDir);

				Console.WriteLine($"Saving chorus repo test data for {modelVersion}");
				DirectoryUtilities.CopyDirectoryWithException(
					Path.Combine(wizardOptions.UsbDirectory, $"test-{modelVersion}", ".hg"),
					Path.Combine(wizardOptions.WorkDir, "LanguageDepot", ".hg"), true);
				SaveLanguageDepot(new Options.SaveOptions(wizardOptions)
				{
					WorkDir = null,
					CommitMsg = wizardOptions.CommitMsg ?? "New test data"
				});

				SaveLanguageDepotNoOpPatchIfNecessary(modelVersion, wizardOptions.LanguageDepotVersion + 1);

				Console.WriteLine($"Restoring LanguageForge mongo data version {wizardOptions.MongoVersion} for model {modelVersion}");
				RestoreMongoDb(new Options.RestoreOptions(wizardOptions));

				Console.WriteLine($"Now make the changes to '{wizardOptions.Project}' in your local" +
					" LanguageForge, then press return. Don't do a send/receive!");
				Console.WriteLine("(You might have to empty the cached IndexedDB data in your " +
					"browser's developer tools)");
				Run("/bin/bash", "-c \"xdg-open http://languageforge.local/app/projects\"", outputDir);
				Console.ReadLine();

				Console.WriteLine($"Saving LanguageForge test data for {modelVersion}");
				SaveMongoDb(new Options.SaveOptions(wizardOptions)
				{
					CommitMsg = wizardOptions.CommitMsg ?? "New test data"
				});

				// Merge the data we just created
				Console.WriteLine($"Merge test data for {modelVersion}");
				Merge(new Options.MergeOptions(wizardOptions)
				{
					LanguageDepotVersion = wizardOptions.LanguageDepotVersion + 1,
					MongoVersion = wizardOptions.MongoVersion + 1
				});

				if (string.IsNullOrEmpty(workdir))
				{
					// we created a temporary workdir, so delete it again
					DirectoryUtilities.DeleteDirectoryRobust(wizardOptions.WorkDir);
				}
			}
		}

		private static void SaveLanguageDepotNoOpPatchIfNecessary(int modelVersion, int patchVersion)
		{
			if (ChorusHelper.PatchExists(modelVersion, patchVersion))
				return;

			// add no-op patch
			var oldContent = File.ReadAllText(Path.Combine(LanguageDepotHelper.LdDirectory, "no-op"))
				.TrimEnd('\n');
			File.WriteAllText(Path.Combine(Settings.DataDir, modelVersion.ToString(),
					$"r{patchVersion}.patch"),
				$@"# HG changeset patch
# User No-op
# Branch {modelVersion}
no-op to keep LD/Mongo patches balanced

diff no-op
--- a/no-op
+++ b/no-op
@@ -1,1 +1,1 @@
-{oldContent}
+{Guid.NewGuid().ToString()}
");
			Console.WriteLine($"Added no-op patch r{patchVersion}.patch");
		}

		private static void SaveLanguageDepot(Options.SaveOptions options)
		{
			var patchDir = Path.Combine(Settings.DataDir, options.ModelVersion.ToString());
			var hgDir = string.IsNullOrEmpty(options.WorkDir)
				? Path.Combine(Settings.TempDir, "LanguageDepot")
				: options.WorkDir;
			Directory.CreateDirectory(patchDir);

			var output = Run("hg", "log --template \"{rev} \"", hgDir);
			var revs = output.Trim().Trim('\r', '\n').Split(' ').Reverse();
			var count = 0;
			foreach (var rev in revs)
			{
				var patchFile = Path.Combine(patchDir, $"r{rev}.patch");
				if (File.Exists(patchFile))
					continue;

				Run("hg", $"export -r {rev} --output {patchFile}", hgDir);
				Console.WriteLine($"Saved file {patchFile}");
				count++;
			}

			if (count > 0)
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
					ld.ApplySinglePatch(options.ModelVersion, i);
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

			var patchFiles = Directory.GetFiles(MongoHelper.GetMongoPatchDir(options.ModelVersion.ToString()), "*.patch");
			var lastNumber = 0;
			if (patchFiles != null && patchFiles.Length > 0)
			{
				Array.Sort(patchFiles);
				var patchNoStr = Path.GetFileName(patchFiles[patchFiles.Length - 1]).Substring(0, 4);
				lastNumber = int.Parse(patchNoStr);
			}
			if (_mongoHelper == null)
				_mongoHelper = new MongoHelper(options.Project, true, false);
			_mongoHelper.SaveDatabase(MongoHelper.GetMongoPatchDir(options.ModelVersion.ToString()),
				options.ModelVersion.ToString(), options.CommitMsg, lastNumber + 1);

			Console.WriteLine("Successfully saved mongo database");
		}

		private static void RestoreMongoDb(Options.RestoreOptions options)
		{
			if (!options.Project.StartsWith("sf_"))
				options.Project = "sf_" + options.Project;

			MongoHelper.Initialize(options.ModelVersion.ToString(), options.MongoVersion);
			if (_mongoHelper == null)
				_mongoHelper = new MongoHelper(options.Project, true);
			_mongoHelper.RestoreDatabase(options.ModelVersion.ToString(), "master");

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

					var msg = $"Running '{command} {args}' returned {process.ExitCode}.\nStderr:\n{stderr}\nOutput:\n{output}";
					throw new ApplicationException(msg);
				}
			}
		}

	}
}