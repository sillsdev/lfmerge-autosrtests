// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	public static class VerifyLanguageDepot
	{
		public static void AssertFilesContain(XElement expected)
		{
			foreach (var expectedLexeme in expected.Elements())
			{
				var lexemes = expectedLexeme.Elements("LexemeForm");
				if (!lexemes.Any())
					continue;

				var filename = FindXmlFile(lexemes.First());
				using (var streamReader = new StreamReader(filename))
				{
					var actualLexicon = XElement.Load(streamReader);
					var actualLexeme = actualLexicon.Element("LexEntry");
					if (actualLexeme == null)
						continue;
					VerifyTree(expectedLexeme, actualLexeme);
				}
			}
		}

		private static string FindXmlFile(XElement element)
		{
			var searchString = element.Value.Replace("<", "&lt;").Replace(">", "&gt;");
			foreach (var file in Directory.GetFiles(
				Path.Combine(LanguageDepotHelper.LdDirectory, "Linguistics", "Lexicon"),
				"Lexicon_*.lexdb"))
			{
				var content = File.ReadAllText(file);
				if (content.Contains(searchString))
					return file;
			}

			return string.Empty;
		}

		private static void VerifyTree(XElement expectedLexeme, XElement actualLexeme)
		{
			if (expectedLexeme.HasAttributes)
			{
				foreach (var expectedAttribute in expectedLexeme.Attributes())
				{
					if (expectedAttribute.Name == "expectAbsence")
						continue;
					var actualAttribute = actualLexeme.Attribute(expectedAttribute.Name);
					Assert.That(actualAttribute, Is.Not.Null,
						$"No attribute '{expectedAttribute.Name}' for element '{expectedLexeme.Name}'");
					Assert.That(actualAttribute.Value, Is.EqualTo(expectedAttribute.Value),
						$"Different values for attribute '{expectedAttribute.Name}' of element '{expectedLexeme.Name}'");
				}
			}

			if (!expectedLexeme.HasElements)

			{
				Assert.That(actualLexeme.Value, Is.EqualTo(expectedLexeme.Value),
					$"Different values for element '{expectedLexeme.Name}'");
				return;
			}

			foreach (var expectedChild in expectedLexeme.Elements())
			{
				var actualChild = actualLexeme.Element(expectedChild.Name);
				if (expectedChild.Attribute("expectAbsence") != null)
				{
					Assert.That(actualChild, Is.Null,
						$"Found element '{expectedChild.Name}' for parent '{expectedLexeme.Name}' which should not be there");
				}
				else
				{
					Assert.That(actualChild, Is.Not.Null,
						$"No element '{expectedChild.Name}' for parent '{expectedLexeme.Name}'");
					VerifyTree(expectedChild, actualChild);
				}
			}
		}
	}
}
