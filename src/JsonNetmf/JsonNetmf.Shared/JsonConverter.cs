// (c) Pervasive Digital LLC
// Use of this code and resulting binaries is permitted only under the
// terms of a written license.


// skigrinder - 
// Used this define a lot in the effort to get Array serialization & deserialization working
//#define NANOFRAMEWORK_DISPLAY_DEBUG


using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Windows.Storage.Streams;



namespace PervasiveDigital.Json {
	// The protocol mantra: Be strict in what you emit, and generous in what you accept.


	public static class JsonConverter {
		private enum TokenType {
			LBrace, RBrace, LArray, RArray, Colon, Comma, String, Number, Date, Error, True, False, Null, End
		}

		private struct LexToken {
			public TokenType TType;
			public string TValue;
		}

		public class SerializationCtx {
			public int Indent;
		}

		// skigrinder - 
		// Used this a lot in the effort to get Array serialization & deserialization working
		public static void DisplayDebug(string displayString) {
#if (NANOFRAMEWORK_DISPLAY_DEBUG)
			Console.WriteLine(displayString);           // Show Serialize & Deserialize details - but only when NANOFRAMEWORK_DISPLAY_DEBUG is defined
#endif
		}


		public static SerializationCtx SerializationContext = null;
		public static object SyncObj = new object();

		// skigrinder - 
		// Made a lot of changes here to get Array serialization & deserialization working
		public static JToken Serialize(object oSource) {
			DisplayDebug($"Serialize(object oSource) - oSource.GetType(): {oSource.GetType().Name}  oSource.ToString(): {oSource.ToString()}");
			var type = oSource.GetType();
			if (type.IsArray) {
				JToken retToken = JArray.Serialize(type, oSource);
				DisplayDebug($"Serialize(object oSource) - finished after calling JArray.Serialize() ");
				return retToken;
			} else {
				JToken retToken = JObject.Serialize(type, oSource);
				DisplayDebug($"Serialize(object oSource) - finished after calling JObject.Serialize() ");
				return retToken;
			}
		}


		public static object DeserializeObject(string sourceString, Type type) {
			var dserResult = Deserialize(sourceString);
			return PopulateObject(dserResult, type, "/");
		}

		public static object DeserializeObject(Stream stream, Type type) {
			var dserResult = Deserialize(stream);
			return PopulateObject(dserResult, type, "/");
		}

		public static object DeserializeObject(DataReader dr, Type type) {
			var dserResult = Deserialize(dr);
			return PopulateObject(dserResult, type, "/");
		}

		public static string debugIndent = $"PopulateObject() - ";      // PopulateObject() goes recursive - this cleans up the debug output
		public static int debugOutdent = 10;


		// skigrinder - 
		// Made a lot of changes here to get Array serialization & deserialization working
		// Also created a lot of debug output using DisplayDebug() - this is controlled above by #define NANOFRAMEWORK_DISPLAY_DEBUG
		private static object PopulateObject(JToken rootToken, Type rootType, string rootPath) {
			if ((rootToken == null) || (rootType == null) || (rootPath == null)) {
				throw new Exception($"PopulateObject() - All parameters must be non-null.  rootToken: {(rootToken != null ? rootToken.GetType().Name : "null")}   rootType: {(rootType != null ? rootType.Name : "null")}   rootPath: {(rootPath != null ? rootPath : "null")}");
			}
			try {
				// This was tricky to get working - Arrays didn't work at all
				// Recursion made it difficult for complex objects - indenting the debug output helped a lot
				// Leave the debug here in case future testing reveals trouble - maybe get rid of it at some point
				debugIndent = "          " + debugIndent;
				DisplayDebug($"{debugIndent} Start - ");        // Simple message to make sure we get to this point - displaying null values causes the device to hang & makes debugging problematic
				DisplayDebug($"{debugIndent} rootToken is a {rootToken.GetType().Name}  rootType: {rootType.Name}   rootPath: {rootPath}");
				if (rootToken is JObject) {
					object rootInstance = null;         // This is the object that gets populated and returned
														// Create rootInstance from the rootType's constructor
					DisplayDebug($"{debugIndent} create rootInstance from rootType: {rootType.Name} using GetConstructor() & Invoke()");
					Type[] types = { };        // Empty array of Types - GetConstructor didn't work unless given an empty array of Type[]
					ConstructorInfo ci = rootType.GetConstructor(types);
					if (ci == null) {
						throw new Exception($"PopulateObject() - failed to create target instance.   rootType: {rootType.Name} ");
					}
					rootInstance = ci.Invoke(null);
					DisplayDebug($"{debugIndent} rootInstance created.  rootInstance.GetType(): {(rootInstance?.GetType()?.Name != null ? rootInstance.GetType().Name : "null")}");
					// If we haven't successfully created rootInstance, bail out
					if (rootInstance == null) {
						throw new Exception($"PopulateObject() - failed to create target instance from rootType: {rootType.Name} ");
					}

					// Everything looks good - process the rootToken as a JObject
					var rootObject = (JObject)rootToken;
					if ((rootObject == null) || (rootObject.Members == null)) {
						throw new Exception($"PopulateObject() - failed to create target instance from rootType: {rootType.Name} ");
					}

					// Process all members for this rootObject
					DisplayDebug($"{debugIndent} Entering rootObject.Members loop ");
					foreach (var m in rootObject.Members) {
						DisplayDebug($"{debugIndent} Process rootObject.Member");
						var memberProperty = (JProperty)m;
						DisplayDebug($"{debugIndent}     memberProperty.Name:  {memberProperty?.Name ?? "null"} ");
						MethodInfo memberGetMethod = rootType.GetMethod("get_" + memberProperty.Name);
						Type memberType = memberGetMethod.ReturnType;
						MethodInfo memberSetMethod = rootType.GetMethod("set_" + memberProperty.Name);
						if (memberType == null) {
							throw new Exception($"PopulateObject() - failed to create memberType from {rootType.Name}.GetMethod ");
						}
						// Process the member based on JObject, JValue, or JArray
						if (memberProperty.Value is JObject) {
							// Call PopulateObject() for this member - i.e. recursion
							DisplayDebug($"{debugIndent}     memberProperty.Value is JObject");
							var memberPath = rootPath;
							if (memberPath[memberPath.Length - 1] == '/') {
								memberPath += memberProperty.Name;                      // Don't need to add a slash before appending rootElementType
							} else {
								memberPath = memberPath + '/' + memberProperty.Name;    // Need to add a slash before appending rootElementType
							}
							var memberObject = PopulateObject(memberProperty.Value, memberType, memberPath);
							memberSetMethod.Invoke(rootInstance, new object[] { memberObject });
						} else if (memberProperty.Value is JValue) {
							// Don't need any more info - populate the member using memberSetMethod.Invoke()
							DisplayDebug($"{debugIndent}     memberProperty.Value is JValue");
							if (memberType != typeof(DateTime)) {
								memberSetMethod.Invoke(rootInstance, new object[] { ((JValue)memberProperty.Value).Value });
							} else {
								DateTime dt;
								var sdt = ((JValue)memberProperty.Value).Value.ToString();
								if (sdt.Contains("Date(")) {
									dt = DateTimeExtensions.FromASPNetAjax(sdt);
								} else {
									dt = DateTimeExtensions.FromIso8601(sdt);
								}
								memberSetMethod.Invoke(rootInstance, new object[] { dt });
							}
						} else if (memberProperty.Value is JArray) {
							DisplayDebug($"{debugIndent}     memberProperty.Value is a JArray");
							Type memberElementType = memberType.GetElementType();    // Need this type when we try to populate the array elements
							var memberValueArray = (JArray)memberProperty.Value;   // Create a JArray (memberValueArray) to hold the contents of memberProperty.Value 
							var memberValueArrayList = new ArrayList();             // Create a temporary ArrayList memberValueArrayList - populate this as the memberItems are parsed
							JToken[] memberItems = memberValueArray.Items;          // Create a JToken[] array for Items associated for this memberProperty.Value
							DisplayDebug($"{debugIndent}       copy {memberItems.Length} memberItems from memberValueArray into memberValueArrayList - call PopulateObject() for items that aren't JValue");
							foreach (JToken item in memberItems) {
								if (item is JValue) {
									DisplayDebug($"{debugIndent}         item is a JValue: {((JValue)item).Value} - Add it to memberValueArrayList");
									memberValueArrayList.Add(((JValue)item).Value);
									DisplayDebug($"{debugIndent}         item JValue added to memberValueArrayList");
								} else if (item is JToken) {
									// Since memberProperty.Value is a JArray:
									// 		memberType        is the array   type (i.e. foobar[])
									// 		memberElementType is the element type (i.e. foobar)		- use this to call PopulateObject()
									string memberElementPath = rootPath + "/" + memberProperty.Name + "/" + memberElementType.Name;
									DisplayDebug($"{debugIndent}         memberType: {memberType.Name}   memberElementType: {memberElementType.Name} ");
									DisplayDebug($"{debugIndent}         calling PopulateObject(JToken item, {memberElementType.Name}, {memberElementPath}) ");
									var itemObj = PopulateObject(item, memberElementType, memberElementPath);
									memberValueArrayList.Add(itemObj);
									DisplayDebug($"{debugIndent}         item added to memberValueArrayList");
								} else {
									DisplayDebug($"{debugIndent}         item is not a JToken or a JValue - this case is not handled");
								}
							}
							DisplayDebug($"{debugIndent}       {memberItems.Length} memberValueArray.Items copied into memberValueArrayList - i.e. contents of memberProperty.Value");

							// Create targetArray - an Array of memberElementType objects - targetArray will be copied to rootInstance - then rootInstance will be returned
							DisplayDebug($"{debugIndent}       create targetArray - an Array of memberElementType: {memberElementType} objects - use Array.CreateInstance({memberElementType}, {memberValueArray.Length}");
							Array targetArray = Array.CreateInstance(memberElementType, memberValueArray.Length);
							if (targetArray == null) {
								throw new Exception("PopulateObject() - failed to create Array of type: {memberElementType}[]");
							}
							DisplayDebug($"{debugIndent}       targetArray created using CreateInstance().  targetArray.GetType().Name: {(targetArray?.GetType()?.Name != null ? targetArray.GetType().Name : "null")}");

							memberValueArrayList.CopyTo(targetArray);
							DisplayDebug($"{debugIndent}       copied memberValueArrayList into the targetArray");
							memberSetMethod.Invoke(rootInstance, new object[] { targetArray });
							DisplayDebug($"{debugIndent}       populated the rootInstance object with the contents of targetArray -  via {memberSetMethod.Name}()");
						}   // end of (if memberProperty.Value is JArray)
					}   // end of foreach() loop on rootToken Jobject members
					debugIndent = debugIndent.Substring(debugOutdent);     // 'Outdent' before returning
					DisplayDebug($"{debugIndent} Returning rootInstance");
					return rootInstance;
					// end of (if rootToken is JObject)

				} else if (rootToken is JArray) {
					Type rootElementType = rootType.GetElementType();
					if (rootElementType == null) {
						throw new NotSupportedException($"PopulateObject() - For arrays, type: {rootType.Name} must have a valid element type");
					}
					DisplayDebug($"{debugIndent} rootType: {rootType.Name}  rootType.GetElementType(): {rootType.GetElementType().Name}");

					// Create & populate rootArrayList with the items in rootToken - call PopulateObject if the item is more complicated than a JValue 
					DisplayDebug($"{debugIndent} Create and populate rootArrayList with the items in rootToken - call PopulateObject if the item is more complicated than a JValue");
					ArrayList rootArrayList = new ArrayList();
					JArray rootArray = (JArray)rootToken;
					foreach (var item in rootArray.Items) {
						if (item is JValue) {
							DisplayDebug($"{debugIndent} item type: {item.GetType().Name}.   Adding it to rootArrayList");
							rootArrayList.Add(((JValue)item).Value);
						} else {
							DisplayDebug($"{debugIndent} item type: {item.GetType().Name} - use rootElementType to call PopulateObject()");
							// Pass rootElementType and rootPath with rootElementType appended to PopulateObject for this item 
							string itemPath = rootPath;
							if (itemPath[itemPath.Length - 1] == '/') {
								itemPath += rootElementType.Name;                   // Don't need to add a slash before appending rootElementType
							} else {
								itemPath = itemPath + '/' + rootElementType.Name;   // Need to add a slash before appending rootElementType
							}
							var itemObj = PopulateObject(item, rootElementType, itemPath);
							rootArrayList.Add(itemObj);
							DisplayDebug($"{debugIndent} added object of type: {itemObj.GetType().Name} (returned from PopulateObject()) to rootArrayList");
						}
					}
					DisplayDebug($"{debugIndent} finished creating rootArrayList - copy rootArrayList to targetArray");
					Array targetArray = Array.CreateInstance(rootType.GetElementType(), rootArray.Length);
					if (targetArray == null) {
						throw new Exception($"PopulateObject() - CreateInstance() failed for type: {rootElementType.Name}    length: {rootArray.Length}");
					}
					DisplayDebug($"{debugIndent} created Array targetArray by calling CreateInstance() with rootType.GetElementType()");
					rootArrayList.CopyTo(targetArray);
					DisplayDebug($"{debugIndent} populated targetArray with the contents of rootArrayList");
					debugIndent = debugIndent.Substring(debugOutdent);     // 'Outdent' before returning
					return targetArray;
				}   // end of  (if rootToken is JArray)
				debugIndent = debugIndent.Substring(debugOutdent);          // 'Outdent' before returning
				return null;
			} catch (Exception ex) {
				DisplayDebug($"{debugIndent} Exception: {ex.Message}");
				debugIndent = debugIndent.Substring(debugOutdent);          // 'Outdent' before returning
				return null;
			}
		}   // end of PopulateObject



		// skigrinder - 
		// Trying to deserialize a stream in nanoFramework was problematic.
		// Stream.Peek() has not been implemented in nanoFramework - and it was used at least once in the original code
		// Therefore, read all input into the static jsonBytes[] and use jsonPos to keep track of where we are when parsing the input
		public static byte[] jsonBytes;     // Do all deserialization using this byte[]
		public static int jsonPos;      // Current position in jsonBytes[]

		// skigrinder - 
		// Copy the input to jsonBytes - then Deserialize()
		public static JToken Deserialize(string sourceString) {
			jsonBytes = new byte[sourceString.Length];
			jsonBytes = Encoding.UTF8.GetBytes(sourceString);
			jsonPos = 0;
			return Deserialize();
		}

		// skigrinder - 
		// Copy the input to jsonBytes - then Deserialize()
		public static JToken Deserialize(Stream sourceStream) {
			// Read the sourcestream into jsonBytes[]
			jsonBytes = new byte[sourceStream.Length];
			sourceStream.Read(jsonBytes, 0, (int)sourceStream.Length);
			jsonPos = 0;
			return Deserialize();
		}

		// skigrinder - 
		// Copy the input to jsonBytes - then Deserialize()
		public static JToken Deserialize(DataReader dr) {
			// Read the DataReader into jsonBytes[]
			jsonBytes = new byte[dr.UnconsumedBufferLength];
			jsonPos = 0;
			while (dr.UnconsumedBufferLength > 0) {
				jsonBytes[jsonPos++] = dr.ReadByte();
			}
			jsonPos = 0;
			return Deserialize();
		}

		// skigrinder - 
		// Deserialize() now assumes that the input has been copied int jsonBytes[]
		// Keep track of position with jsonPos
		public static JToken Deserialize() {
			// Deserialize the json input data in jsonBytes[]
			//DisplayDebug($"Deserialize() - jsonPos: {jsonPos}   jsonBytes.Length: {jsonBytes.Length}");
			JToken result = null;
			LexToken token;
			token = GetNextToken();
			//DisplayDebug($"Deserialize() - JsonConverter - Deserialize() - GetNextToken() returned:  token.TType: {token.TType.TokenTypeToString()}  token.TValue: {token.TValue}");

			switch (token.TType) {
				case TokenType.LBrace:
					result = ParseObject(ref token);
					if (token.TType == TokenType.RBrace) {
						token = GetNextToken();
					} else if (token.TType != TokenType.End && token.TType != TokenType.Error) {        // MORT clean this up
						throw new Exception("unexpected content after end of object");
					}
					break;
				case TokenType.LArray:
					result = ParseArray(ref token);
					if (token.TType == TokenType.RArray) {
						token = GetNextToken();
					} else if (token.TType != TokenType.End && token.TType != TokenType.Error) {        // MORT clean this up
						throw new Exception("unexpected content after end of array");
					}
					break;
				default:
					throw new Exception("unexpected initial token in json parse");
			}
			if (token.TType != TokenType.End) {
				throw new Exception("unexpected end token in json parse");
			} else if (token.TType == TokenType.Error) {
				throw new Exception("unexpected lexical token during json parse");
			}
			return result;
		}

		public static object FromBson(byte[] buffer, Type resultType) {
			int offset = 0;
			int len = (Int32)SerializationUtilities.Unmarshall(buffer, ref offset, TypeCode.Int32);
			JToken dserResult = null;
			while (offset < buffer.Length - 1) {
				var bsonType = (BsonTypes)buffer[offset++];
				// eat the empty ename
				var idxNul = JToken.FindNul(buffer, offset);
				if (idxNul == -1)
					throw new Exception("Missing ename terminator");
				var ename = JToken.ConvertToString(buffer, offset, idxNul - offset);
				offset = idxNul + 1;
				switch (bsonType) {
					case BsonTypes.BsonDocument:
						dserResult = JObject.FromBson(buffer, ref offset);
						break;
					case BsonTypes.BsonArray:
						dserResult = JArray.FromBson(buffer, ref offset);
						break;
					default:
						throw new Exception("unexpected top-level object type in bson");
				}
			}
			if (buffer[offset++] != 0) {
				throw new Exception("bad format - missing trailing null on bson document");
			}
			return PopulateObject(dserResult, resultType, "/");
		}

		private static JObject ParseObject(ref LexToken token) {
			var result = new JObject();
			token = GetNextToken();
			while (token.TType != TokenType.End && token.TType != TokenType.Error && token.TType != TokenType.RBrace) {         // MORT clean this up
																																// Get the name from the name:value pair
				if (token.TType != TokenType.String) {
					throw new Exception("expected label");
				}
				var propName = token.TValue;
				// Look for the :
				token = GetNextToken();
				if (token.TType != TokenType.Colon) {
					throw new Exception("expected colon");
				}
				// Get the value from the name:value pair
				var value = ParseValue(ref token);
				result.Add(propName, value);
				// Look for additional name:value pairs (i.e. separated by a comma)
				token = GetNextToken();
				if (token.TType == TokenType.Comma) {
					token = GetNextToken();
				}
			}
			if (token.TType == TokenType.Error) {
				throw new Exception("unexpected token in json object");
			} else if (token.TType != TokenType.RBrace) {
				throw new Exception("unterminated json object");
			}
			return result;
		}

		private static JArray ParseArray(ref LexToken token) {
			ArrayList list = new ArrayList();
			while (token.TType != TokenType.End && token.TType != TokenType.Error && token.TType != TokenType.RArray) {         // MORT clean this up
				var value = ParseValue(ref token);
				if (value != null) {
					list.Add(value);
					token = GetNextToken();
					if (token.TType != TokenType.Comma && token.TType != TokenType.RArray) {
						throw new Exception("badly formed array");
					}
				}
			}
			if (token.TType == TokenType.Error) {
				throw new Exception("unexpected token in array");
			} else if (token.TType != TokenType.RArray) {
				throw new Exception("unterminated json array");
			}
			var result = new JArray((JToken[])list.ToArray(typeof(JToken)));
			return result;
		}

		private static JToken ParseValue(ref LexToken token) {
			token = GetNextToken();
			if (token.TType == TokenType.RArray) {
				// we were expecting a value in an array, and came across the end-of-array marker,
				//  so this is an empty array
				return null;
			} else if (token.TType == TokenType.String) {
				return new JValue(token.TValue);
			} else if (token.TType == TokenType.Number) {
				if (token.TValue.IndexOfAny(new char[] { '.', 'e', 'E' }) != -1) {
					return new JValue(double.Parse(token.TValue));
				} else {
					return new JValue(int.Parse(token.TValue));
				}
			} else if (token.TType == TokenType.True) {
				return new JValue(true);
			} else if (token.TType == TokenType.False) {
				return new JValue(false);
			} else if (token.TType == TokenType.Null) {
				return new JValue(null);
			} else if (token.TType == TokenType.Date) {
				throw new NotSupportedException("datetime parsing not supported");
			} else if (token.TType == TokenType.LBrace) {
				return ParseObject(ref token);
			} else if (token.TType == TokenType.LArray) {
				return ParseArray(ref token);
			}

			throw new Exception("invalid value found during json parse");
		}

		private static LexToken GetNextToken() {
			var result = GetNextTokenInternal();
			return result;
		}


		private static LexToken GetNextTokenInternal() {
			try {
				StringBuilder sb = null;
				char openQuote = '\0';
				char ch = ' ';
				while (true) {
					if (jsonPos >= jsonBytes.Length) {
						DisplayDebug($"GetNextTokenInternal() - JsonConverter GetNextToken() - no more data - call EndToken()");
						return EndToken(sb);
					}
					ch = (char)jsonBytes[jsonPos++];

					// Handle json escapes
					bool escaped = false;
					if (ch == '\\') {
						escaped = true;
						ch = (char)jsonBytes[jsonPos++];
						if (ch == (char)0xffff) {
							return EndToken(sb);
						}
						//TODO: replace with a mapping array? This switch is really incomplete.
						switch (ch) {
							case '\'':
								ch = '\'';
								break;
							case '"':
								ch = '"';
								break;
							case 't':
								ch = '\t';
								break;
							case 'r':
								ch = '\r';
								break;
							case 'n':
								ch = '\n';
								break;
							default:
								throw new Exception("unsupported escape");
						}
					}

					if ((sb != null) && ((ch != openQuote) || (escaped))) {
						sb.Append(ch);
					} else if (IsNumberIntroChar(ch)) {
						sb = new StringBuilder();
						while (IsNumberChar(ch)) {
							sb.Append(ch);
							// skigrinder - 
							// nanoFramework doesn't support Peek() for Streams or DataReaders
							// This is why we converted everything to a byte[] instead of trying to work directly from a Stream or a DataReader
							// Look at the next byte but don't advance jsonPos unless we're still working on the number
							// i.e. Peek() to see if we're at the end of the number
							ch = (char)jsonBytes[jsonPos];
							if (IsNumberChar(ch)) {
								jsonPos++;                      // We're still working on the number - advance jsonPos
							}

							if (ch == (char)0xffff) {
								return EndToken(sb);
							}
						}
						// Note that we don't claim that this is a well-formed number
						return new LexToken() { TType = TokenType.Number, TValue = sb.ToString() };
					} else {
						switch (ch) {
							case '{':
								return new LexToken() { TType = TokenType.LBrace, TValue = null };
							case '}':
								return new LexToken() { TType = TokenType.RBrace, TValue = null };
							case '[':
								return new LexToken() { TType = TokenType.LArray, TValue = null };
							case ']':
								return new LexToken() { TType = TokenType.RArray, TValue = null };
							case ':':
								return new LexToken() { TType = TokenType.Colon, TValue = null };
							case ',':
								return new LexToken() { TType = TokenType.Comma, TValue = null };
							case '"':
							case '\'':
								if (sb == null) {
									openQuote = ch;
									sb = new StringBuilder();
								} else {
									// We're building a string and we hit a quote character.
									// The ch must match openQuote, or otherwise we should have eaten it above as string content
									//Debug.Assert(ch == openQuote);
									return new LexToken() { TType = TokenType.String, TValue = sb.ToString() };
								}
								break;
							case ' ':
							case '\t':
							case '\r':
							case '\n':
								break; // whitespace - go around again
							case (char)0xffff:
								return EndToken(sb);
							default:
								// try to collect a token
								switch (ch.ToLower()) {
									case 't':
										Expect('r');
										Expect('u');
										Expect('e');
										return new LexToken() { TType = TokenType.True, TValue = null };
									case 'f':
										Expect('a');
										Expect('l');
										Expect('s');
										Expect('e');
										return new LexToken() { TType = TokenType.False, TValue = null };
									case 'n':
										Expect('u');
										Expect('l');
										Expect('l');
										return new LexToken() { TType = TokenType.Null, TValue = null };
									default:
										throw new Exception("unexpected character during json lexical parse");
								}   // end of switch (ch.ToLower())
						}   // end of switch (ch)
					}   // end of else
				}   // end of while (true)
			} catch (Exception e) {
				// MORT - eventually get rid of this try/catch
				DisplayDebug($"GetNextTokenInternal() - Exception caught");
				DisplayDebug($"GetNextTokenInternal() - Exception: {e.Message}");
				DisplayDebug($"GetNextTokenInternal() - StackTrace: {e.StackTrace.ToString()}");
				throw new Exception("something bad happened");
			}
		}   // end of GetNextTokenInternal()

		private static void Expect(char expected) {
			char ch = (char)jsonBytes[jsonPos++];
			if (ch.ToLower() != expected) {
				throw new Exception("unexpected character during json lexical parse");
			}
		}

		private static bool IsValidTokenChar(char ch) {
			return (ch >= 'a' && ch <= 'z') ||
				   (ch >= 'A' && ch <= 'Z') ||
				   (ch >= '0' && ch <= '9');
		}

		private static LexToken EndToken(StringBuilder sb) {
			if (sb != null) {
				return new LexToken() { TType = TokenType.Error, TValue = null };
			} else {
				return new LexToken() { TType = TokenType.End, TValue = null };
			}
		}

		// Legal first characters for numbers
		private static bool IsNumberIntroChar(char ch) {
			return (ch == '-') || (ch == '+') || (ch == '.') || (ch >= '0' & ch <= '9');
		}

		// Legal chars for 2..n'th position of a number
		private static bool IsNumberChar(char ch) {
			return (ch == '-') || (ch == '+') || (ch == '.') || (ch == 'e') || (ch == 'E') || (ch >= '0' & ch <= '9');
		}

		private static string TokenTypeToString(this TokenType val) {
			switch (val) {
				case TokenType.Colon:
					return "COLON";
				case TokenType.Comma:
					return "COMMA";
				case TokenType.Date:
					return "DATE";
				case TokenType.End:
					return "END";
				case TokenType.Error:
					return "ERROR";
				case TokenType.LArray:
					return "LARRAY";
				case TokenType.LBrace:
					return "LBRACE";
				case TokenType.Number:
					return "NUMBER";
				case TokenType.RArray:
					return "RARRAY";
				case TokenType.RBrace:
					return "RBRACE";
				case TokenType.String:
					return "STRING";
				case TokenType.Null:
					return "NULL";
				case TokenType.True:
					return "TRUE";
				case TokenType.False:
					return "FALSE";
				default:
					return "??unknown??";
			}
		}   // end of TokenTypeToString()
	}   // end of class JsonConverter
}   // end of namespace PervasiveDigital.Json
