// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Palaso.IO;

namespace LfMerge.AutomatedSRTests
{
	public class MongoHelper : IDisposable
	{
		private bool _keepDatabase;
		private const string Git = "/usr/bin/git";
		private const string Mongo = "/usr/bin/mongo";
		private const string MongoImport = "/usr/bin/mongoimport";
		private const string MongoExport = "/usr/bin/mongoexport";

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
				TestHelper.Run(Git, $"am {file} --ignore-whitespace", mongoSourceDir);
				TestHelper.Run(Git, $"tag r{patchNo}", mongoSourceDir);
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

		public void RestoreDatabase(int modelVersion, int tag)
		{
			RestoreDatabase(modelVersion.ToString(), $"r{tag}");
		}

		public void RestoreDatabase(string modelVersion, string tag)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			TestHelper.Run(Git, $"checkout {tag}", mongoSourceDir);
			var projectEntryFile = Path.Combine(mongoSourceDir, DbName + ".json");
			if (!File.Exists(projectEntryFile))
			{
				throw new FileNotFoundException($"Can't find file {projectEntryFile}");
			}

			var tempFile = ReadJson(projectEntryFile);
			TestHelper.Run(MongoImport,
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
			var content = TestHelper.Run(MongoExport,
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} " +
				$"--db scriptureforge --collection projects --query '{{ \"projectCode\" : \"{project}\" }}'",
				mongoSourceDir);
			WriteJson(file, content);
			TestHelper.Run(Git, $"add {file}", mongoSourceDir);

			ExportCollection("activity", modelVersion);
			ExportCollection("lexicon", modelVersion);
			ExportCollection("lexiconComments", modelVersion);
			ExportCollection("optionlists", modelVersion);

			AddCollectionToGit("activity", modelVersion);
			AddCollectionToGit("lexicon", modelVersion);
			AddCollectionToGit("lexiconComments", modelVersion);
			AddCollectionToGit("optionlists", modelVersion);

			TestHelper.Run(Git, $"commit -a --allow-empty -m \"{msg}\"", mongoSourceDir);
			TestHelper.Run(Git,
				$"format-patch -1 -o {patchDir} --start-number {startNumber} --ignore-all-space",
				mongoSourceDir);
		}

		private static void WriteJson(string file, string content)
		{
			// Normalize JSON file (mainly removing spaces between keys and values).
			// We do this by deserializing and serializing the objects.
			// Mongo gives us multiple objects (one on a line), but Json.NET doesn't like that,
			// so we process them line by line.
			var strBldr = new StringBuilder();
			foreach (var line in content.Split('\n'))
			{
				if (string.IsNullOrEmpty(line))
					continue;
				strBldr.AppendLine(JsonConvert.SerializeObject(
					JsonConvert.DeserializeObject(line),
					new JsonSerializerSettings
					{
						DateFormatHandling = DateFormatHandling.IsoDateFormat,
						DateTimeZoneHandling = DateTimeZoneHandling.Utc,
						DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
						Formatting = Formatting.None
					}));
			}
			strBldr.Replace("}", "}\n");
			File.WriteAllText(file, strBldr.ToString());
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
			if (!File.Exists(file))
				return;
			var tempFile = ReadJson(file);
			TestHelper.Run(MongoImport,
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} --db {DbName} " +
				$"--drop --collection {collection} --file {tempFile}",
				mongoSourceDir);
			File.Delete(tempFile);
		}

		private void ExportCollection(string collection, string modelVersion)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			var file = Path.Combine(mongoSourceDir, $"{DbName}.{collection}.json");
			var content = TestHelper.Run(MongoExport,
				$"--host {Settings.MongoHostName}:{Settings.MongoPort} --db {DbName} " +
				$"--collection {collection}",
				mongoSourceDir);
			WriteJson(file, content);
		}

		private void AddCollectionToGit(string collection, string modelVersion)
		{
			var mongoSourceDir = GetMongoSourceDir(modelVersion);
			var file = Path.Combine(mongoSourceDir, $"{DbName}.{collection}.json");
			TestHelper.Run(Git, $"add {file}", mongoSourceDir);
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
			TestHelper.Run(Git, "init .", gitDir);
			TestHelper.Run(Git, "config user.email \"you@example.com\"", gitDir);
			TestHelper.Run(Git, "config user.name \"Your Name\"", gitDir);
		}

		public static void Cleanup()
		{
			DirectoryUtilities.DeleteDirectoryRobust(Path.Combine(Settings.TempDir, "patches"));
		}

		private string DbName { get; }

		private void RemoveDatabase()
		{
			var projectCode = DbName.StartsWith("sf_") ? DbName.Substring(3) : DbName;
			var workDir = Path.Combine(Settings.TempDir, "patches");
			TestHelper.Run(Mongo, $"{DbName} --eval 'db.dropDatabase()'", workDir, false, true);
			TestHelper.Run(Mongo,
				$"--host {Settings.MongoHostName} --port {Settings.MongoPort} " +
				$"scriptureforge --eval 'db.projects.remove({{ \"projectName\" : \"{projectCode}\" }})'",
				workDir, false, true);
		}
	}
}