// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Xml.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests.TestInfrastructureTests
{
	[TestFixture]
	public class JsonToXmlTests
	{
		private static void VerifyTree(XElement actual, XElement expected)
		{
			if (expected.HasAttributes)
			{
				foreach (var expectedAttribute in expected.Attributes())
				{
					var actualAttribute = actual.Attribute(expectedAttribute.Name);
					Assert.That(actualAttribute, Is.Not.Null,
						$"No attribute '{expectedAttribute.Name}' for element '{expected.Name}'");
					Assert.That(actualAttribute.Value, Is.EqualTo(expectedAttribute.Value),
						$"Different values for attribute '{expectedAttribute.Name}' of element '{expected.Name}'");
				}
			}

			if (!expected.HasElements)

			{
				Assert.That(actual.Value, Is.EqualTo(expected.Value),
					$"Different values for element '{expected.Name}'");
				return;
			}

			for (XElement expectedChild = expected.FirstNode as XElement, actualChild = actual.FirstNode as XElement;
				expectedChild != null && actualChild != null;
				expectedChild = expectedChild.NextNode as XElement, actualChild = actualChild.NextNode as XElement)
			{
				VerifyTree(actualChild, expectedChild);
			}

			if (actual.NextNode == null)
				Assert.That(expected.NextNode, Is.Null, "Actual doesn't contain all expected elements");
			if (expected.NextNode == null)
				Assert.That(actual.NextNode, Is.Null, "Actual contains additional unexpected elements");
		}

		[Test]
		public void EmptyArray()
		{
			var xElement = JsonToXml.Convert("[]");
			VerifyTree(xElement, XElement.Parse("<root/>"));
		}

		[Test]
		public void EmptyObject()
		{
			Assert.That(() => JsonToXml.Convert("{}"),
				Throws.TypeOf<InvalidDataException>().With.Property("Message")
					.EqualTo("JSON data doesn't have an array as outermost element"));
		}

		[Test]
		public void LexiconDataOnly()
		{
			// language=json
			var json = @"[ { 'lexicon': [
			{ 'lexeme': { 'fr1' : { 'value' : 'B1' } },
				'senses' : [ {
					'definition' : { 'en1' : { 'value' : 'Bdef2' } },
					'gloss' : { 'en2' : { 'value' : '' } }
				} ] },
			{ 'lexeme': { 'fr2' : { 'value' : 'A3' } },
				'senses' : [ {
					/* no definition */
					'gloss' : { 'en3' : { 'value' : 'Agloss4' } }
				} ] }
			]}]";

			var xElement = JsonToXml.Convert(json);
			VerifyTree(xElement, XElement.Parse(
// language=xml
@"<root>
	<Lexicon>
		<LexEntry>
			<LexemeForm>
				<MoStemAllomorph>
					<Form>
						<AUni ws=""fr1"">B1</AUni>
					</Form>
				</MoStemAllomorph>
			</LexemeForm>
			<Senses>
				<ownseq>
					<Definition>
						<AStr ws=""en1"">
							<Run ws=""en1"">Bdef2</Run>
						</AStr>
					</Definition>
					<Gloss expectAbsence=""true"" />
				</ownseq>
			</Senses>
		</LexEntry>
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
</root>"));
		}

		[Test]
		public void LexiconPlusComments()
		{
			// language=json
			var json = @"[ { 'lexicon': [
			{ 'lexeme': { 'fr1' : { 'value' : 'B1' } },
				'senses' : [ {
					'definition' : { 'en1' : { 'value' : 'Bdef2' } },
					'gloss' : { 'en2' : { 'value' : '' } }
				} ] },
			{ 'lexeme': { 'fr2' : { 'value' : 'A3' } },
				'senses' : [ {
					/* no definition */
					'gloss' : { 'en3' : { 'value' : 'Agloss4' } }
				} ] }
			]}, { 'notes': [ {
				'class' : 'question' ,
				'ref' : 'A3',
				'message' : {
					'status': 'open',
					'value': 'comment about word A'
				}
			}]}]";

			var xElement = JsonToXml.Convert(json);
			VerifyTree(xElement, XElement.Parse(
// language=xml
@"<root>
	<Lexicon>
		<LexEntry>
			<LexemeForm>
				<MoStemAllomorph>
					<Form>
						<AUni ws=""fr1"">B1</AUni>
					</Form>
				</MoStemAllomorph>
			</LexemeForm>
			<Senses>
				<ownseq>
					<Definition>
						<AStr ws=""en1"">
							<Run ws=""en1"">Bdef2</Run>
						</AStr>
					</Definition>
					<Gloss expectAbsence=""true"" />
				</ownseq>
			</Senses>
		</LexEntry>
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
		<annotation class=""question"" ref=""label=A3"">
			<message status=""open"">comment about word A</message>
		</annotation>
	</notes>
</root>"));
		}

		[Test]
		public void CommentsAndReplies()
		{
			// language=json
			var json = @"[ { 'notes': [
				{ 'class' : 'question',
					'ref' : 'A',
					'message' : {
					'status': '',
					'value': 'FW comment on word A'
				} },
				{ 'class' : 'question',
				'ref' : 'E',
				'message' : {
					'status': '',
					'value': 'FW comment on E'
				}, 'replies': [
					{ 'message': {
						'status': 'open',
						'value': 'LF reply on E'
					} },
					{ 'message': {
						'status': '',
						'value': 'FW reply on E'
					} }
				] }
			]}]";

			var xElement = JsonToXml.Convert(json);
			VerifyTree(xElement, XElement.Parse(
				// language=xml
				@"<root>
	<notes>
		<annotation class=""question"" ref=""label=A"">
			<message status="""">FW comment on word A</message>
		</annotation>
		<annotation class=""question"" ref=""label=E"">
			<message status="""">FW comment on E</message>
			<message status=""open"">LF reply on E</message>
			<message status="""">FW reply on E</message>
		</annotation>
	</notes>
</root>"));
		}
	}
}
