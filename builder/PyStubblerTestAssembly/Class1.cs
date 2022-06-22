using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace PyStubblerTestAssembly
{

    public class MyCustomEventArgs:EventArgs
    {

    }

    public static class MyEmptyClass{}

    [PublicAPI]
    public class MyTestClass
    {



        private string myPrivateStringField = "privateString";
        private int myPrivateintField = 123;

        public string myPublicStringField = "publicString";
        public int myPublicIntField = 123;

        private static string StaticPrivateStringField = "privateString";
        private static int StaticPrivateintField = 123;

        public static string StaticPublicStringField = "privateString";
        public static int StaticPublicIntField = 123;

        private List<string> myPrivateStringListField;
        private List<int> myPrivateintListField;

        public List<string> myPublicStringListField;
        public List<int> myPublicIntListField;

        private static List<string> StaticPrivateStringListField;
        private static List<int> StaticPrivateintListField;

        public static List<string> StaticPublicStringListField;
        public static List<int> StaticPublicIntListField;


        public Dictionary<string, int> myDictionartyStringInt;

        public EventHandler<EventArgs> simpleEventHandlerField;
        public event EventHandler<MyCustomEventArgs> EventHandlerFieldAsProp;

        public int IntegerProperty { get; set; }
        public static int staticIntegerProperty { get; set; }

        public int IntegerPropertyPrivateSet { get; private set; }
        public int IntegerPropertyOnlyGet { get; }

        public readonly List<int> ReadonlyIntList;

        public MyTestClass()
        {

        }
        public MyTestClass(string extraParam)
        {

        }
        private MyTestClass(string extraParam, int privateInt)
        {

        }
        public double[] IMakeAnArrayOfDoubles()
        {
            return new double[100];
        }

        public ANestedPublicClass MethodUsingNestedPublicClass(ANestedPublicClass classinst)
        {
            return classinst;
        }

        public List<List<string>> IMakeAListOfListsofStrings()
        {
            return new List<List<string>>();
        }

        public float IGiveYouAFloat()
        {
            return (float)-1.2354;
        }
        public double IGiveYouADouble()
        {
            return -1.2354;
        }


        public static void MyStaticFunction()
        {

        }

        public static void MyStaticFunction(string s)
        {

        }
        public static void MyStaticFunction(IEnumerable<string> strings)
        {

        }

        public class ANestedPublicClass
        {
            public ANestedPublicClass() { }
        }

        private class ANestedPrivateClass
        {
            public ANestedPrivateClass() { }
        }
    }
}
