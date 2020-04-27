// (c) Pervasive Digital LLC
// Use of this code and resulting binaries is permitted only under the
// terms of a written license.
using System;
using System.Text;

namespace PervasiveDigital.Json {
	public class JProperty : JToken {
		public JProperty() {
		}

		public JProperty(string name, JToken value) {
			this.Name = name;
			this.Value = value;
		}

		public string Name { get; set; }
		public JToken Value { get; set; }

		public override string ToString() {
			EnterSerialization();
			StringBuilder sb = new StringBuilder();
			try {
				sb.Append('"');
				sb.Append(this.Name);

				// skigrinder - 
				// Don't put a space before or after the ':'
				// Eliminate all whitespace until the nanoFramework Windows.Storage and Windows.Storage.Streams libraries get fixed
				// Whitespace characters, especially newlines, are problematic when writing to ESP32 internal flash files (e.g. a JSON config file)
				// May want to put this back in if we go back to the 'pretty' format after this gets fixed.  
				//sb.Append("\" : ");
				sb.Append("\":");



				//// skigrinder - 
				//// Checked with Jose' - He said to stick with "True" and "False"
				//// Decided to convert to lower case anyway
				////	JSON strings with upper case fail when sent to a browser
				////	JSON strings with upper case also fail when sent to https://json2csharp.com or http://json.parser.online.fr/ 
				////
				//// Boolean types get serialized to "True" and "False".  
				//// But the convention for https://json2csharp.com is lower case (i.e. "true" and "false")
				//// The goal here is to create a JSON string that can be entered into https://json2csharp.com without errors
				// Convert Boolean values here to lower case when appending to the JSON string
				// Have to dig into this.Value to figure out if it's a Boolean
				JToken token = (JToken)this.Value;
				if (token is JValue) {      // Not all tokens are JValue - some are JObject or JArray
					JValue j = (JValue)token;
					if (j.Value.GetType().Name == "Boolean") {
						sb.Append(this.Value.ToString().ToLower());
						return sb.ToString();
					}
				}
				sb.Append(this.Value.ToString());
				return sb.ToString();
			} finally {
				ExitSerialization();
			}
		}

		public override int GetBsonSize() {
			if (this.Value == null)
				return 0;
			else
				return this.Value.GetBsonSize();
		}

		public override int GetBsonSize(string ename) {
			return 1 + ename.Length + 1 + this.GetBsonSize();
		}

		public override void ToBson(byte[] buffer, ref int offset) {
			if (this.Value != null)
				this.Value.ToBson(buffer, ref offset);
		}

		public override BsonTypes GetBsonType() {
			return this.Value.GetBsonType();
		}
	}
}
