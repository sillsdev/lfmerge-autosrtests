// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Palaso.IO;

namespace LfMerge.AutomatedSRTests
{
	public class MongoHelper : IDisposable
	{
		public MongoHelper(string dbName)
		{
			DbName = dbName;
			RemoveDatabase();
		}

		#region Dispose functionality

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				RemoveDatabase();
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~MongoHelper()
		{
			Dispose(false);
		}

		#endregion

		public void RestoreDatabase(string tag)
		{
			Run("git", $"checkout {tag}", MongoSourceDir);
			var projectEntryFile = Path.Combine(MongoSourceDir, DbName + ".json");
			if (!File.Exists(projectEntryFile))
			{
				throw new FileNotFoundException($"Can't find file {projectEntryFile}");
			}

			Run("mongoimport",
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} --db scriptureforge " +
				$"--collection projects --file {projectEntryFile}",
				MongoPatchDir);

			ImportCollection("activity");
			ImportCollection("lexicon");
			ImportCollection("optionlists");
		}

		private void ImportCollection(string collection)
		{
			var file = Path.Combine(MongoSourceDir, $"{DbName}.{collection}.json");
			Run("mongoimport",
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} --db {DbName} " +
				$"--drop --collection {collection} --file {file}",
				MongoSourceDir);
		}

		public static void Initialize()
		{
			// we use a git repo to store the JSON files that we'll import into Mongo. Storing
			// the JSON files as patch files allows to easily see (e.g. on GitHub) the changes
			// between two versions; recreating the git repo at test run time makes it easier to
			// switch between versions.
			Directory.CreateDirectory(MongoSourceDir);
			Run("git", "init .", MongoSourceDir);
			Run("git", "config user.email \"you@example.com\"", MongoSourceDir);
			Run("git", "config user.name \"Your Name\"", MongoSourceDir);

			var patchFiles = Directory.GetFiles(MongoPatchDir, "*.patch");
			Array.Sort(patchFiles);
			foreach (var file in patchFiles)
			{
				var patchNoStr = Path.GetFileName(file).Substring(0, 4);
				var patchNo = int.Parse(patchNoStr);
				Run("git", $"am {file}", MongoSourceDir);
				Run("git", $"tag r{patchNo}", MongoSourceDir);
			}
		}

		public static void Cleanup()
		{
			DirectoryUtilities.DeleteDirectoryRobust(MongoSourceDir);
		}

		private string DbName { get; }

		private static string MongoPatchDir => Path.Combine(Settings.DataDir, "mongo");

		private static string MongoSourceDir => Path.Combine(Settings.TempDir, "patches");

		private static void Run(string command, string args, string workDir, bool throwException = true)
		{
			//Console.WriteLine();
			//Console.WriteLine($"Running command: {command} {args}");
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
					//Console.WriteLine($"Output: {output}");
					//Console.WriteLine($"Stderr: {stderr}");

					if (process.ExitCode == 0)
						return;

					var msg = $"Running '{command} {args}' returned {process.ExitCode}.\nStderr:\n${stderr}";
					if (throwException)
						throw new ApplicationException(msg);

					Console.WriteLine(msg);
				}
			}
		}

		private void RemoveDatabase()
		{
			var projectCode = DbName.StartsWith("sf_") ? DbName.Substring(3) : DbName;
			Run("mongo", $"{DbName} --eval 'db.dropDatabase()'", MongoSourceDir, false);
			Run("mongo",
				$"--host {Settings.MongoHostName} --port {Settings.MongoPort} " +
				$"scriptureforge --eval 'db.projects.remove({{ \"projectName\" : \"{projectCode}\" }})'",
				MongoSourceDir, false);
		}
	}
}