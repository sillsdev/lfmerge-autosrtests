// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using Palaso.IO;

namespace LfMerge.AutomatedSRTests
{
	public class MongoHelper: IDisposable
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

		public void RestoreDatabase()
		{
			var projectEntryFile = Path.Combine(DataDir, DbName + ".json");
			if (!File.Exists(projectEntryFile))
			{
				throw new FileNotFoundException($"Can't find file {projectEntryFile}");
			}

			var projectDumpDir = Path.Combine(DataDir, DbName);
			if (!Directory.Exists(projectDumpDir))
			{
				throw new FileNotFoundException($"Can't find directory {projectDumpDir}");
			}

			Run("mongoimport",
				$"-db scriptureforge --collection projects --file {projectEntryFile}",
				DataDir);

			Run("mongorestore", $"-db {DbName} {projectDumpDir}", DataDir);
		}

		private string DbName { get; }

		public static string DataDir =>
			Path.Combine(Directories.DataDir, "mongo", "dump");

		private static void Run(string command, string args, string workDir)
		{
			using (var process = new Process())
			{
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.WorkingDirectory = workDir;
				process.StartInfo.FileName = command;
				process.StartInfo.Arguments = args;

				process.Start();

				process.WaitForExit();

				if (process.ExitCode != 0)
				{
					throw new ApplicationException($"Running '{command} {args}' returned {process.ExitCode}");
				}
			}
		}

		private void RemoveDatabase()
		{
			var projectCode = DbName.StartsWith("sf_") ? DbName.Substring(3) : DbName;
			Run("mongo", $"{DbName} --eval 'db.dropDatabase()'", DataDir);
			Run("mongo",
				$"scriptureforge --eval 'db.projects.remove({{ \"projectName\" : \"{projectCode}\" }})'",
				DataDir);
		}
	}
}
