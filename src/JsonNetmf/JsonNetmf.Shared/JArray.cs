// (c) Pervasive Digital LLC
// Use of this code and resulting binaries is permitted only under the
// terms of a written license.


// skigrinder - 
// Used this a lot in the effort to get Array serialization & deserialization working
//#define NANOFRAMEWORK_DISPLAY_DEBUG

using System;
using System.Collections;
using System.Text;


namespace PervasiveDigital.Json {
    public class JArray : JToken {
        private readonly JToken[] _contents;

        public JArray() {
        }

        public JArray(JToken[] values) {
            _contents = values;
        }

        // skigrinder - 
        // Used this a lot in the effort to get Array serialization & deserialization working
        public static void DisplayDebug(string displayString) {
#if (NANOFRAMEWORK_DISPLAY_DEBUG)
		Console.WriteLine(displayString);           // Show Serialize & Deserialize details - but only when NANOFRAMEWORK_DISPLAY_DEBUG is defined
#endif
        }


        // skigrinder - 
        // Made a lot of changes here to get Array serialization working
        private JArray(Array source) {
            DisplayDebug($"JArray(Array source) - Start - source type: {source.GetType().Name}  length: {source.Length}  value: {source.GetValue(0)}");
            _contents = new JToken[source.Length];
            for (int i = 0; i < source.Length; ++i) {
                DisplayDebug($"JArray(Array source) - _contents loop - i: {i}");
                var value = source.GetValue(i);
                if (value == null) {
                    throw new Exception($"JArray(Array source) - source.GetValue() returned null");
                }
                var valueType = value.GetType();
                if (valueType == null) {
                    throw new Exception($"JArray(Array source) - value.GetType() returned null");
                }
                DisplayDebug($"JArray(Array source) - valueType: {valueType.Name} ");
                if ((valueType.IsValueType) || (valueType == typeof(string))) {
                    DisplayDebug($"JArray(Array source) - valueType is ValueType or string - calling JValue.Serialize(valueType, value)");
                    _contents[i] = JValue.Serialize(valueType, value);
                } else if (valueType.IsArray) {
                    DisplayDebug($"JArray(Array source) - valueType is Array - calling JArray.Serialize(valueType, value)");
                    _contents[i] = JArray.Serialize(valueType, value);
                } else {
                    DisplayDebug($"JArray(Array source) - valueType is not Array and not ValueType or string - calling JObject.Serialize(valueType, value)");
                    _contents[i] = JObject.Serialize(valueType, value); ;
                }
            }
            DisplayDebug($"JArray(Array source) - Finished");
        }

        public int Length {
            get { return _contents.Length; }
        }

        public JToken[] Items {
            get { return _contents; }
        }

        public static JArray Serialize(Type type, object oSource) {
            return new JArray((Array)oSource);
        }

        public JToken this[int i] {
            get { return _contents[i]; }
        }

        public override string ToString() {
            EnterSerialization();       // set up a SerializationContext object and Lock it (via Monitor)
            try {
                StringBuilder sb = new StringBuilder();
                sb.Append('[');
                Indent(true);
                int prefaceLength = 0;
                bool first = true;
                foreach (var item in _contents) {
                    if (!first) {
                        if (sb.Length - prefaceLength > 72) {
                            // skigrinder - 
                            // Don't use AppendLine()
                            // Eliminate all whitespace until the nanoFramework Windows.Storage and Windows.Storage.Streams libraries get fixed
                            // Whitespace characters, especially newlines, are problematic when writing to ESP32 internal flash files (e.g. a JSON config file)
                            // May want to put this back in if we go back to the 'pretty' format after this gets fixed.  
                            //sb.AppendLine(",");
                            sb.Append(",");
                            prefaceLength = sb.Length;
                        } else {
                            sb.Append(',');
                        }
                    }
                    first = false;
                    sb.Append(item);
                }
                sb.Append(']');
                Outdent();
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

            for (int i = 0; i < _contents.Length; ++i) {
                _contents[i].ToBson(i.ToString(), buffer, ref offset);
            }

            // Write the trailing nul
            if (buffer != null)
                buffer[offset] = (byte)0;
            ++offset;

            // Write the completed size
            if (buffer != null)
                SerializationUtilities.Marshall(buffer, ref startingOffset, offset - startingOffset);
        }

        public override BsonTypes GetBsonType() {
            return BsonTypes.BsonArray;
        }

        internal static JArray FromBson(byte[] buffer, ref int offset) {
            BsonTypes elementType = (BsonTypes)0;
            int startingOffset = offset;
            int len = (Int32)SerializationUtilities.Unmarshall(buffer, ref offset, TypeCode.Int32);
            var list = new ArrayList();
            int idx = 0;
            while (offset < startingOffset + len - 1) {
                // get the element type
                var bsonType = (BsonTypes)buffer[offset++];
                if (elementType == (BsonTypes)0)
                    elementType = bsonType;
                if (bsonType != elementType)
                    throw new Exception("all array elements must be of the same type");

                // get the element name
                var idxNul = JToken.FindNul(buffer, offset);
                if (idxNul == -1)
                    throw new Exception("Missing ename terminator");
                var ename = JToken.ConvertToString(buffer, offset, idxNul - offset);
                var elemIdx = int.Parse(ename);
                if (elemIdx != idx)
                    throw new Exception("sparse arrays are not supported");
                ++idx;

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
                list.Add(item);
            }
            if (buffer[offset++] != 0)
                throw new Exception("bad format - missing trailing null on bson document");
            return new JArray((JToken[])list.ToArray(typeof(JToken)));
        }
    }
}
