// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using CommandLine;
using CommandLine.Text;

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
				ModelVersion = other.ModelVersion;
				Project = other.Project;
				Parent = other.Parent;
			}

			[Option("workdir", HelpText = "Where to create/find the test repo")]
			public string WorkDir { get; set; }

			[Option("model", HelpText = "FW model version", Required = true)]
			public string ModelVersion { get; set; }

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

		[VerbOption("restore", HelpText = "Restore the test data")]
		public RestoreOptions RestoreVerb { get; set;  }

		[VerbOption("save", HelpText = "Save the test data")]
		public SaveOptions SaveVerb { get; set;  }

		[VerbOption("merge", HelpText = "Merge the test data and save new patches for the merged version for both LD and Mongo data")]
		public MergeOptions MergeVerb { get; set;  }

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
