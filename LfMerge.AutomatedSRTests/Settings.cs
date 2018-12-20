// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.Diagnostics;
using System.IO;
using SIL.IO;

namespace LfMerge.AutomatedSRTests
{
	public static class Settings
	{
		public const int MinModelVersion = 7000068;
		public const int MaxModelVersion = 7000072;
		private static string _tempDir;

		public static string TempDir
		{
			get => String.IsNullOrEmpty(_tempDir)
				? Path.Combine(Path.GetTempPath(), Process.GetCurrentProcess().Id.ToString())
				: _tempDir;
			set => _tempDir = value;
		}

		private static string _dataDirName;

		public static string DbName {get; set; }

		public static string DataDirName
		{
			get => String.IsNullOrEmpty(_dataDirName) ? "data" : _dataDirName;
			set => _dataDirName = value;
		}

		public static string DataDir =>
			Path.Combine(FileLocationUtilities.DirectoryOfApplicationOrSolution, DataDirName);

		public static void Cleanup()
		{
			RobustIO.DeleteDirectoryAndContents(TempDir);
		}

		private static bool VerifyPhpDir(string dir)
		{
			return File.Exists(Path.Combine(dir, "Api/Library/Shared/CLI/cliConfig.php")) &&
				File.Exists(Path.Combine(dir, "vendor/autoload.php"));
		}

		private static string CheckPhpDir(string candidate)
		{
			return VerifyPhpDir(candidate) ? candidate : null;
		}

		private static string PhpSourceDir
		{
			get
			{
				var phpSourceDir = CheckPhpDir("/var/www/virtual/languageforge.org/htdocs") ??
					CheckPhpDir("/var/www/virtual/languageforge.org/htdocs") ??
					CheckPhpDir("/var/www/languageforge.org_dev/htdocs") ??
					Path.Combine(DataDir, "php", "src");
				if (!VerifyPhpDir(phpSourceDir))
				{
					Console.WriteLine(
						"Can't find 'Api/Library/Shared/CLI/cliConfig.php' or 'vendor/autoload.php'" +
						" in any of the directories ['/var/www/virtual/languageforge.org/htdocs', " +
						"'/var/www/virtual/languageforge.org/htdocs', '{0}']", phpSourceDir);
				}
				return phpSourceDir;
			}
		}

		public static string MongoHostName =>
			Environment.GetEnvironmentVariable("MongoHostName") ?? "localhost";

		public static string MongoPort =>
			Environment.GetEnvironmentVariable("MongoPort") ?? "27017";

		public static void WriteConfigFile(string baseDir)
		{
			var settings = $@"
BaseDir = {baseDir}
WebworkDir = webwork
TemplatesDir = Templates
MongoHostname = {MongoHostName}
MongoPort = {MongoPort}
MongoMainDatabaseName = scriptureforge
MongoDatabaseNamePrefix = sf_
VerboseProgress = false
PhpSourcePath = {PhpSourceDir}
LanguageDepotRepoUri = {LanguageDepotHelper.LdDirectory}
";
			var configFile = Path.Combine(TempDir, "sendreceive.conf");
			File.WriteAllText(configFile, settings);
		}
	}
}
