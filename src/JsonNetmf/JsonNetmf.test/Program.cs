using System;
using Microsoft.SPOT;
using PervasiveDigital.Json;

namespace JsonNetmf.test
{
    public class TestClass
    {
        public int i;
        public UInt32 ui32;
        public string aString;
        [JsonIgnore] public string ignoreme;
        [JsonProperty(Name = "otherName")] public string someName;
        public DateTime Timestamp;
    }
    public class Program
    {
        public static void Main()
        {
            var test = new TestClass()
            {
                aString = "A string",
                i = 10,
                ignoreme = "who me?",
                someName = "who?",
                Timestamp = DateTime.UtcNow
            };
            var result = JsonConverter.Serialize(test);
            Debug.Print("Serialization:");
            Debug.Print(result.ToString());
        }
    }
}
