// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using CommandLine;
using CommandLine.Text;
using LfMerge.AutomatedSRTests;

namespace LfMerge.TestUtil
{
	public class Options
	{
		public abstract class CommonOptions
		{
			protected CommonOptions()
			{
			}

			protected CommonOptions(CommonOptions other)
			{
				WorkDir = other.WorkDir;
				DataDir = other.DataDir;
				ModelVersion = other.ModelVersion;
				Project = other.Project;
				Parent = other.Parent;
			}

			[Option("workdir", HelpText = "Where to create/find the test repo")]
			public string WorkDir { get; set; }

			[Option("datadir", HelpText = "Where to store/find the split-up revision files (default \"data\")")]
			public string DataDir { get; set; }

			[Option("model", HelpText = "FW model version")]
			public int ModelVersion { get; set; }

			[Option("project", HelpText = "Project name", Required = true)]
			public string Project { get; set; }

			public Options Parent { get; set; }

			public abstract string GetUsage();
		}

		public class RestoreOptions: CommonOptions
		{
			public RestoreOptions()
			{
			}

			public RestoreOptions(MergeOptions other): base(other)
			{
				LanguageDepotVersion = other.LanguageDepotVersion;
				MongoVersion = other.MongoVersion;
			}

			[Option("ld", HelpText = "Version of simulated LD repo to restore")]
			public int? LanguageDepotVersion { get; set; }

			[Option("mongo", HelpText = "Version of mongo database to restore")]
			public int? MongoVersion { get; set; }

			public override string GetUsage()
			{
				return Parent.GetUsage("restore");
			}
		}

		public class SaveOptions : CommonOptions
		{
			public SaveOptions()
			{
			}

			public SaveOptions(MergeOptions other) : base(other)
			{
				SaveLanguageDepot = true;
				SaveMongoDb = true;
			}

			[Option("ld", HelpText = "Save language depot test data")]
			public bool SaveLanguageDepot { get; set; }

			[Option("mongo", HelpText = "Save mongo database")]
			public bool SaveMongoDb { get; set; }

			[Option("msg", HelpText = "Commit message for patch")]
			public string CommitMsg { get; set; }

			public override string GetUsage()
			{
				return Parent.GetUsage("save");
			}
		}

		public class MergeOptions : CommonOptions
		{
			public MergeOptions()
			{
			}

			public MergeOptions(MergeOptions other) : base(other)
			{
				LanguageDepotVersion = other.LanguageDepotVersion;
				MongoVersion = other.MongoVersion;
				CommitMsg = other.CommitMsg;
			}

			[Option("ld", Required = true, HelpText = "Version of simulated LD repo to restore")]
			public int LanguageDepotVersion { get; set; }

			[Option("mongo", Required = true, HelpText = "Version of mongo database to restore")]
			public int MongoVersion { get; set; }

			[Option("msg", HelpText = "Commit message for patch")]
			public string CommitMsg { get; set; }

			public override string GetUsage()
			{
				return Parent.GetUsage("merge");
			}
		}

		public class WizardOptions : MergeOptions
		{
			[Option("usb", Required = true, HelpText = "Directory where USB stick is mounted, e.g. /media/$USER/MyUsbStick")]
			public string UsbDirectory { get; set; }

			[Option("fwprojects", HelpText = "Directory where FW projects are stored. Default: $FWROOT/Output<modelversion>DistFiles/Projects")]
			public string FwProjectDirectory { get; set; }

			[Option("fwroot", Required = true, HelpText = "FW root directory, e.g. $HOME/fwrepo/fw")]
			public string FwRoot { get; set; }

			[Option("minmodel", DefaultValue = Settings.MinModelVersion, HelpText = "Model version to start with (useful if something crashes)")]
			public int MinModel { get; set; }

			[Option("maxmodel", DefaultValue = Settings.MaxModelVersion, HelpText = "Model version to finish with (useful if something crashes)")]
			public int MaxModel { get; set; }

			[Option("newproject", DefaultValue = false, HelpText = "true to create a new project")]
			public bool NewProject { get; set; }

			public override string GetUsage()
			{
				return Parent.GetUsage("wizard");
			}

			public string GetFwProjectDirectory(int modelVersion)
			{
				return !string.IsNullOrEmpty(FwProjectDirectory)
					? FwProjectDirectory
					: Path.Combine(FwRoot, $"Output{modelVersion}", "DistFiles", "Projects");
			}
		}

		public class UpdateMongoOptions : CommonOptions
		{
			[Option("minmodel", DefaultValue = Settings.MinModelVersion, HelpText = "Model version to start with (useful if something crashes)")]
			public int MinModel { get; set; }

			[Option("maxmodel", DefaultValue = Settings.MaxModelVersion, HelpText = "Model version to finish with (useful if something crashes)")]
			public int MaxModel { get; set; }

			public override string GetUsage()
			{
				return HelpText.AutoBuild(this, "update-mongo");
			}
		}

		[VerbOption("restore", HelpText = "Restore the test data")]
		public RestoreOptions RestoreVerb { get; set;  }

		[VerbOption("save", HelpText = "Save the test data")]
		public SaveOptions SaveVerb { get; set;  }

		[VerbOption("merge", HelpText = "Merge the test data and save new patches for the merged version for both LD and Mongo data")]
		public MergeOptions MergeVerb { get; set;  }

		[VerbOption("wizard", HelpText = "Guide through the steps necessary to create test data for all supported model versions")]
		public WizardOptions WizardVerb { get; set;  }
		
		[VerbOption("update-mongo", HelpText = "Rewrite mongo patches to match current mongo version")]
		public UpdateMongoOptions UpdateMongoVerb { get; set; }

		[HelpOption("help")]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

		[HelpVerbOption]
		public string GetUsage(string verb)
		{
			return HelpText.AutoBuild(this, verb);
		}


		[ParserState]
		public IParserState LastParserState { get; set; }

		public static Tuple<string, object> ParseCommandLineArgs(string[] args)
		{
			string invokedVerb = null;
			object invokedVerbInstance = null;

			var options = new Options();
			if (!Parser.Default.ParseArguments(args, options,
				(verb, subOptions) =>
				{
					// if parsing succeeds the verb name and correct instance
					// will be passed to onVerbCommand delegate (string,object)
					invokedVerb = verb;
					invokedVerbInstance = subOptions;
				}))
			{
				Environment.Exit(Parser.DefaultExitCodeFail);
			}

			var verbOptions = invokedVerbInstance as CommonOptions;
			verbOptions.Parent = options;

			return new Tuple<string, object>(invokedVerb, invokedVerbInstance);
		}
	}
}
