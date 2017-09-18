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
		private bool _keepDatabase;

		public static void Initialize()
		{
			var modelVersionDirectories = Directory.GetDirectories(Settings.DataDir, "70*");
			foreach (var modelVersionDirectory in modelVersionDirectories)
			{
				var modelVersion = Path.GetFileName(modelVersionDirectory);
				Initialize(modelVersion);
			}
		}

		public static void Initialize(string modelVersion, int? version = null)
		{
			// we use a git repo to store the JSON files that we'll import into Mongo. Storing
			// the JSON files as patch files allows to easily see (e.g. on GitHub) the changes
			// between two versions; recreating the git repo at test run time makes it easier to
			// switch between versions.
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			InitSourceDir(mongoSourceDir);

			var patchFiles = Directory.GetFiles(GetMongoPatchDir(modelVersion), "*.patch");
			Array.Sort(patchFiles);
			foreach (var file in patchFiles)
			{
				var patchNoStr = Path.GetFileName(file).Substring(0, 4);
				var patchNo = int.Parse(patchNoStr);
				if (version.HasValue && patchNo > version.Value)
					break;
				Run("git", $"am {file}", mongoSourceDir);
				Run("git", $"tag r{patchNo}", mongoSourceDir);
			}
		}

		public MongoHelper(string dbName, bool keepDatabase = false, bool removeDatabase = true)
		{
			_keepDatabase = keepDatabase;
			DbName = dbName;
			if (removeDatabase)
				RemoveDatabase();
		}

		#region Dispose functionality

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (!_keepDatabase)
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

		public void RestoreDatabase(string tag, int modelVersion)
		{
			RestoreDatabase(tag, modelVersion.ToString());
		}

		public void RestoreDatabase(string tag, string modelVersion)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			Run("git", $"checkout {tag}", mongoSourceDir);
			var projectEntryFile = Path.Combine(mongoSourceDir, DbName + ".json");
			if (!File.Exists(projectEntryFile))
			{
				throw new FileNotFoundException($"Can't find file {projectEntryFile}");
			}

			var tempFile = ReadJson(projectEntryFile);
			Run("mongoimport",
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} --db scriptureforge " +
				$"--collection projects --file {tempFile}",
				GetMongoPatchDir(modelVersion));
			File.Delete(tempFile);

			ImportCollection("activity", modelVersion);
			ImportCollection("lexicon", modelVersion);
			ImportCollection("lexiconComments", modelVersion);
			ImportCollection("optionlists", modelVersion);
		}

		public void SaveDatabase(string patchDir, string modelVersion, string msg, int startNumber)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			InitSourceDir(mongoSourceDir);
			var project = DbName.StartsWith("sf_") ? DbName.Substring(3) : DbName;
			var file = Path.Combine(mongoSourceDir, $"{DbName}.json");
			var content = Run("mongoexport",
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} " +
				$"--db scriptureforge --collection projects --query '{{ \"projectCode\" : \"{project}\" }}'",
				mongoSourceDir);
			WriteJson(file, content);
			Run("git", $"add {file}", mongoSourceDir);

			ExportCollection("activity", modelVersion);
			ExportCollection("lexicon", modelVersion);
			ExportCollection("lexiconComments", modelVersion);
			ExportCollection("optionlists", modelVersion);

			AddCollectionToGit("activity", modelVersion);
			AddCollectionToGit("lexicon", modelVersion);
			AddCollectionToGit("lexiconComments", modelVersion);
			AddCollectionToGit("optionlists", modelVersion);

			Run("git", $"commit -a -m \"{msg}\"", mongoSourceDir);
			Run("git", $"format-patch -1 -o {patchDir} --start-number {startNumber}", mongoSourceDir);
		}

		private static void WriteJson(string file, string content)
		{
			File.WriteAllText(file, content.Replace("}", "}\n"));
		}

		private static string ReadJson(string file)
		{
			var tempFile = Path.GetTempFileName();
			File.WriteAllText(tempFile, File.ReadAllText(file).Replace("}\n", "}"));
			return tempFile;
		}

		private void ImportCollection(string collection, string modelVersion)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			var file = Path.Combine(mongoSourceDir, $"{DbName}.{collection}.json");
			var tempFile = ReadJson(file);
			Run("mongoimport",
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} --db {DbName} " +
				$"--drop --collection {collection} --file {tempFile}",
				mongoSourceDir);
			File.Delete(tempFile);
		}

		private void ExportCollection(string collection, string modelVersion)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			var file = Path.Combine(mongoSourceDir, $"{DbName}.{collection}.json");
			var content = Run("mongoexport",
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} --db {DbName} " +
				$"--collection {collection}",
				mongoSourceDir);
			WriteJson(file, content);
		}

		private void AddCollectionToGit(string collection, string modelVersion)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			var file = Path.Combine(mongoSourceDir, $"{DbName}.{collection}.json");
			Run("git", $"add {file}", mongoSourceDir);
		}

		public static string GetMongoPatchDir(string modelVersion)
		{
			return Path.Combine(Settings.DataDir, modelVersion, "mongo");
		}

		private static string GetMongoSourceDir(string modelVersion)
		{
			return Path.Combine(Settings.TempDir, "source", modelVersion);
		}

		private static void InitSourceDir(string gitDir)
		{
			Directory.CreateDirectory(gitDir);
			Run("git", "init .", gitDir);
			Run("git", "config user.email \"you@example.com\"", gitDir);
			Run("git", "config user.name \"Your Name\"", gitDir);
		}

		public static void Cleanup()
		{
			DirectoryUtilities.DeleteDirectoryRobust(Path.Combine(Settings.TempDir, "patches"));
		}

		private string DbName { get; }

		private static string Run(string command, string args, string workDir, bool throwException = true, bool ignoreErrors = false)
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
						return output.ToString();

					if (ignoreErrors)
						return string.Empty;

					var msg = $"Running '{command} {args}'\nreturned {process.ExitCode}.\nStderr:\n{stderr}\nOutput:\n{output}";
					if (throwException)
						throw new ApplicationException(msg);

					Console.WriteLine(msg);
					return stderr.ToString();
				}
			}
		}

		private void RemoveDatabase()
		{
			var projectCode = DbName.StartsWith("sf_") ? DbName.Substring(3) : DbName;
			var workDir = Path.Combine(Settings.TempDir, "patches");
			Run("mongo", $"{DbName} --eval 'db.dropDatabase()'", workDir, false, true);
			Run("mongo",
				$"--host {Settings.MongoHostName} --port {Settings.MongoPort} " +
				$"scriptureforge --eval 'db.projects.remove({{ \"projectName\" : \"{projectCode}\" }})'",
				workDir, false, true);
		}
	}
}