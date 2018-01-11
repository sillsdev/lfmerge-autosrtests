// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Xml.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	[TestFixture]
	public class VerifyLanguageDepotTests
	{
		class VerifyLanguageDepotDouble : VerifyLanguageDepot
		{
			public VerifyLanguageDepotDouble()
			{
				ReplaceInstance(this);
			}

			public string XmlFile { get; set; }

			protected override string FindXmlFile(XElement lexEntry)
			{
				return XmlFile;
			}
		}

		class VerifyLanguageDepotSpy : VerifyLanguageDepot
		{
			public VerifyLanguageDepotSpy()
			{
				ReplaceInstance(this);
			}

			public string XmlFile { get; private set; }

			protected override string FindXmlFile(XElement lexEntry)
			{
				XmlFile = base.FindXmlFile(lexEntry);
				return XmlFile;
			}
		}

		private VerifyLanguageDepot _VerifyLanguageDepot;

		private void CreateLexiconTestData(string fileContent)
		{
			((VerifyLanguageDepotDouble)_VerifyLanguageDepot).XmlFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			File.WriteAllText(((VerifyLanguageDepotDouble)_VerifyLanguageDepot).XmlFile, fileContent);
		}

		private static void CreateNotesTestData(string fileContent)
		{
			var fileName = Path.Combine(Settings.TempDir, "LanguageDepot", "Lexicon.fwstub.ChorusNotes");
			Directory.CreateDirectory(Path.GetDirectoryName(fileName));
			File.WriteAllText(fileName, fileContent);
		}

		[TestFixtureSetUp]
		public void FixtureSetUp()
		{
			_VerifyLanguageDepot = new VerifyLanguageDepotDouble();
		}

		[TearDown]
		public void TearDown()
		{
			var verifyLanguageDepotDouble = _VerifyLanguageDepot as VerifyLanguageDepotDouble;
			if (verifyLanguageDepotDouble == null)
				return;

			if (File.Exists(verifyLanguageDepotDouble.XmlFile))
				File.Delete(verifyLanguageDepotDouble.XmlFile);
			verifyLanguageDepotDouble.XmlFile = null;
			Settings.Cleanup();
		}

		[Test]
		public void AssertFilesContain_LexiconOnly_AllElements()
		{
			// Setup
			CreateLexiconTestData(@"
			<Lexicon>
				<LexEntry>
					<LexemeForm>
						<MoStemAllomorph>
							<Form>
								<AUni ws=""fr2"">A3</AUni>
							</Form>
						</MoStemAllomorph>
					</LexemeForm>
					<Senses>
						<ownseq>
							<Gloss>
								<AUni ws=""en3"">Agloss4</AUni>
							</Gloss>
						</ownseq>
					</Senses>
				</LexEntry>
			</Lexicon>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<Lexicon>
					<LexEntry>
						<LexemeForm>
							<MoStemAllomorph>
								<Form>
									<AUni ws=""fr2"">A3</AUni>
								</Form>
							</MoStemAllomorph>
						</LexemeForm>
						<Senses>
							<ownseq>
								<Definition expectAbsence=""true"" />
								<Gloss>
									<AUni ws=""en3"">Agloss4</AUni>
								</Gloss>
							</ownseq>
						</Senses>
					</LexEntry>
				</Lexicon>
			</root>")), Throws.Nothing);
		}

		[Test]
		public void AssertFilesContain_LexiconOnly_FileMissesElement()
		{
			// Setup
			CreateLexiconTestData(@"
			<Lexicon>
				<LexEntry>
					<LexemeForm>
						<MoStemAllomorph>
							<Form>
								<AUni ws=""fr2"">A3</AUni>
							</Form>
						</MoStemAllomorph>
					</LexemeForm>
				</LexEntry>
			</Lexicon>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<Lexicon>
					<LexEntry>
						<LexemeForm>
							<MoStemAllomorph>
								<Form>
									<AUni ws=""fr2"">A3</AUni>
								</Form>
							</MoStemAllomorph>
						</LexemeForm>
						<Senses>
							<ownseq>
								<Definition expectAbsence=""true"" />
								<Gloss>
									<AUni ws=""en3"">Agloss4</AUni>
								</Gloss>
							</ownseq>
						</Senses>
					</LexEntry>
				</Lexicon>
			</root>")), Throws.Exception.With.Message.StartsWith("  LanguageDepot: No element 'Senses' for parent 'LexEntry'"));
		}

		[Test]
		public void AssertFilesContain_LexiconOnly_FileHasDifferentValue()
		{
			// Setup
			CreateLexiconTestData(@"
			<Lexicon>
				<LexEntry>
					<LexemeForm>
						<MoStemAllomorph>
							<Form>
								<AUni ws=""fr2"">A5</AUni>
							</Form>
						</MoStemAllomorph>
					</LexemeForm>
					<Senses>
						<ownseq>
							<Gloss>
								<AUni ws=""en3"">Agloss4</AUni>
							</Gloss>
						</ownseq>
					</Senses>
				</LexEntry>
			</Lexicon>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<Lexicon>
					<LexEntry>
						<LexemeForm>
							<MoStemAllomorph>
								<Form>
									<AUni ws=""fr2"">A3</AUni>
								</Form>
							</MoStemAllomorph>
						</LexemeForm>
						<Senses>
							<ownseq>
								<Definition expectAbsence=""true"" />
								<Gloss>
									<AUni ws=""en3"">Agloss4</AUni>
								</Gloss>
							</ownseq>
						</Senses>
					</LexEntry>
				</Lexicon>
			</root>")), Throws.Exception.With.Message.StartsWith("  LanguageDepot: Different values for element 'AUni'"));
		}

		[Test]
		public void AssertFilesContain_LexiconOnly_FileContainsAdditionalElements()
		{
			// Setup
			CreateLexiconTestData(@"
			<Lexicon>
				<LexEntry>
					<LexemeForm>
						<MoStemAllomorph>
							<Form>
								<AUni ws=""fr2"">A3</AUni>
							</Form>
						</MoStemAllomorph>
					</LexemeForm>
					<Senses>
						<ownseq>
							<Gloss>
								<AUni ws=""en3"">Agloss4</AUni>
							</Gloss>
						</ownseq>
					</Senses>
				</LexEntry>
			</Lexicon>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<Lexicon>
					<LexEntry>
						<LexemeForm>
							<MoStemAllomorph>
								<Form>
									<AUni ws=""fr2"">A3</AUni>
								</Form>
							</MoStemAllomorph>
						</LexemeForm>
					</LexEntry>
				</Lexicon>
			</root>")), Throws.Nothing); // we can't detect this
		}

		[Test]
		public void AssertFilesContain_LexiconOnly_FileContainsElementThatShouldBeAbsent()
		{
			// Setup
			CreateLexiconTestData(@"
			<Lexicon>
				<LexEntry>
					<LexemeForm>
						<MoStemAllomorph>
							<Form>
								<AUni ws=""fr2"">A3</AUni>
							</Form>
						</MoStemAllomorph>
					</LexemeForm>
					<Senses>
						<ownseq>
							<Definition>
								<AUni ws=""en3"">A definition</AUni>
							</Definition>
							<Gloss>
								<AUni ws=""en3"">Agloss4</AUni>
							</Gloss>
						</ownseq>
					</Senses>
				</LexEntry>
			</Lexicon>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<Lexicon>
					<LexEntry>
						<LexemeForm>
							<MoStemAllomorph>
								<Form>
									<AUni ws=""fr2"">A3</AUni>
								</Form>
							</MoStemAllomorph>
						</LexemeForm>
						<Senses>
							<ownseq>
								<Definition expectAbsence=""true"" />
								<Gloss>
									<AUni ws=""en3"">Agloss4</AUni>
								</Gloss>
							</ownseq>
						</Senses>
					</LexEntry>
				</Lexicon>
			</root>")), Throws.Exception.With.Message.StartsWith("  LanguageDepot: Found element 'Definition' for parent 'ownseq' which should not be there"));
		}

		[Test]
		public void AssertFilesContain_LexiconAndNotes_AllElements()
		{
			// Setup
			CreateLexiconTestData(@"
			<Lexicon>
				<LexEntry>
					<LexemeForm>
						<MoStemAllomorph>
							<Form>
								<AUni ws=""fr2"">A3</AUni>
							</Form>
						</MoStemAllomorph>
					</LexemeForm>
					<Senses>
						<ownseq>
							<Gloss>
								<AUni ws=""en3"">Agloss4</AUni>
							</Gloss>
						</ownseq>
					</Senses>
				</LexEntry>
			</Lexicon>");

			CreateNotesTestData(@"
			<notes>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;tag=&amp;id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
					<message status=""open"">comment about word A</message>
				</annotation>
			</notes>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<Lexicon>
					<LexEntry>
						<LexemeForm>
							<MoStemAllomorph>
								<Form>
									<AUni ws=""fr2"">A3</AUni>
								</Form>
							</MoStemAllomorph>
						</LexemeForm>
						<Senses>
							<ownseq>
								<Definition expectAbsence=""true"" />
								<Gloss>
									<AUni ws=""en3"">Agloss4</AUni>
								</Gloss>
							</ownseq>
						</Senses>
					</LexEntry>
				</Lexicon>
				<notes>
					<annotation class=""question"" ref=""id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
						<message status=""open"">comment about word A</message>
					</annotation>
				</notes>
			</root>")), Throws.Nothing);
		}

		[Test]
		public void AssertFilesContain_NotesOnly_NoteForDifferentEntry()
		{
			// Setup
			CreateNotesTestData(@"
			<notes>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;tag=&amp;id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=X1"">
					<message status=""open"">comment about word X</message>
				</annotation>
			</notes>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<notes>
					<annotation class=""question"" ref=""id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
						<message status=""open"">comment about word A</message>
					</annotation>
				</notes>
			</root>")), Throws.Exception.With.Message.StartsWith("  LanguageDepot: Attribute 'ref' of element 'annotation' doesn't contain expected value"));
		}

		[Test]
		public void AssertFilesContain_NotesOnly_FileContainsAdditionalElement()
		{
			// Setup
			CreateNotesTestData(@"
			<notes>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;tag=&amp;id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
					<message status=""open"">comment about word A</message>
				</annotation>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;label=B1"">
					<message status=""open"">comment about word B1</message>
				</annotation>
			</notes>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<notes>
					<annotation class=""question"" ref=""id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
						<message status=""open"">comment about word A</message>
					</annotation>
				</notes>
			</root>")), Throws.Exception.With.Message.EqualTo(@"  Expected: 1
  But was:  2
"));
		}

		[Test]
		public void AssertFilesContain_NotesOnly_FileMissingElement()
		{
			// Setup
			CreateNotesTestData(@"
			<notes>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;tag=&amp;id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
					<message status=""open"">comment about word A</message>
				</annotation>
			</notes>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<notes>
					<annotation class=""question"" ref=""id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
						<message status=""open"">comment about word A</message>
					</annotation>
					<annotation class=""question"" ref=""id=ff1c7330-4202-4bea-9243-54d3beefdead&amp;label=B1"">
						<message status=""open"">comment about word B</message>
					</annotation>
				</notes>
			</root>")), Throws.Exception.With.Message.EqualTo(@"  Expected: 2
  But was:  1
"));
		}

		[Test]
		public void AssertFilesContain_NotesOnly_DifferentOrder()
		{
			// Setup
			CreateNotesTestData(@"
			<notes>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;id=ff1c7330-4202-4bea-9243-54d3beefdead&amp;label=B1"">
					<message status=""open"">comment about word B</message>
				</annotation>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;tag=&amp;id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
					<message status=""open"">comment about word A</message>
				</annotation>
			</notes>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<notes>
					<annotation class=""question"" ref=""id=be1c7330-4201-41aa-9243-54d37f3e7fb0&amp;label=A3"">
						<message status=""open"">comment about word A</message>
					</annotation>
					<annotation class=""question"" ref=""id=ff1c7330-4202-4bea-9243-54d3beefdead&amp;label=B1"">
						<message status=""open"">comment about word B</message>
					</annotation>
				</notes>
			</root>")), Throws.Exception.With.Message.StartsWith("  LanguageDepot: Attribute 'ref' of element 'annotation' doesn't contain expected value"));
		}

		[Test]
		public void AssertFilesContain_NotesWithReplies()
		{
			// Setup
			CreateNotesTestData(@"
			<notes>
				<annotation class=""question"" ref=""silfw://localhost/link?app=flex&amp;id=ff1c7330-4202-4bea-9243-54d3beefdead&amp;label=E"">
					<message status="""">FW comment on E</message>
					<message status=""open"">LF reply on E</message>
					<message status=""open"">FW reply on E</message>
				</annotation>
			</notes>");

			// SUT/Verify
			Assert.That(() => VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<notes>
					<annotation class=""question"" ref=""id=ff1c7330-4202-4bea-9243-54d3beefdead&amp;label=E"">
						<message status="""">FW comment on E</message>
						<message status=""open"">LF reply on E</message>
						<message status=""open"">FW reply on E</message>
					</annotation>
				</notes>
			</root>")), Throws.Nothing);
		}

		[Test]
		public void FindXmlFile()
		{
			// Setup
			Settings.DataDirName = "comments-data";
			Settings.DbName = "test-comment-sr";
			var languageDepot = new LanguageDepotHelper();
			languageDepot.ApplyPatches(Settings.MinModelVersion, 2);

			var sut = new VerifyLanguageDepotSpy();

			// Exercise
			VerifyLanguageDepot.AssertFilesContain(XElement.Parse(@"
			<root>
				<Lexicon>
					<LexEntry>
						<LexemeForm>
							<MoStemAllomorph>
								<Form>
									<AUni ws=""fr"">A</AUni>
								</Form>
							</MoStemAllomorph>
						</LexemeForm>
						<Senses>
							<ownseq>
								<Definition expectAbsence=""true"" />
								<Gloss>
									<AUni ws=""en"">A</AUni>
								</Gloss>
							</ownseq>
						</Senses>
					</LexEntry>
				</Lexicon>
			</root>"));

			// Verify
			Assert.That(Path.GetFileName(sut.XmlFile), Is.EqualTo("Lexicon_07.lexdb"));
		}

	}
}
