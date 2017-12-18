// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	public static class TestHelper
	{
		public static string SRState
		{
			get
			{
				var stateFile = Path.Combine(LfMergeHelper.BaseDir, "state", $"{Settings.DbName}.state");
				Assert.That(File.Exists(stateFile), Is.True, $"Statefile '{stateFile}' doesn't exist");
				var stateFileContent = JObject.Parse(File.ReadAllText(stateFile));
				var state = stateFileContent["SRState"].ToString();
				return state;
			}
		}

		public static void SetupFixture(string dataDirName, string dbName)
		{
			Settings.DataDirName = dataDirName;
			Settings.DbName = dbName;
			MongoHelper.Initialize();
		}

		public static void TearDownFixture()
		{
			MongoHelper.Cleanup();
			Settings.Cleanup();
		}

		public static void InitializeTestEnvironment(out LanguageDepotHelper languageDepot,
			out MongoHelper mongo, out WebworkHelper webwork)
		{
			languageDepot = new LanguageDepotHelper();
			mongo = new MongoHelper($"sf_{Settings.DbName}");
			webwork = new WebworkHelper(Settings.DbName);
		}

		public static void ShutdownTestEnvironment(LanguageDepotHelper languageDepot,
			MongoHelper mongo, WebworkHelper webwork)
		{
			mongo.Dispose();
			languageDepot.Dispose();
			webwork.Dispose();
			LfMergeHelper.Cleanup();
		}

		/// <summary>
		/// Run a command with the given arguments. Returns standard output.
		/// </summary>
		public static string Run(string command, string args, string workDir,
			bool throwException = true, bool ignoreErrors = false)
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
				process.StartInfo.EnvironmentVariables.Add("TZ", "UTC");

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
	}
}
