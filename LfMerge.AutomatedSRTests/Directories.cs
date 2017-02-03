// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using Palaso.IO;

namespace LfMerge.AutomatedSRTests
{
	public static class Directories
	{
		public static string TempDir => Path.Combine(Path.GetTempPath(),
			Process.GetCurrentProcess().Id.ToString());

		public static string DataDir =>
			Path.Combine(FileLocator.DirectoryOfApplicationOrSolution, "data");

		public static void Cleanup()
		{
			DirectoryUtilities.DeleteDirectoryRobust(TempDir);
		}
	}
}
