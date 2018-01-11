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
				VerifyNotes(expectedValues, actualRecords);
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

		private void VerifyNotes(List<JToken> expectedValues, List<BsonDocument> actualRecords)
		{
			Assert.That(actualRecords.Count, Is.EqualTo(expectedValues.Count), "Mongo: Different number of notes");

			// The order in which the notes appear can be different of what Chorus has, so we
			// deal with that here
			var actualNotesByRef = new Dictionary<string, List<BsonDocument>>();
			foreach (var note in actualRecords)
			{
				var regarding = note.GetElement("regarding");
				var word = regarding.Value.ToBsonDocument().GetElement("word").Value.AsString;
				if (actualNotesByRef.ContainsKey(word))
					actualNotesByRef[word].Add(note);
				else
					actualNotesByRef.Add(word, new List<BsonDocument>(new[] { note }));
			}

			for (_recordNo = 0; _recordNo < expectedValues.Count; _recordNo++)
			{
				var expected = expectedValues[_recordNo] as JObject;
				var noteRef = ((JValue) expected.Property("ref").Value).Value as string;
				var content =
					((JValue) ((JObject) expected.Property("message").Value).Property("value").Value)
					.Value as string;
				Assert.That(actualNotesByRef.ContainsKey(noteRef), Is.True, $"Mongo: Can't find note for '{noteRef}' (record {_recordNo})");
				var actual = actualNotesByRef[noteRef].FirstOrDefault(n => n.GetElement("content").Value.AsString == content);
				Assert.That(actual, Is.Not.Null, $"Mongo: Can't find note for '{noteRef}' with content '{content}' (record {_recordNo})");
				VerifyNote(expected, actual);
			}
		}

		private void VerifyNote(JObject expected, BsonDocument actual)
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
					case "message":
						VerifyNotesMessage(prop.Value as JObject, actual);
						break;
					case "replies":
						VerifyReplies(prop.Value as JArray, actual);
						break;
					default:
						Assert.Fail($"Mongo: Unhandled property '{prop.Name}' in 'notes' element of expected data");
						break;
				}
			}
		}

		private static void VerifyReplies(JArray expectedObjs, BsonDocument actual)
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

		private static void VerifyReply(JObject expected, BsonDocument actual)
		{
			foreach (var prop in expected.Properties())
			{
				switch (prop.Name)
				{
					case "message":
						var expectedObj = prop.Value as JObject;
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
						break;
					default:
						Assert.Fail($"Mongo: Unhandled property '{prop.Name}' in 'notes' element of expected data");
						break;
				}
			}
		}

		private static void VerifyNotesMessage(JObject expectedObj, BsonDocument actual)
		{
			Assert.That(expectedObj, Is.Not.Null);
			var actualProps = actual.Elements;
			Assert.That(actualProps.Count(),
				Is.GreaterThanOrEqualTo(expectedObj.Properties().ToList().Count));
			foreach (var prop in expectedObj.Properties())
			{
				switch (prop.Name)
				{
					case "status":
					{
						var expectedValue = prop.Value as JValue;
						var expectedString = (string) expectedValue.Value;
						if (string.IsNullOrEmpty(expectedString))
							expectedString = "open";
						var actualValue = actual.GetElement(prop.Name);
						Assert.That(actualValue.Name, Is.EqualTo(prop.Name));
						Assert.That(actualValue.Value.AsString, Is.EqualTo(expectedString));
						break;
					}
					case "value":
						VerifySingleValue("content", prop.Value, actual.GetElement("content"));
						break;
					default:
						Assert.Fail($"Mongo: Unhandled property '{prop.Name}' in 'message' element of expected data");
						break;
				}
			}
		}

		private static void VerifyNotesRef(JValue expectedValue, BsonDocument actualValue)
		{
			var regarding = actualValue.Elements.First(s => s.Name == "regarding");
			var word = regarding.Value.ToBsonDocument().First(s => s.Name == "word");
			VerifySingleValue("word", expectedValue, word);
		}
	}
}
