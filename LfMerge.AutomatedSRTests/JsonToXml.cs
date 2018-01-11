// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json;
using Palaso.Extensions;

namespace LfMerge.AutomatedSRTests
{
	/// <summary>
	/// Converts the JSON datastructure which describes the expected data for the tests into
	/// a corresponding XML form that can be used to verify language depot data.
	/// </summary>
	public class JsonToXml
	{
		private JsonTextReader _reader;
		private readonly Stack<XElement> _popStack;
		private readonly XElement _root;

		public static XElement Convert(string expected)
		{
			return new JsonToXml().Translate(expected);
		}

		private JsonToXml()
		{
			_popStack = new Stack<XElement>();
			_root = new XElement("root");
			_popStack.Push(_root);
		}

		private XElement Translate(string expected)
		{
			_reader = new JsonTextReader(new StringReader(expected));
			while (_reader.Read())
			{
				switch (_reader.TokenType)
				{
					case JsonToken.StartArray:
						break;
					case JsonToken.StartObject:
						break;
					case JsonToken.EndObject:
						_popStack.Pop();
						break;
					case JsonToken.EndArray:
						if (_popStack.Count == 1)
							return _popStack.Pop();
						break;
					case JsonToken.PropertyName:
					{
						var element = ProcessPropertyName();
						if (element != null)
							_popStack.Peek().Add(element);
						break;
					}
					case JsonToken.Comment:
						_popStack.Peek().Add(ProcessComment());
						break;
					case JsonToken.Date:
					case JsonToken.Bytes:
					case JsonToken.Integer:
					case JsonToken.Float:
					case JsonToken.String:
					case JsonToken.Boolean:
						break;
					case JsonToken.Null:
					case JsonToken.Undefined:
					case JsonToken.None:
					case JsonToken.StartConstructor:
					case JsonToken.EndConstructor:
					case JsonToken.Raw:
						break;
				}
			}

			if (_popStack.Count > 0)
				throw new InvalidDataException("JSON data didn't end with an end-of-array token");
			throw new InvalidDataException("JSON data doesn't have an array as outermost element");
		}

		private XElement ProcessComment()
		{
			// we use the comment to specify fields that should not exist;
			// format: "no <fieldname>"
			XElement child = null;
			var parts = _reader.Value.ToString()
				.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length > 1)
			{
				if (parts[0] == "no")
				{
					child = new XElement(parts[1].ToUpperFirstLetter());
					child.Add(new XAttribute("expectAbsence", true));
				}
			}
			return child;
		}

		private XElement ProcessPropertyName()
		{
			XElement element = null;
			switch (_reader.Value as string)
			{
				case "lexicon":
					// 'lexicon': [ { 'lexeme': {  } } ]
					_popStack.Push(AddChildElement(_root, "Lexicon"));
					return null;
				case "lexeme":
				{
					// "lexeme": { "fr" : { "value" : "lf1<br/>" } }
					_popStack.Push(AddChildElement(_popStack.Peek(), "LexEntry"));
					element = new XElement("LexemeForm");
					element.Add(ProcessLexeme());
					break;
				}
				case "senses":
					element = new XElement("Senses");
					element.Add(ProcessSenses());
					break;
				case "definition":
				{
					// "definition" : { "en" : { "value" : "Word added by LF<br/>" } }
					element = new XElement("Definition");
					var aStr = ProcessAStrElement();
					if (string.IsNullOrEmpty(aStr.Element("Run").Value))
						element.Add(new XAttribute("expectAbsence", true));
					else
						element.Add(aStr);
					break;
				}
				case "gloss":
				{
					// "gloss" : { "en" : { "value" : "created in FLEx" } },
					element = new XElement("Gloss");
					var aUni = ProcessAUniElement();
					if (string.IsNullOrEmpty(aUni.Value))
						element.Add(new XAttribute("expectAbsence", true));
					else
						element.Add(aUni);
					break;
				}
				case "partOfSpeech":
					// "partOfSpeech" : { "value" : "adv1" }
					// How do we deal with POS? For now just ignore
					Read(JsonToken.StartObject); // {
					Read(JsonToken.PropertyName); // "value"
					Read(JsonToken.String); // "adv1"
					Read(JsonToken.EndObject); // }
					break;
				case "notes":
				{
					// 'notes': [ { 'class': {  } } ]
					var notes = ProcessNotes();
					Read(JsonToken.EndObject);
					_root.Add(notes);
					return null;
				}
			}

			return element;
		}

		private XContainer ProcessSenses()
		{
			// [ { "gloss" : { "en" : { "value" : "created in FLEx" } },
			//     "partOfSpeech" : { "value" : "adv1" } } ]
			Read(JsonToken.StartArray); // [
			Read(JsonToken.StartObject); // {
			var ownseq = new XElement("ownseq");
			while (_reader.Read())
			{
				switch (_reader.TokenType)
				{
					case JsonToken.PropertyName:
						ownseq.Add(ProcessPropertyName());
						break;
					case JsonToken.StartObject:
					case JsonToken.EndObject:
						break;
					case JsonToken.EndArray:
						return ownseq;
					case JsonToken.Comment:
						ownseq.Add(ProcessComment());
						break;
				}
			}

			return ownseq;
		}

		private XElement ProcessLexeme()
		{
			//  { "fr" : { "value" : "lf1<br/>" } }
			var mostem = new XElement("MoStemAllomorph");
			var form = new XElement("Form");
			form.Add(ProcessAUniElement());
			mostem.Add(form);
			return mostem;
		}

		private XElement ProcessAUniElement()
		{
			//  { "fr" : { "value" : "lf1<br/>" } }
			Read(JsonToken.StartObject); // {
			var aUni = new XElement("AUni");
			AddWritingSystemAttribute(aUni); // "fr"
			Read(JsonToken.StartObject); // {
			Read(JsonToken.PropertyName); // "value"
			Debug.Assert(_reader.Value as string == "value");
			Read(JsonToken.String); // "lf1<br/>"
			aUni.Add((string) _reader.Value);
			Read(JsonToken.EndObject); // }
			Read(JsonToken.EndObject); // }
			return aUni;
		}

		private XElement ProcessAStrElement()
		{
			// { "en" : { "value" : "Word added by LF<br/>" } }
			Read(JsonToken.StartObject); // {
			var aStr = new XElement("AStr");
			var ws = AddWritingSystemAttribute(aStr); // "en"
			Read(JsonToken.StartObject); // {
			Read(JsonToken.PropertyName); // "value"
			Debug.Assert(_reader.Value as string == "value");
			Read(JsonToken.String); // "Word added by LF<br/>"
			var run = new XElement("Run");
			AddWritingSystemAttribute(run, ws);
			run.Add((string) _reader.Value);
			aStr.Add(run);
			Read(JsonToken.EndObject); // }
			Read(JsonToken.EndObject); // }
			return aStr;
		}

		private void Read(JsonToken token)
		{
			_reader.Read();
			Debug.Assert(_reader.TokenType == token);
		}

		private string AddWritingSystemAttribute(XContainer auni, string ws = null)
		{
			if (ws == null)
			{
				Read(JsonToken.PropertyName); // "fr"
				ws = _reader.Value as string;
			}
			auni.Add(new XAttribute("ws", ws));
			return ws;
		}

		private XElement AddChildElement(XContainer parent, string name)
		{
			var element = new XElement(name);
			parent?.Add(element);
			return element;
		}

		private XElement ProcessNotes()
		{
			var notes = new XElement("notes");
			Read(JsonToken.StartArray);

			XElement currentNote = null;
			while (_reader.Read())
			{
				switch (_reader.TokenType)
				{
					case JsonToken.PropertyName:
					{
						XAttribute attr;
						switch (_reader.Value as string)
						{
							case "class":
								attr = new XAttribute("class", true);
								Read(JsonToken.String);
								attr.Value = (string)_reader.Value;
								currentNote.Add(attr);
								break;
							case "ref":
								attr = new XAttribute("ref", true);
								Read(JsonToken.String);
								attr.Value = $"label={_reader.Value}";
								currentNote.Add(attr);
								break;
							case "message":
								currentNote.Add(ProcessMessage());
								break;
							case "replies":
								ProcessReplies(currentNote);
								break;
						}
						break;
					}
					case JsonToken.StartObject:
						currentNote = new XElement("annotation");
						notes.Add(currentNote);
						break;
					case JsonToken.EndObject:
						break;
					case JsonToken.EndArray:
						return notes;
				}
			}
			return null;
		}

		private void ProcessReplies(XContainer currentNote)
		{
			Read(JsonToken.StartArray);
			while (_reader.Read())
			{
				switch (_reader.TokenType)
				{
					case JsonToken.PropertyName:
					{
						Debug.Assert((string) _reader.Value == "message");
						currentNote.Add(ProcessMessage());
						break;
					}
					case JsonToken.EndArray:
						return;
					case JsonToken.StartObject:
					case JsonToken.EndObject:
						break;
					default:
						throw new Exception($"Unexpected token of type {_reader.TokenType}");
				}
			}
		}

		private XElement ProcessMessage()
		{
			var message = new XElement("message");
			Read(JsonToken.StartObject);

			while (_reader.Read())
			{
				switch (_reader.TokenType)
				{
					case JsonToken.PropertyName:
					{
						switch (_reader.Value as string)
						{
							case "status":
								var attr = new XAttribute("status", true);
								Read(JsonToken.String);
								attr.Value = (string) _reader.Value;
								message.Add(attr);
								break;
							case "value":
								Read(JsonToken.String);
								message.Add((string) _reader.Value);
								break;
						}
						break;
					}
					case JsonToken.EndObject:
						return message;
				}
			}
			return null;
		}
	}
}
