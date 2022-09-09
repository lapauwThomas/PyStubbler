using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace PyStubblerLib
{
    public static class PythonTypeConversions
    {

    

        public static List<Type> KnownTypes = new List<Type>(); //list of 

        public static Type[] AssemblyTypes;

        public static string SafePythonName(string s)
        {
            if (s == "from")
                return "from_";
            return s;
        }

        public static string ToPythonType(string s)
        {
            string rc = s;
            if (rc.EndsWith("&"))
                rc = rc.Substring(0, rc.Length - 1);

            if (rc.EndsWith("`1") || rc.EndsWith("`2"))
                rc = rc.Substring(0, rc.Length - 2);

            if (rc.EndsWith("[]"))
            {
                string partial = ToPythonType(rc.Substring(0, rc.Length - 2));
                return $"Set({partial})";
            }

            if (rc.EndsWith("*"))
                return rc.Substring(0, rc.Length - 1); // ? not sure what we can do for pointers

            if (rc.Equals("String"))
                return "str";
            if (rc.Equals("Double"))
                return "float";
            if (rc.Equals("Boolean"))
                return "bool";
            if (rc.Equals("Int32"))
                return "int";
            if (rc.Equals("Object"))
                return "object";
            return rc;
        }

        public static string ToPythonType(Type t)
        {
            try
            {

                if (AssemblyTypes.Contains(t))
                {
                    if(!KnownTypes.Contains(t))KnownTypes.Add(t);
                    return t.Name;
                }

                if (t == typeof(string))
                    return "str"; // added here for lazy evaluation. since strings implement IEnumerable<char>
                if (t == typeof(string))
                    return "str";
                if (t == typeof(double))
                    return "float";
                if (t == typeof(double))
                    return "float";
                if (t == typeof(bool))
                    return "bool";
                if (t == typeof(int))
                    return "int";

                if (t == typeof(System.Array[]))
                    return $"List";
                if (t.IsArray)
                {
                    var subtype = t.GetElementType();



                    return $"List[{ToPythonType(subtype)}]";
                }

                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    Type keyType = t.GetGenericArguments()[0];
                    Type valueType = t.GetGenericArguments()[1];
                    return $"Dict[{ToPythonType(keyType)}, {ToPythonType(valueType)}]";
                }

                if (typeof(IList).IsAssignableFrom(t))
                {
                    Type subtype = null;

                    if (t.GenericTypeArguments.Length > 0)
                        subtype = t.GenericTypeArguments[0];
                    else
                    {
                        subtype = t.GetElementType();
                    }

                    return $"List[{ToPythonType(subtype)}]";
                }

                if (typeof(IEnumerable).IsAssignableFrom(t))
                {
                    var subtype = t.GenericTypeArguments[0];
                    if (subtype == null)
                    {
                        subtype = t.GetElementType();
                    }

                    return $"Iterable[{ToPythonType(subtype)}]";
                }

                return "Any"; //here we dont know what, or it is effectively an object
                //// TODO: Figure out the right way to get at IEnumerable<T>
                //// if (t.FullName != null && t.FullName.StartsWith("System.Collections.Generic.IEnumerable`1[["))
                //if (typeof(IEnumerable).IsAssignableFrom(t))
                //{
                //    string enumerableType = t.FullName.Substring("System.Collections.Generic.IEnumerable`1[[".Length);
                //    enumerableType = enumerableType.Substring(0, enumerableType.IndexOf(','));
                //    var pieces = enumerableType.Split('.');
                //    string rc = ToPythonType(pieces[pieces.Length - 1]);
                //    return $"Iterable[{rc}]";
                //}
            }
            catch (Exception ex)
            {
                Logger logger = PSLogManager.Instance.GetCurrentClassLogger();
                logger.Warn(ex, $"Could not deduce type for {t.FullName} from [{t.AssemblyQualifiedName}");
                return "Any"; //here we dont know what, or it is effectively an object
            }

        }
    }
}
