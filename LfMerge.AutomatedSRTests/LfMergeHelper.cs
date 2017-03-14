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
		public static string BaseDir => Path.Combine(Settings.TempDir, "BaseDir");

		private static void WriteTestSettingsFile()
		{
			Directory.CreateDirectory(BaseDir);
			Settings.WriteConfigFile(BaseDir);
		}

		public static void Run(string args)
		{
			WriteTestSettingsFile();

			using (var process = new Process())
			{
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.FileName = Path.Combine(FileLocator.DirectoryOfApplicationOrSolution, "lfmerge");
				process.StartInfo.Arguments = args + $" --config \"{Settings.TempDir}\"";
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

		public static void Cleanup()
		{
			DirectoryUtilities.DeleteDirectoryRobust(BaseDir);
		}
	}
}
