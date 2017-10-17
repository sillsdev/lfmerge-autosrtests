// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
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
		private readonly Stack<BsonDocument> _stack = new Stack<BsonDocument>();

		private VerifyMongo()
		{
		}

		private void VerifyMongoData(string expected)
		{
			if (_mongoclient == null)
				_mongoclient = new MongoClient($"mongodb://{Settings.MongoHostName}:{Settings.MongoPort}");
			var db = _mongoclient.GetDatabase($"sf_{Settings.DbName}");
			var collection = db.GetCollection<BsonDocument>("lexicon");
			var list = collection.Find(_ => true).ToListAsync();
			list.Wait();
			var actualRecords = list.Result.ToList();
			var expectedReader = new JsonTextReader(new StringReader(expected));
			expectedReader.Read();
			Debug.Assert(expectedReader.TokenType == JsonToken.StartArray, "Expected data should be an array");
			_recordNo = 0;
			foreach (var actual in actualRecords)
			{
				VerifyField(expectedReader, actual.AsBsonValue);
				_recordNo++;
			}
		}

		private void VerifyField(JsonReader expected, BsonValue actualValue)
		{
			while (expected.Read())
			{
				switch (expected.TokenType)
				{
					case JsonToken.StartArray:
						break;
					case JsonToken.StartObject:
						break;
					case JsonToken.PropertyName:
						var field = expected.Value as string;
						switch (actualValue.BsonType)
						{
							case BsonType.Document:
							{
								VerifyObject(expected, actualValue, field);
								break;
							}
							case BsonType.Array:
							{
								var array = actualValue.AsBsonArray;
								foreach (var element in array)
									VerifyObject(expected, element, field);

								break;
							}
						}

						break;
					case JsonToken.String:
					case JsonToken.Integer:
					case JsonToken.Float:
					case JsonToken.Boolean:
					case JsonToken.Date:
					case JsonToken.Bytes:
						Assert.That(actualValue.AsString, Is.EqualTo(expected.Value));
						return;
					case JsonToken.EndArray:
						break;
					case JsonToken.EndObject:
						return;
					case JsonToken.Comment:
						// we use the comment to specify fields that should not exist;
						// format: "no <fieldname>"
						var parts = expected.Value.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
						if (parts.Length > 1)
						{
							var noField = parts[1];
							var doc = _stack.Peek();
							Assert.That(doc.Elements.Count(s => s.Name == noField), Is.EqualTo(0),
								$"Field '{noField}' for record {_recordNo} exists but shouldn't");
						}
						break;
					case JsonToken.None:
					case JsonToken.Raw:
					case JsonToken.Null:
					case JsonToken.Undefined:
					case JsonToken.StartConstructor:
					case JsonToken.EndConstructor:
						break;
				}
			}
		}

		private void VerifyObject(JsonReader expected, BsonValue actualValue, string field)
		{
			var actual = actualValue.AsBsonDocument;
			_stack.Push(actual);
			Assert.That(actual.Elements.Count(s => s.Name == field), Is.EqualTo(1),
				$"Unexpected count of elements of '{field}' for record {_recordNo}");
			VerifyField(expected, actual.Elements.First(s => s.Name == field).Value);
			_stack.Pop();
		}

	}
}
