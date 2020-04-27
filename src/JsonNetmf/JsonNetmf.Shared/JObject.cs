// (c) Pervasive Digital LLC
// Use of this code and resulting binaries is permitted only under the
// terms of a written license.


// skigrinder - 
// Used this a lot in the effort to get Array serialization & deserialization working
//#define NANOFRAMEWORK_DISPLAY_DEBUG

using System;
using System.Collections;
using System.Reflection;
using System.Text;


namespace PervasiveDigital.Json {
	public class JObject : JToken {
		private readonly Hashtable _members = new Hashtable();

		public JProperty this[string name] {
			get { return (JProperty)_members[name.ToLower()]; }
			set {
				if (name.ToLower() != value.Name.ToLower()) {
					throw new ArgumentException("index value must match property name");
				}
				_members.Add(value.Name.ToLower(), value);
			}
		}

		public ICollection Members {
			get { return _members.Values; }
		}

		public void Add(string name, JToken value) {
			_members.Add(name.ToLower(), new JProperty(name, value));
		}

		// skigrinder - 
		// Used this a lot in the effort to get Array serialization & deserialization working
		public static void DisplayDebug(string displayString) {
#if (NANOFRAMEWORK_DISPLAY_DEBUG)
		Console.WriteLine(indent + displayString);           // Show Serialize & Deserialize details - but only when NANOFRAMEWORK_DISPLAY_DEBUG is defined
#endif
		}
		public static string indent = "";


		// skigrinder - 
		// Made a lot of changes here to get Array serialization working
		public static JObject Serialize(Type type, object oSource) {
			indent = indent + "      ";         // Indent the debug output - this helps to show recursion
			DisplayDebug($"JObject.Serialize() - Start - type: {type.Name}    oSource.GetType(): {oSource.GetType().Name}");
			var result = new JObject();
			MethodInfo[] methods;
			Type elementType = null;
			if (type.IsArray) {
				elementType = type.GetElementType();
				DisplayDebug($"JObject.Serialize() - type is Array - elementType: {elementType?.Name ?? "null"} ");
			}
			// Loop through all of this type's methods - find a get_ method that can be used to serialize oSource
			methods = type.GetMethods();
			foreach (var m in methods) {
				if (!m.IsPublic) {
					continue;               // Only look at public methods
				}
				// Modified AS TINY CLR May Have issue with Getter for Chars & Length from String (see post forum)
				if (m.Name.IndexOf("get_") != 0) {
					continue;   // Only look at methods that start with 'get_'
				}
				if ((m.Name == "get_Chars") || (m.Name == "get_Length" || (m.Name == "Empty") || (m.Name == "get_IsReadOnly") || (m.Name == "get_IsFixedSize") || (m.Name == "get_IsSynchronized"))) {
					continue;   // Not all 'get_' methods have what we're looking for
				}
				var name = m.Name.Substring(4);     // take out the 'get_'
				var methodResult = m.Invoke(oSource, null);
				// It was pretty tricky getting things to work - tried lots of different combinations - needed lots of debug - keep it in case future testing reveals trouble
				// Code would be pretty simple without all this debug - maybe get rid of it at some point after things have been well proven
				DisplayDebug($"JObject.Serialize() - methods loop - method: {m.Name}   methodResult.GetType(): {methodResult.GetType().Name}  methodResult: {methodResult.ToString()}  m.DeclaringType: {m.DeclaringType.Name}");
				if (methodResult == null) {
					DisplayDebug($"JObject.Serialize() - methods loop - methodResult is null.  Calling JValue.Serialize({m.ReturnType.Name}, null) ");
					result._members.Add(name, new JProperty(name, JValue.Serialize(m.ReturnType, null)));
					DisplayDebug($"JObject.Serialize() - methods loop - added JProperty({name}, JValue.Serialize(...)) results to result._members[]");
				} else if (m.ReturnType.IsValueType || m.ReturnType == typeof(string)) {
					DisplayDebug($"JObject.Serialize() - methods loop - m.ReturnType is ValueType or string. Calling JValue.Serialize({m.ReturnType.Name}, {methodResult.ToString()}) ");
					result._members.Add(name, new JProperty(name, JValue.Serialize(m.ReturnType, methodResult)));
					DisplayDebug($"JObject.Serialize() - methods loop - added JProperty({name}, JValue.Serialize(...)) results to result._members[]");
				} else if (m.ReturnType.IsArray) {          // Original code checked m.DeclaringType - this didn't work very well - checking m.ReturnType made all the difference
					elementType = methodResult.GetType().GetElementType();
					// Tried lots of combinations to get this to work - used 'json2csharp.com' to verify the serialized result string - leave this debug here in case future testing reveals trouble  
					DisplayDebug($"JObject.Serialize() - methods loop - m.ReturnType is ValueType.  Calling JArray.Serialize({m.ReturnType.Name}, {methodResult.ToString()}) ");
					result._members.Add(name, new JProperty(name, JArray.Serialize(m.ReturnType, methodResult)));
					DisplayDebug($"JObject.Serialize() - methods loop - added JProperty({elementType.Name}, JArray.Serialize(...)) results to result._members[]");
				} else {
					DisplayDebug($"JObject.Serialize() - methods loop - calling JObject.Serialize({m.ReturnType.Name}, {methodResult.ToString()}) ");
					result._members.Add(name, new JProperty(name, JObject.Serialize(m.ReturnType, methodResult)));
					DisplayDebug($"JObject.Serialize() - methods loop - added JProperty({name}, JObject.Serialize(...)) results to result._members[]");
				}
			}   // end of method loop
			DisplayDebug($"JObject.Serialize() - methods loop finished - start fields loop");

			var fields = type.GetFields();
			foreach (var f in fields) {
				if (f.FieldType.IsNotPublic) {
					continue;
				}
				switch (f.MemberType) {
					case MemberTypes.Field:
					case MemberTypes.Property:
						var value = f.GetValue(oSource);
						if (value == null) {
							result._members.Add(f.Name, new JProperty(f.Name, JValue.Serialize(f.FieldType, null)));
						} else if (f.FieldType.IsValueType || f.FieldType == typeof(string)) {
							result._members.Add(f.Name.ToLower(), new JProperty(f.Name, JValue.Serialize(f.FieldType, value)));
						} else {
							if (f.FieldType.IsArray) {
								result._members.Add(f.Name.ToLower(), new JProperty(f.Name, JArray.Serialize(f.FieldType, value)));
							} else {
								result._members.Add(f.Name.ToLower(), new JProperty(f.Name, JObject.Serialize(f.FieldType, value)));
							}
						}
						break;
					default:
						break;
				}
			}
			DisplayDebug($"JObject.Serialize() - fields loop finished");
			DisplayDebug($"JObject.Serialize() - Finished - type: {type.Name}");
			indent = indent.Substring(6);     // 'Outdent' before returning
			return result;
		}



		// skigrinder - 
		// Eliminate all whitespace until the nanoFramework Windows.Storage and Windows.Storage.Streams libraries get fixed
		// Whitespace characters, especially newlines, are problematic when writing to ESP32 internal flash files (e.g. a JSON config file)
		// May want to put this back in if we go back to the 'pretty' format after this gets fixed.  
		//
		// Original code creates a 'pretty' string with indentation and linefeeds
		// Currently, nanoFramework FileIO.ReadText() only returns the first line (open issue #556) 
		// Need to use this workaround in client code until things get fixed:
		//		string jsonString = "";
		//		var buf = FileIO.ReadBuffer(flashFile);
		//		using (DataReader dataReader = DataReader.FromBuffer(buf)) {
		//			jsonString = dataReader.ReadString(buf.Length);
		//		}
		// However, there are other problems with FileIO in general - strings created with whitespace don't read & write consistently
		// Therefore, use the 'non-pretty' version of ToString() until the nanoFramework libraries get fixed (Windows.Storage & Windows.Storage.Streams)
		//public override string ToString() {
		//	EnterSerialization();           // set up a SerializationContext object and Lock it (via Monitor)
		//	StringBuilder sb = new StringBuilder();
		//	try {
		//		DisplayDebug($"JObject.ToString() - Start");
		//		DisplayDebug($"JObject.ToString() - {this.GetType().Name}  -  process {_members.Values.Count} members");
		//		sb.AppendLine(Indent(true) + "{");
		//		bool first = true;
		//		foreach (var member in _members.Values) {
		//			if (!first) {
		//				sb.AppendLine(",");
		//			}
		//			first = false;
		//			JProperty prop = (JProperty)member;
		//			DisplayDebug($"JObject.ToString() - member: {prop.Name}  - calling ((JProperty)member).ToString()");
		//			string resultString = ((JProperty)member).ToString();
		//			DisplayDebug($"JObject.ToString() - member: {prop.Name}  - called ((JProperty)member).ToString() -  resultString: {resultString}");
		//			sb.Append(Indent() + resultString);
		//			DisplayDebug($"JObject.ToString() - member: {prop.Name}  - added resultString to sb");
		//		}
		//		sb.AppendLine();
		//		Outdent();
		//		sb.Append(Indent() + "}");
		//		DisplayDebug($"JObject.ToString() - Finished");
		//		return sb.ToString();
		//	} catch (Exception e) {
		//		DisplayDebug($"JObject.ToString() - Exception: {e.Message}");
		//		return sb.ToString();
		//	} finally {
		//		ExitSerialization();    // Unlocks the SerializationContext object
		//	}
		//}

		// skigrinder - 
		// Eliminate all whitespace until the nanoFramework Windows.Storage and Windows.Storage.Streams libraries get fixed
		// Whitespace characters, especially newlines, are problematic when writing to ESP32 internal flash files (e.g. a JSON config file)
		// May want to put this back in if we go back to the 'pretty' format after this gets fixed.  
		// This code writes the string without indentation or newlines - i.e.more like NewtonSoft.JSON
		public override string ToString() {
			EnterSerialization();           // set up a SerializationContext object and Lock it (via Monitor)
			try {
				StringBuilder sb = new StringBuilder();
				sb.Append("{");
				bool first = true;
				foreach (var member in _members.Values) {
					if (!first) {
						sb.Append(",");
					}
					first = false;
					sb.Append(((JProperty)member).ToString());
				}
				//sb.AppendLine();
				//Outdent();
				sb.Append("}");
				return sb.ToString();
			} finally {
				ExitSerialization();    // Unlocks the SerializationContext object
			}
		}


		public override int GetBsonSize() {
			int offset = 0;
			this.ToBson(null, ref offset);
			return offset;
		}

		public override int GetBsonSize(string ename) {
			return 1 + ename.Length + 1 + this.GetBsonSize();
		}

		public override void ToBson(byte[] buffer, ref int offset) {
			int startingOffset = offset;
			// leave space for the size
			offset += 4;
			foreach (DictionaryEntry member in _members) {
				((JProperty)member.Value).ToBson(((JProperty)member.Value).Name, buffer, ref offset);
			}
			// Write the trailing nul
			if (buffer != null) {
				buffer[offset] = (byte)0;
			}
			++offset;
			// Write the completed size
			if (buffer != null) {
				SerializationUtilities.Marshall(buffer, ref startingOffset, offset - startingOffset);
			}
		}

		public override BsonTypes GetBsonType() {
			return BsonTypes.BsonDocument;
		}

		internal static JObject FromBson(byte[] buffer, ref int offset) {
			JObject result = new JObject();
			int startingOffset = offset;
			int len = (Int32)SerializationUtilities.Unmarshall(buffer, ref offset, TypeCode.Int32);
			while (offset < startingOffset + len - 1) {
				// get the element type
				var bsonType = (BsonTypes)buffer[offset++];
				// get the element name
				var idxNul = JToken.FindNul(buffer, offset);
				if (idxNul == -1) {
					throw new Exception("Missing ename terminator");
				}
				var ename = JToken.ConvertToString(buffer, offset, idxNul - offset);
				offset = idxNul + 1;

				JToken item = null;
				switch (bsonType) {
					case BsonTypes.BsonArray:
						item = JArray.FromBson(buffer, ref offset);
						break;
					case BsonTypes.BsonDocument:
						item = JObject.FromBson(buffer, ref offset);
						break;
					case BsonTypes.BsonNull:
						item = new JValue();
						break;
					case BsonTypes.BsonBoolean:
					case BsonTypes.BsonDateTime:
					case BsonTypes.BsonDouble:
					case BsonTypes.BsonInt32:
					case BsonTypes.BsonInt64:
					case BsonTypes.BsonString:
						item = JValue.FromBson(bsonType, buffer, ref offset);
						break;
				}
				result.Add(ename, item);
			}
			if (buffer[offset++] != 0) {
				throw new Exception("bad format - missing trailing null on bson document");
			}
			return result;
		}   // end of FromBson()
	}   // end of class JObject : JToken
}
