// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using Palaso.IO;

namespace LfMerge.AutomatedSRTests
{
	public static class LfMergeHelper
	{
		public static string BaseDir => Path.Combine(Directories.TempDir, "BaseDir");

		private static bool VerifyPhpDir(string dir)
		{
			return File.Exists(Path.Combine(dir, "Api/Library/Shared/CLI/cliConfig.php")) &&
				File.Exists(Path.Combine(dir, "vendor/autoload.php"));
		}

		private static string CheckPhpDir(string candidate)
		{
			return VerifyPhpDir(candidate) ? candidate : null;
		}

		private static void WriteTestSettingsFile()
		{
			var phpSourceDir = CheckPhpDir("/var/www/virtual/languageforge.org/htdocs") ??
				CheckPhpDir("/var/www/virtual/languageforge.org/htdocs") ??
				CheckPhpDir("/var/www/languageforge.org_dev/htdocs") ??
				Path.Combine(Directories.DataDir, "php", "src");
			if (!VerifyPhpDir(phpSourceDir))
			{
				Console.WriteLine("Can't find 'Api/Library/Shared/CLI/cliConfig.php' or 'vendor/autoload.php'" +
					" in any of the directories ['/var/www/virtual/languageforge.org/htdocs', " +
					"'/var/www/virtual/languageforge.org/htdocs', '{0}']", phpSourceDir);
			}

			Directory.CreateDirectory(BaseDir);
			var mongoHostName = Environment.GetEnvironmentVariable("MongoHostName") ?? "localhost";
			var mongoPort = Environment.GetEnvironmentVariable("MongoPort") ?? "27017";
			var settings = $@"
BaseDir = {BaseDir}
WebworkDir = webwork
TemplatesDir = Templates
MongoHostname = {mongoHostName}
MongoPort = {mongoPort}
MongoMainDatabaseName = scriptureforge
MongoDatabaseNamePrefix = sf_
VerboseProgress = false
PhpSourcePath = {phpSourceDir}
LanguageDepotRepoUri = {LanguageDepotHelper.LdDirectory}
";
			var configFile = Path.Combine(Directories.TempDir, "sendreceive.conf");
			File.WriteAllText(configFile, settings);
		}

		public static void Run(string args)
		{
			WriteTestSettingsFile();

			using (var process = new Process())
			{
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.FileName = Path.Combine(FileLocator.DirectoryOfApplicationOrSolution, "lfmerge");
				process.StartInfo.Arguments = args + $" --config \"{Directories.TempDir}\"";
				process.StartInfo.RedirectStandardOutput = true;
				Console.WriteLine($"Executing: {process.StartInfo.FileName} {process.StartInfo.Arguments}");

				process.Start();

				Console.WriteLine("Output: {0}", process.StandardOutput.ReadToEnd());
				process.WaitForExit();

				if (process.ExitCode != 0)
				{
					throw new ApplicationException($"Running 'lfmerge {args}' returned {process.ExitCode}");
				}
			}
		}
	}
}
