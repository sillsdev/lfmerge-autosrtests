// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LfMerge.AutomatedSRTests
{
	public class VerifyMongo
	{
		public static void AssertData(string expected)
		{
			new VerifyMongo().VerifyMongoData(expected);
		}

		private int _recordNo;
		private MongoClient _mongoclient;

		private VerifyMongo()
		{
		}

		private void VerifyMongoData(string expected)
		{
			if (_mongoclient == null)
				_mongoclient = new MongoClient($"mongodb://{Settings.MongoHostName}:{Settings.MongoPort}");
			var db = _mongoclient.GetDatabase($"sf_{Settings.DbName}");
			var expectedObjects = JArray.Parse(expected, new JsonLoadSettings {CommentHandling = CommentHandling.Load});
			foreach (JObject obj in expectedObjects)
			{
				VerifyCollection(db, obj);
			}
		}

		private void VerifyCollection(IMongoDatabase db, JObject expectedCollection)
		{
			var property = expectedCollection.Properties().First();

			var collectionName = property.Name;
			if (collectionName == "notes")
				collectionName = "lexiconComments";
			var collection = db.GetCollection<BsonDocument>(collectionName);
			var list = collection.Find(_ => true).ToListAsync();
			list.Wait();
			var actualRecords = list.Result.ToList();
			var expectedValues = property.Value.ToList();
			Assert.That(actualRecords.Count, Is.GreaterThanOrEqualTo(expectedValues.Count), $"Mongo: unexpected number of records for collection {collectionName}");
			if (collectionName == "lexiconComments")
				VerifyNotes(expectedValues, actualRecords, db.GetCollection<BsonDocument>("lexicon"));
			else
				VerifyLexicon(expectedValues, actualRecords);
		}

		private void VerifyLexicon(List<JToken> expectedValues, List<BsonDocument> actualRecords)
		{
			// The order in which the entries appear can be different of what Chorus has, so we
			// deal with that here
			var actualRecordsByName = new Dictionary<string, BsonDocument>();
			foreach (var entry in actualRecords)
			{
				if (entry.GetElement("isDeleted").Value.AsBoolean)
					continue;

				var lexeme = entry.GetElement("lexeme");
				var word = lexeme.Value.ToBsonDocument().Elements.First().Value.ToBsonDocument().
					GetElement("value").Value.AsString;
				if (actualRecordsByName.ContainsKey(word))
					Assert.Fail($"Mongo: Multiple lexEntries for '{word}'");
				else
					actualRecordsByName.Add(word, entry);
			}

			Assert.That(actualRecordsByName.Count, Is.GreaterThanOrEqualTo(expectedValues.Count), "Mongo: Different number of lexentries");

			for (_recordNo = 0; _recordNo < expectedValues.Count; _recordNo++)
			{
				var expected = expectedValues[_recordNo] as JObject;
				var lexeme = expected.Property("lexeme").Value as JObject;
				var ws = lexeme.Properties().First().Value as JObject;
				var value = ws.Property("value").Value as JValue;
				var word = value.Value as string;
				Assert.That(actualRecordsByName.ContainsKey(word), Is.True, $"Mongo: Can't find lexEntry for '{word}' (record {_recordNo})");
				var actual = actualRecordsByName[word];
				foreach (var prop in expected.Properties())
				{
					VerifyValue(prop.Name, prop.Value, actual);
				}
			}
		}

		private static void VerifySingleValue(string field, JToken expected, BsonElement actual)
		{
			var expectedValue = expected as JValue;
			Assert.That(expectedValue, Is.Not.Null);
			Assert.That(actual.Name, Is.EqualTo(field));
			Assert.That(actual.Value.AsString, Is.EqualTo(expectedValue.Value));
		}

		private void VerifyValue(string field, JToken expectedValue, BsonValue actualValue)
		{
			var actual = actualValue.AsBsonDocument;
			switch (expectedValue)
			{
				case JArray _:
					Assert.That(actual.Elements.Count(s => s.Name == field), Is.EqualTo(1),
						$"Mongo: Unexpected count of elements of '{field}' for record {_recordNo}");
					var child = actual.Elements.First(s => s.Name == field);
					foreach (JObject obj in expectedValue)
					{
						foreach (var prop in obj.Properties())
						{
							VerifyValue(prop.Name, prop.Value, child.Value.AsBsonArray[0]);
						}
					}
					return;
				case JValue val:
					Assert.That(actual.ElementCount, Is.EqualTo(1));
					var element = actual.Elements.First();
					VerifySingleValue(field, val, element);
					return;
				case JObject expectedObj:
					Assert.That(actual.Elements.Count(s => s.Name == field), Is.EqualTo(1),
						$"Mongo: Unexpected count of elements of '{field}' for record {_recordNo}");
					Assert.That(expectedObj, Is.Not.Null);
					var actualProps = actual.Elements;
					Assert.That(actualProps.Count(),
						Is.GreaterThanOrEqualTo(expectedObj.Properties().ToList().Count));
					foreach (var prop in expectedObj.Properties())
					{
						VerifyValue(prop.Name, prop.Value, actual.First(s => s.Name == field).Value);
					}
					return;
				default:
					Assert.Fail($"Mongo: Unexpected object type {expectedValue.GetType()}");
					return;
			}
		}

		private void VerifyNotes(List<JToken> expectedValues, List<BsonDocument> actualRecords,
			IMongoCollection<BsonDocument> lexEntries)
		{
			Assert.That(actualRecords.Count, Is.EqualTo(expectedValues.Count), "Mongo: Different number of notes");

			// The order in which the notes appear can be different of what Chorus has, so we
			// deal with that here
			var actualNotesByRef = new Dictionary<string, List<BsonDocument>>();
			foreach (var note in actualRecords)
			{
				var regarding = note.GetElement("regarding");
				var word = string.Empty;
				var field = string.Empty;
				var regardingBsonDoc = regarding.Value.ToBsonDocument();
				if (regardingBsonDoc.TryGetElement("word", out var wordElement))
					word = wordElement.Value.AsString;
				if (regardingBsonDoc.TryGetElement("field", out var fieldElement))
					field = fieldElement.Value.AsString;
				Assert.That(!string.IsNullOrEmpty(word) && !string.IsNullOrEmpty(field), Is.False, "Mongo: both 'word' and 'field' have a value in 'regarding'");
				Assert.That(string.IsNullOrEmpty(word) && string.IsNullOrEmpty(field), Is.False, "Mongo: neither 'word' nor 'field' have a value in 'regarding'");

				var key = word;
				if (string.IsNullOrEmpty(word))
				{
					key = null;
					var targetEntryId = note.GetElement("entryRef").Value.AsObjectId;
					var filter = Builders<BsonDocument>.Filter.Eq("_id", targetEntryId);

					var lexEntry = lexEntries.Find(filter).FirstOrDefaultAsync();
					lexEntry.Wait();
					if (lexEntry != null)
					{
						var lexeme = lexEntry.Result.GetElement("lexeme").Value.AsBsonDocument;
						var firstWs = lexeme.Elements.First().Value.AsBsonDocument;
						key = firstWs.GetElement("value").Value.AsString;
					}
					Assert.That(key, Is.Not.Null, "Mongo: Can't find LexEntry that comment refers to");
				}

				if (actualNotesByRef.ContainsKey(key))
					actualNotesByRef[key].Add(note);
				else
					actualNotesByRef.Add(key, new List<BsonDocument>(new[] { note }));
			}

			for (_recordNo = 0; _recordNo < expectedValues.Count; _recordNo++)
			{
				var expected = expectedValues[_recordNo] as JObject;
				var noteRef = ((JValue) expected.Property("ref").Value).Value as string;
				var content =
					((JValue) ((JObject) ((JObject) ((JArray) expected.Property("messages").Value).First())
					.Property("message").Value).Property("value").Value).Value as string;
				Assert.That(actualNotesByRef.ContainsKey(noteRef), Is.True, $"Mongo: Can't find note for '{noteRef}' (record {_recordNo})");
				var actual = actualNotesByRef[noteRef].FirstOrDefault(n => n.GetElement("content").Value.AsString == content);
				Assert.That(actual, Is.Not.Null, $"Mongo: Can't find note for '{noteRef}' with content '{content}' (record {_recordNo})");
				VerifyNote(expected, actual);
			}
		}

		private static void VerifyNote(JObject expected, BsonDocument actual)
		{
			foreach (var prop in expected.Properties())
			{
				switch (prop.Name)
				{
					case "class":
						// LanguageForge doesn't distinguish between different kinds of notes, so
						// we ignore it here.
						break;
					case "ref":
						VerifyNotesRef(prop.Value as JValue, actual);
						break;
					case "messages":
					{
						var first = ((JArray) prop.Value).First() as JObject;
						VerifyNotesContent(first.Property("message").Value as JObject, actual);
						var last = ((JArray) prop.Value).Last() as JObject;
						VerifyNotesStatus(last.Property("message").Value as JObject, actual);
						VerifyReplies(((JArray)prop.Value).SelectTokens("$..message[1:]").
							Where(o => ((JObject)o).Property("value") != null).ToList(), actual);
						break;
					}
					default:
						Assert.Fail($"Mongo: Unhandled property '{prop.Name}' in 'notes' element of expected data");
						break;
				}
			}
		}

		private static void VerifyReplies(List<JToken> expectedObjs, BsonDocument actual)
		{
			Assert.That(expectedObjs, Is.Not.Null);
			var actualObjs = actual.GetElement("replies").Value as BsonArray;
			Assert.That(actualObjs.Count, Is.GreaterThanOrEqualTo(expectedObjs.Count));
			for (var i = 0; i < expectedObjs.Count; i++)
			{
				var expectedObj = expectedObjs[i] as JObject;
				var actualObj = actualObjs[i];
				VerifyReply(expectedObj, actualObj.ToBsonDocument());
			}
		}

		private static void VerifyReply(JObject expectedObj, BsonDocument actual)
		{
			var actualProps = actual.Elements;
			Assert.That(actualProps.Count(),
				Is.GreaterThanOrEqualTo(expectedObj.Properties().ToList().Count));
			foreach (var messageProp in expectedObj.Properties())
			{
				switch (messageProp.Name)
				{
					case "status":
						// we ignore that for Mongo
						break;
					case "value":
						VerifySingleValue("content", messageProp.Value, actual.GetElement("content"));
						break;
					default:
						Assert.Fail($"Mongo: Unhandled property '{messageProp.Name}' in 'message' reply element of expected data");
						break;
				}
			}
		}

		private static void VerifyNotesContent(JObject expectedMessage, BsonDocument actual)
		{
			VerifySingleValue("content", expectedMessage.Property("value").Value,
				actual.GetElement("content"));
		}

		private static void VerifyNotesStatus(JObject expectedMessage, BsonDocument actual)
		{
			var expectedValue = expectedMessage.Property("status").Value as JValue;
			var expectedString = (string) expectedValue.Value;
			if (string.IsNullOrEmpty(expectedString))
				expectedString = "open";
			var actualValue = actual.GetElement("status");
			Assert.That(actualValue.Name, Is.EqualTo("status"));
			Assert.That(actualValue.Value.AsString, Is.EqualTo(expectedString),
				$"Mongo: Unexpected status for entry " +
				$"'{((JObject)(((JArray)expectedMessage.Parent.Parent.Parent).First() as JObject).Property("message").Value).Property("value").Value}'");
		}

		private static void VerifyNotesRef(JToken expectedValue, BsonDocument actualValue)
		{
			var regarding = actualValue.GetElement("regarding");
			if (regarding.Value.AsBsonDocument.TryGetElement("word", out var word))
			{
				VerifySingleValue("word", expectedValue, word);
				Assert.That(regarding.Value.AsBsonDocument.Contains("meaning"), Is.True);
				Assert.That(regarding.Value.AsBsonDocument.Contains("field"), Is.False);
				Assert.That(regarding.Value.AsBsonDocument.Contains("fieldNameForDisplay"), Is.False);
				Assert.That(regarding.Value.AsBsonDocument.Contains("fieldValue"), Is.False);
				Assert.That(regarding.Value.AsBsonDocument.Contains("inputSystem"), Is.False);
				Assert.That(regarding.Value.AsBsonDocument.Contains("inputSystemAbbreviation"), Is.False);
			}
			else
			{
				Assert.That(regarding.Value.AsBsonDocument.Contains("field"), Is.True);
				Assert.That(regarding.Value.AsBsonDocument.Contains("fieldNameForDisplay"), Is.True);
				Assert.That(regarding.Value.AsBsonDocument.Contains("fieldValue"), Is.True);
				Assert.That(regarding.Value.AsBsonDocument.Contains("inputSystem"), Is.True);
				Assert.That(regarding.Value.AsBsonDocument.Contains("inputSystemAbbreviation"), Is.True);
				Assert.That(regarding.Value.AsBsonDocument.Contains("word"), Is.False);
				Assert.That(regarding.Value.AsBsonDocument.Contains("meaning"), Is.False);
			}
		}
	}
}
