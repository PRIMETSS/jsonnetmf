﻿using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;
using PervasiveDigital.Json;

namespace JsonNetTinyCLR.text
{
    public class ChildClass
    {
        public int one;
        public int two;
        public int three;
    }

    public class TestClass
    {
        public int i;
        public string aString;
        public string someName;
        public DateTime Timestamp;
        public int[] intArray;
        public string[] stringArray;
        public ChildClass child1;
        public ChildClass Child { get; set; }
    }

    public class Program
    {
        public static void Main()
        {
            DoArrayTest();
            DoSimpleObjectTest();
            DoComplexObjectTest();
        }

        private static void DoArrayTest()
        {
            int[] intArray = new[] { 1, 3, 5, 7, 9 };

            var result = JsonConverter.Serialize(intArray);
            var bson = result.ToBson();
            var compare = JsonConverter.FromBson(bson, typeof(int[]));
            if (!ArraysAreEqual(intArray, (Array)compare))
                throw new Exception("array test failed");
            Debug.WriteLine("Array test succeeded");
        }

        private static void DoSimpleObjectTest()
        {
            var source = new ChildClass()
            {
                one = 1,
                two = 2,
                three = 3
            };

            var serialized = JsonConverter.Serialize(source);
            var bson = serialized.ToBson();
            var compare = (ChildClass)JsonConverter.FromBson(bson, typeof(ChildClass));
            if (source.one != compare.one ||
                source.two != compare.two ||
                source.three != compare.three)
                throw new Exception("simple object test failed");
            Debug.WriteLine("simple object test passed");
        }

        private static void DoComplexObjectTest()
        {
            var test = new TestClass()
            {
                aString = "A string",
                i = 10,
                someName = "who?",
                Timestamp = new DateTime(2008,1,1),
                intArray = new[] { 1, 3, 5, 7, 9 },
                stringArray = new[] { "two", "four", "six", "eight" },
                child1 = new ChildClass() { one = 1, two = 2, three = 3 },
                Child = new ChildClass() { one = 100, two = 200, three = 300 }
            };
            var result = JsonConverter.Serialize(test);
            Debug.WriteLine("Serialization:");
            var stringValue = result.ToString();
            Debug.WriteLine(stringValue);

            var dserResult = JsonConverter.Deserialize(stringValue);
            Debug.WriteLine("After deserialization:");
            Debug.WriteLine(dserResult.ToString());

            var newInstance = (TestClass)JsonConverter.DeserializeObject(stringValue, typeof(TestClass), CreateInstance);
            if (test.i != newInstance.i ||
                test.Timestamp.ToString() != newInstance.Timestamp.ToString() ||
                test.aString != newInstance.aString ||
                test.someName != newInstance.someName ||
                !ArraysAreEqual(test.intArray, newInstance.intArray) ||
                !ArraysAreEqual(test.stringArray, newInstance.stringArray)
                )
                throw new Exception("complex object test failed");

            // bson tests
            var bson = result.ToBson();
            var compare = JsonConverter.FromBson(bson, typeof(TestClass), CreateInstance);
        }

        private static object CreateInstance(string path, string name, int length)
        {
            if (name == "intArray")
                return new int[length];
            else if (name == "stringArray")
                return new string[length];
            else
                return null;
        }

        private static bool ArraysAreEqual(Array a1, Array a2)
        {
            if (a1 == null && a2 == null)
                return true;
            if (a1 == null || a2 == null)
                return false;
            if (a1.Length != a2.Length)
                return false;
            for (int i = 0; i < a1.Length; ++i)
            {
                if (!a1.GetValue(i).Equals(a2.GetValue(i)))
                    return false;
            }
            return true;
        }

    }
}
