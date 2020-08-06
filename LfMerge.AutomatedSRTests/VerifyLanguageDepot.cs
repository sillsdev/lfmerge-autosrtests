// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	public class VerifyLanguageDepot
	{
		private static VerifyLanguageDepot _instance;
		private int _currentRecord;

		static VerifyLanguageDepot()
		{
			_instance = new VerifyLanguageDepot();
		}

		protected static void ReplaceInstance(VerifyLanguageDepot newInstance)
		{
			_instance = newInstance;
		}

		public static void AssertFilesContain(XElement expected)
		{
			_instance.VerifyFilesContain(expected);
		}

		private void VerifyFilesContain(XElement expected)
		{
			foreach (var element in expected.Elements())
			{
				switch (element.Name.LocalName)
				{
					case "Lexicon":
						var lexEntries = element.Elements().ToList();
						for (_currentRecord = 0; _currentRecord < lexEntries.Count; _currentRecord++)
							VerifyLexEntry(lexEntries[_currentRecord]);
						break;
					case "notes":
						VerifyAnnotation(element.Elements().ToList());
						break;
					default:
						Assert.Fail($"LanguageDepot contains unexpected element of kind {element.Name}");
						break;
				}
			}
		}

		private void VerifyLexEntry(XElement expectedLexEntry)
		{
			var filename = FindXmlFile(expectedLexEntry);
			Assert.That(filename, Is.Not.Empty,
				$"LanguageDepot: Can't find expected LexEntry: {GetLexEntrySearchString(expectedLexEntry).Replace("\r", "").Replace("\n", "")}");
			using (var streamReader = new StreamReader(filename))
			{
				var actualLexicon = XElement.Load(streamReader);
				var actualLexeme = actualLexicon.Elements("LexEntry").
					FirstOrDefault(element => NormalizeString(GetLexEntrySearchString(element)) == NormalizeString(GetLexEntrySearchString(expectedLexEntry)));
				Assert.That(actualLexeme, Is.Not.Null, $"LanguageDepot: Missing LexEntry: '{NormalizeStringForMessage(GetLexEntrySearchString(expectedLexEntry))}'");
				VerifyTree(expectedLexEntry, actualLexeme);
			}
		}

		private static string NormalizeString(string input)
		{
			return input.Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "")
				.Replace("<", "&lt;").Replace(">", "&gt;");
		}

		private static string NormalizeStringForMessage(string input)
		{
			return input.Replace("\r", "").Replace("\n", "").Replace("\t", "");
		}

		protected virtual string FindXmlFile(XElement lexEntry)
		{
			var searchString = NormalizeString(GetLexEntrySearchString(lexEntry));
			foreach (var file in Directory.GetFiles(
				Path.Combine(LanguageDepotHelper.LdDirectory, "Linguistics", "Lexicon"),
				"Lexicon_*.lexdb"))
			{
				var content = NormalizeString(File.ReadAllText(file));
				if (content.Contains(searchString))
					return file;
			}

			return string.Empty;
		}

		private static string GetLexEntrySearchString(XElement lexEntry)
		{
			var lexeme = lexEntry.Element("LexemeForm");
			return lexeme.Element("MoStemAllomorph").Element("Form").ToString();
		}

		private void VerifyTree(XElement expectedElement, XElement actualElement)
		{
			if (expectedElement.HasAttributes)
			{
				foreach (var expectedAttribute in expectedElement.Attributes())
				{
					if (expectedAttribute.Name == "expectAbsence")
						continue;
					var actualAttribute = actualElement.Attribute(expectedAttribute.Name);
					Assert.That(actualAttribute, Is.Not.Null,
						$"LanguageDepot: No attribute '{expectedAttribute.Name}' for element '{expectedElement.Name}' (record {_currentRecord})");
					if (actualAttribute.Name == "ref")
						Assert.That(actualAttribute.Value, Does.Contain(expectedAttribute.Value),
							$"LanguageDepot: Attribute '{expectedAttribute.Name}' of element '{expectedElement.Name}' doesn't contain expected value (record {_currentRecord})");
					else
						Assert.That(actualAttribute.Value, Is.EqualTo(expectedAttribute.Value),
							$"LanguageDepot: Different values for attribute '{expectedAttribute.Name}' of element '{expectedElement.Name}' (record {_currentRecord})");
				}
			}

			if (!expectedElement.HasElements)

			{
				Assert.That(actualElement.Value, Is.EqualTo(expectedElement.Value),
					$"LanguageDepot: Different values for element '{expectedElement.Name}' (record {_currentRecord})");
				return;
			}

			var processedMessage = false;
			foreach (var expectedChild in expectedElement.Elements())
			{
				if (expectedChild.Name == "message")
				{
					if (processedMessage)
						continue;

					var expectedChildren = expectedElement.Elements(expectedChild.Name).ToList();
					var actualChildren = actualElement.Elements().Where(element => element.Name == expectedChild.Name).ToList();
					for (var i = 0; i < expectedChildren.Count; i++)
					{
						var expectedMessage = expectedChildren[i];
						var actualMessage = actualChildren[i];
						VerifyTree(expectedMessage, actualMessage);
					}

					processedMessage = true;
					continue;
				}

				var actualChild = actualElement.Element(expectedChild.Name);
				if (expectedChild.Attribute("expectAbsence") != null)
				{
					Assert.That(actualChild, Is.Null,
						$"LanguageDepot: Found element '{expectedChild.Name}' for parent '{expectedElement.Name}' which should not be there (record {_currentRecord})");
				}
				else
				{
					Assert.That(actualChild, Is.Not.Null,
						$"LanguageDepot: No element '{expectedChild.Name}' for parent '{expectedElement.Name}' (record {_currentRecord})");
					VerifyTree(expectedChild, actualChild);
				}
			}
		}

		private void VerifyAnnotation(List<XElement> expectedAnnotations)
		{
			var fileName = Path.Combine(Settings.TempDir, "LanguageDepot", "Lexicon.fwstub.ChorusNotes");
			using (var streamReader = new StreamReader(fileName))
			{
				var actualNotes = XElement.Load(streamReader);
				var actualAnnotations = actualNotes.Elements("annotation").ToList();
				Assert.That(actualAnnotations.Count, Is.EqualTo(expectedAnnotations.Count));
				for (_currentRecord = 0; _currentRecord < expectedAnnotations.Count; _currentRecord++)
				{
					VerifyTree(expectedAnnotations[_currentRecord], actualAnnotations[_currentRecord]);
				}
			}
		}

	}
}
