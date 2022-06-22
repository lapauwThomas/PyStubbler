using System;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NLog;
using PyStubblerAnnotations;

namespace PyStubblerLib
{
    public class StubBuilder
    {
        Logger logger = PSLogManager.Instance.GetCurrentClassLogger();

        public static string logfile = PSLogManager.Logfilename;

  
      
        private static List<string> SearchPaths { get; set; } = new List<string>();
        public Type[] TypesInAssemblyToStub;

        //public static string BuildAssemblyStubs(string targetAssemblyPath, string destPath = null, string[] searchPaths = null, BuildConfig cfgs = null)
        //{
        //    var builder = new StubBuilder();
        //    return builder.GenerateStubs(targetAssemblyPath, destPath, searchPaths, cfgs);
        //}

        public List<string> errorsList { get; private set; } = new List<string>();

        public string BuildAssemblyStubs(string targetAssemblyPath, string destPath = null, string[] searchPaths = null, BuildConfig cfgs = null)
        {
            // prepare configs
            if (cfgs is null)
                cfgs = new BuildConfig();

            logger.Info($"PyStubbler version [{typeof(StubBuilder).Assembly.GetName().Version}]");
            logger.Info($"Stubbing assembly at path: {targetAssemblyPath}");

            PSLogManager.SetLogLevel(cfgs.LogLevel);

            // prepare resolver
            AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolve;
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;

            // pick a dll and load
            Assembly assemblyToStub = Assembly.LoadFrom(targetAssemblyPath);
            SearchPaths.Add(targetAssemblyPath);
            if (searchPaths != null)
                SearchPaths.AddRange(searchPaths);

            logger.Log(LogLevel.Info, SearchPaths.LogIEnumerable("Search paths: \n"));


            // extract types from assembly
            TypesInAssemblyToStub = assemblyToStub.GetExportedTypes();

            logger.Log(LogLevel.Trace, TypesInAssemblyToStub.LogIEnumerable("Types to stub: \n"));


            PythonTypeConversions.AssemblyTypes = TypesInAssemblyToStub;

            string rootNamespace = TypesInAssemblyToStub[0].Namespace.Split('.')[0];

            // prepare output directory
            DirectoryInfo outputPath;
            if (cfgs.DestPathIsRoot && Directory.Exists(destPath))
            {
                outputPath = new DirectoryInfo(destPath);

            }
            else
            {
                var extendedRootNs = cfgs.Prefix + rootNamespace + cfgs.Postfix;
                if (destPath is null)
                    outputPath = Directory.CreateDirectory(extendedRootNs);
                else
                    outputPath = Directory.CreateDirectory(Path.Combine(destPath, extendedRootNs));
            }
            logger.Debug($"Output Path: {outputPath.FullName}");


            // build dict of namespaces with their contained types
            var stubDictionary = new Dictionary<string, List<Type>>();
            foreach (var stubType in TypesInAssemblyToStub)
            {
                //if namespace for type is not registered.
                if (!stubDictionary.ContainsKey(stubType.Namespace))
                {
                    stubDictionary[stubType.Namespace] = new List<Type>(); //add namespace to 
                }
                stubDictionary[stubType.Namespace].Add(stubType);
            }

            string[] namespacesInAssembly = stubDictionary.Keys.ToArray();

            logger.Log(LogLevel.Trace, namespacesInAssembly.LogIEnumerable("Namespaces found: \n"));


            // generate stubs for each type
            foreach (var typesInCurrentNamespace in stubDictionary.Values)
            {
                WriteStubList(outputPath, namespacesInAssembly, typesInCurrentNamespace);
            }

            UpdateSetupPy(outputPath, assemblyToStub);

            return outputPath.FullName;
        }

        private void UpdateSetupPy(DirectoryInfo stubsDirectory, Assembly assemblyToStub)
        {
            // update the setup.py version with the matching version of the assembly
            var parentDirectory = stubsDirectory.Parent;
            string setup_py = Path.Combine(parentDirectory.FullName, "setup.py");
            if (File.Exists(setup_py))
            {
                string[] contents = File.ReadAllLines(setup_py);
                for (int i = 0; i < contents.Length; i++)
                {
                    string line = contents[i].Trim();
                    if (line.StartsWith("version="))
                    {
                        line = contents[i].Substring(0, contents[i].IndexOf("="));
                        var version = assemblyToStub.GetName().Version;
                        line = line + $"=\"{version.Major}.{version.Minor}.{version.Build}\",";
                        contents[i] = line;
                    }
                }

                File.WriteAllLines(setup_py, contents);
                logger.Info($"Updating setup.py at path: {setup_py}");
            }
        }

        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string assemblyToResolve = args.Name.Substring(0, args.Name.IndexOf(',')) + ".dll";

            // try to find the dll in given search paths
            foreach (var searchPath in SearchPaths)
            {
                string assemblyPath = Path.Combine(searchPath, assemblyToResolve);
                if (File.Exists(assemblyPath))
                {
                    var logger = PSLogManager.Instance.GetCurrentClassLogger();
                    logger.Info($"Loaded assembly: {assemblyPath}");
                    return Assembly.LoadFrom(assemblyPath);
                }
            }

            // say i don't know
            return null;
        }

        private string[] GetChildNamespaces(string parentNamespace, string[] allNamespaces)
        {
            List<string> childNamespaces = new List<string>();
            foreach (var ns in allNamespaces)
            {
                if (ns.StartsWith(parentNamespace + "."))
                {
                    string childNamespace = ns.Substring(parentNamespace.Length + 1);
                    if (!childNamespace.Contains("."))
                        childNamespaces.Add(childNamespace);
                }
            }
            childNamespaces.Sort();
            return childNamespaces.ToArray();
        }


        private void WriteStubList(DirectoryInfo rootDirectory, string[] allNamespaces, List<Type> TypesToStub)
        {
            string CurrentNamespace = TypesToStub[0].Namespace;
            logger.Info($"Creating stubs for namespace: {CurrentNamespace}");

            logger.Trace(TypesToStub.LogIEnumerable($"Types in namespace [{CurrentNamespace}]: \n"));

            // sort the stub list so we get consistent output over time
            TypesToStub.Sort((a, b) => { return a.Name.CompareTo(b.Name); });

            string[] ns = CurrentNamespace.Split('.');
            string path = rootDirectory.FullName;
            for (int i = 1; i < ns.Length; i++)
                path = Path.Combine(path, ns[i]);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                logger.Trace("Creating directory at {path}");
            }

            path = Path.Combine(path, "__init__.pyi");
            try
            {
                var sb = new System.Text.StringBuilder();

                WriteStubHeader(sb, CurrentNamespace, TypesToStub[0].Assembly.GetName().Name);

                string[] allChildNamespaces = GetChildNamespaces(CurrentNamespace, allNamespaces);
                if (allChildNamespaces.Length > 0)
                {
                    sb.Append("__all__ = [");
                    for (int i = 0; i < allChildNamespaces.Length; i++)
                    {
                        if (i > 0)
                            sb.Append(",");
                        sb.Append($"'{allChildNamespaces[i]}'");
                    }

                    sb.AppendLine("]");
                }

                sb.AppendLine("from typing import Tuple, Set, Iterable, List");
                sb.AppendLine("from typing import Dict");




                foreach (var stubType in TypesToStub)
                {
                    WriteStubForType(sb, stubType, ns);
                }

                File.WriteAllText(path, sb.ToString());
                logger.Info($"Finished writing stubs for {CurrentNamespace} to {path}");
            }
            catch (Exception ex)
            {
                errorsList.Add($"Error creating stubs for {CurrentNamespace}");
                logger.Error(ex, $"Exception while creating stubs for {CurrentNamespace}");
            }
        }

        private void WriteStubForType(StringBuilder sb, Type stubType, string[] ns)
        {
            if (stubType.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                return;

            if (stubType.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                return;

            sb.AppendLine();
            sb.AppendLine();
            if (stubType.IsGenericType)
                return;

            if (stubType.IsEnum)
            {
                sb.AppendLine($"class {stubType.Name}(Enum):");
                var names = Enum.GetNames(stubType);
                var values = Enum.GetValues(stubType);
                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    if (name.Equals("None", StringComparison.Ordinal))
                        name = $"#{name}";

                    object val = Convert.ChangeType(values.GetValue(i), Type.GetTypeCode(stubType));
                    sb.AppendLine($"    {name} = {val}");
                }

                return;
            }

            if (stubType.BaseType != null &&
                stubType.BaseType.FullName.StartsWith(ns[0]) &&
                stubType.BaseType.FullName.IndexOf('+') < 0 &&
                stubType.BaseType.FullName.IndexOf('`') < 0
               )
                sb.AppendLine($"class {stubType.Name}({stubType.BaseType.Name}):");
            else
                sb.AppendLine($"class {stubType.Name}:");
            string classStartString = sb.ToString();


            WriteStaticFields(sb, stubType);

            WriteFields(sb, stubType);

            WriteStaticEventHandlers(sb, stubType);

            WriteEventHandlers(sb, stubType);

            WriteConstructors(sb, stubType);

            WriteStaticProperties(sb, stubType);

            WriteProperties(sb, stubType);

            WriteMethods(sb, stubType);

            // If no strings appended, class is empty. add "pass"
            if (sb.ToString().Length == classStartString.Length)
            {
                sb.AppendLine($"    pass");
            }

            return;
        }

        private void WriteMethods(StringBuilder sb, Type stubType)
        {
            // methods
            MethodInfo[] methods = stubType.GetMethods();
            if (methods.Length == 0)
                return;
            // sort for consistent output
            Array.Sort(methods, MethodCompare);
            Dictionary<string, int> methodNames = new Dictionary<string, int>();
            foreach (var method in methods)
            {
                if (method.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                    continue;
                if (method.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;

                int count;
                if (methodNames.TryGetValue(method.Name, out count))
                    count++;
                else
                    count = 1;
                methodNames[method.Name] = count;
            }
            sb.AppendLine("#Class Methods");

            foreach (var method in methods)
            {
                WriteMethod(sb, stubType, method, methodNames);
            }
        }

        private void WriteMethod(StringBuilder sb, Type stubType, MethodInfo method, Dictionary<string, int> methodNames)
        {
            if (stubbedMethods.Contains(method))
                return;

            //logger.Trace($"Writing stub for method {method.Name}");

            if (method.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                return;
            if (method.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                return;

            if (method.DeclaringType != stubType)
                return;

            var parameters = method.GetParameters();
            int outParamCount = 0;
            int refParamCount = 0;
            foreach (var p in parameters)
            {
                if (p.IsOut)
                    outParamCount++;
                else if (p.ParameterType.IsByRef)
                    refParamCount++;
            }

            int parameterCount = parameters.Length - outParamCount;

            if (method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")))
            {
                string propName = method.Name.Substring("get_".Length);
                if (method.Name.StartsWith("get_"))
                    sb.AppendLine("    @property");
                else
                {
                    sb.AppendLine($"    @{propName}.setter");
                }

                sb.Append($"    def {propName}(");
            }
            else
            {
                if (methodNames[method.Name] > 1)
                    sb.AppendLine("    @overload");
                if (method.IsStatic)
                    sb.AppendLine("    @staticmethod");

                sb.Append($"    def {method.Name}(");
            }

            bool addComma = false;
            if (!method.IsStatic)
            {
                sb.Append("self");
                addComma = true;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].IsOut)
                    continue;

                if (addComma)
                    sb.Append(", ");

                sb.Append($"{PythonTypeConversions.SafePythonName(parameters[i].Name)}: {PythonTypeConversions.ToPythonType(parameters[i].ParameterType)}");
                addComma = true;
            }

            sb.Append(")");
            {
                List<string> types = new List<string>();
                if (method.ReturnType == typeof(void))
                {
                    if (outParamCount == 0 && refParamCount == 0)
                        types.Add("None");
                }
                else
                    types.Add(PythonTypeConversions.ToPythonType(method.ReturnType));

                foreach (var p in parameters)
                {
                    if (p.IsOut || (p.ParameterType.IsByRef))
                    {
                        types.Add(PythonTypeConversions.ToPythonType(p.ParameterType));
                    }
                }

                sb.Append($" -> ");
                if (outParamCount == 0 && refParamCount == 0)
                    sb.Append(types[0]);
                else
                {
                    sb.Append("Tuple[");
                    for (int i = 0; i < types.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");
                        sb.Append(types[i]);
                    }

                    sb.Append("]");
                }
            }
            sb.AppendLine(": ...\n");

            stubbedMethods.Add(method);
        }

        private void WritePropertyAccessor(StringBuilder sb, Type stubType, MethodInfo method)
        {
            if (method == null)
            {
                return;
            }

            if (stubbedMethods.Contains(method)) //avoid overlap between methods and getters/setters
                return;

            if (method.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                return;
            if (method.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                return;

            if (method.DeclaringType != stubType)
                return;

            var parameters = method.GetParameters();
            int outParamCount = 0;
            int refParamCount = 0;
            foreach (var p in parameters)
            {
                if (p.IsOut)
                    outParamCount++;
                else if (p.ParameterType.IsByRef)
                    refParamCount++;
            }

            int parameterCount = parameters.Length - outParamCount;

            if (method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")))
            {
                string propName = method.Name.Substring("get_".Length);
                if (method.Name.StartsWith("get_"))
                    sb.AppendLine("    @property");
                else
                {
                    sb.AppendLine($"    @{propName}.setter");
                }

                sb.Append($"    def {propName}(");
            }
            else
            {
                if (method.IsStatic)
                    sb.AppendLine("    @staticmethod");

                sb.Append($"    def {method.Name}(");
            }

            bool addComma = false;
            if (!method.IsStatic)
            {
                sb.Append("self");
                addComma = true;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].IsOut)
                    continue;

                if (addComma)
                    sb.Append(", ");

                sb.Append($"{PythonTypeConversions.SafePythonName(parameters[i].Name)}: {PythonTypeConversions.ToPythonType(parameters[i].ParameterType)}");
                addComma = true;
            }

            sb.Append(")");
            {
                List<string> types = new List<string>();
                if (method.ReturnType == typeof(void))
                {
                    if (outParamCount == 0 && refParamCount == 0)
                        types.Add("None");
                }
                else
                    types.Add(PythonTypeConversions.ToPythonType(method.ReturnType));

                foreach (var p in parameters)
                {
                    if (p.IsOut || (p.ParameterType.IsByRef))
                    {
                        types.Add(PythonTypeConversions.ToPythonType(p.ParameterType));
                    }
                }

                sb.Append($" -> ");
                if (outParamCount == 0 && refParamCount == 0)
                    sb.Append(types[0]);
                else
                {
                    sb.Append("Tuple[");
                    for (int i = 0; i < types.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");
                        sb.Append(types[i]);
                    }

                    sb.Append("]");
                }
            }
            sb.AppendLine(": ...\n");
            logger.Trace($"Created stub for method: {method.Name}");
            stubbedMethods.Add(method);
        }

        private HashSet<MethodInfo> stubbedMethods = new HashSet<MethodInfo>();

        private void WriteConstructors(StringBuilder sb, Type stubType)
        {

            // constructors
            ConstructorInfo[] constructors = stubType.GetConstructors();
            if (constructors.Length == 0)
                return;

            logger.Trace($"Writing constructors for class : {stubType.Name}");
            sb.AppendLine($"#Constructors");
            // sort for consistent output
            Array.Sort(constructors, MethodCompare);
            foreach (var constructor in constructors)
            {
                if (constructor.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;

                if (constructors.Length > 1)
                    sb.AppendLine("    @overload");
                sb.Append("    def __init__(self");
                var parameters = constructor.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (0 == i)
                        sb.Append(", ");
                    sb.Append($"{PythonTypeConversions.SafePythonName(parameters[i].Name)}: {PythonTypeConversions.ToPythonType(parameters[i].ParameterType)}");
                    if (i < (parameters.Length - 1))
                        sb.Append(", ");
                }

                sb.AppendLine("): ...\n");

            }
        }

        private void WriteFields(StringBuilder sb, Type stubType)
        {
            //  fields
            FieldInfo[] instanceFields = stubType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (instanceFields.Length == 0)
                return;
            // sort for consistent output
            sb.AppendLine("#Instance variables (non-static)");
            foreach (var field in instanceFields)
            {
                if (field.IsStatic)
                    continue;
                if (field.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                    continue;
                if (field.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;

                sb.AppendLine($"    {field.Name}: {PythonTypeConversions.ToPythonType(field.FieldType)}");
                logger.Trace($"Writing field : {field.Name}");
            }

            sb.AppendLine("");
        }

        private void WriteStaticFields(StringBuilder sb, Type stubType)
        {
            // Static fields
            FieldInfo[] staticFields = stubType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (staticFields.Length == 0)
                return;
            // sort for consistent output
            sb.AppendLine("#Class variables (static)");
            foreach (var field in staticFields)
            {
                if (!field.IsStatic)
                    continue;
                if (field.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                    continue;
                if (field.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;

                sb.AppendLine($"    {field.Name}: {PythonTypeConversions.ToPythonType(field.FieldType)}");
                logger.Trace($"Writing static field : {field.Name}");
            }

            sb.AppendLine("");
        }

        private void WriteProperties(StringBuilder sb, Type stubType)
        {
            //  fields
            PropertyInfo[] properties = stubType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            if (properties.Length == 0)
                return;
            // sort for consistent output
            sb.AppendLine("#Properties");
            foreach (var prop in properties)
            {

                if (prop.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                    continue;
                if (prop.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;

                logger.Trace($"Writing acessors for instance property: {prop.Name}");

                WritePropertyAccessor(sb, stubType, prop.GetMethod);
                WritePropertyAccessor(sb, stubType, prop.SetMethod);
            }

            sb.AppendLine("");
        }

        private void WriteStaticProperties(StringBuilder sb, Type stubType)
        {
            // Static fields
            PropertyInfo[] properties = stubType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            // sort for consistent output
            if (properties.Length == 0)
                return;
            sb.AppendLine("#Static Properties");
            foreach (var prop in properties)
            {
                if (prop.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                    continue;
                if (prop.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;
                logger.Trace($"Writing acessors for static property: {prop.Name}");
                WritePropertyAccessor(sb, stubType, prop.GetMethod);
                WritePropertyAccessor(sb, stubType, prop.SetMethod);
            }

            sb.AppendLine("");
        }
        private void WriteEventHandlers(StringBuilder sb, Type stubType)
        {
            //  fields
            EventInfo[] eventHandlers = stubType.GetEvents(BindingFlags.Instance | BindingFlags.Public);
            if (eventHandlers.Length == 0)
                return;
            // sort for consistent output
            sb.AppendLine("#EventHandlers");
            foreach (var evt in eventHandlers)
            {
                if (evt.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                    continue;
                if (evt.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;

                sb.AppendLine($"    {evt.Name}: Callable[[Any, {PythonTypeConversions.ToPythonType(evt.EventHandlerType.GenericTypeArguments?[0])}], None])    # void function(object sender, {evt.EventHandlerType.GenericTypeArguments?[0]} args) ");

                logger.Trace($"Writing eventhandler : {evt.Name}");
                //marked as stubbed
                stubbedMethods.Add(evt.AddMethod);
                stubbedMethods.Add(evt.RemoveMethod);
            }

            sb.AppendLine("");
        }

        private void WriteStaticEventHandlers(StringBuilder sb, Type stubType)
        {
            // Static fields
            EventInfo[] eventHandlers = stubType.GetEvents(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (eventHandlers.Length == 0)
                return;
            // sort for consistent output
            sb.AppendLine("#Static EventHandlers");
            foreach (var evt in eventHandlers)
            {
                if (evt.GetCustomAttribute(typeof(System.ObsoleteAttribute)) != null)
                    continue;
                if (evt.GetCustomAttribute(typeof(PyStubblerAnnotations.HideStub)) != null)
                    continue;
                sb.AppendLine($"    {evt.Name}: Callable[[Any, {PythonTypeConversions.ToPythonType(evt.EventHandlerType.GenericTypeArguments?[0])}], None])    # void function(object sender, {evt.EventHandlerType.GenericTypeArguments?[0]} args) ");

                logger.Trace($"Writing static eventhandler : {evt.Name}");
                //marked as stubbed
                stubbedMethods.Add(evt.AddMethod);
                stubbedMethods.Add(evt.RemoveMethod);

            }

            sb.AppendLine("");
        }





        static int MethodCompare(MethodBase a, MethodBase b)
        {
            string aSignature = a.Name;
            foreach (var parameter in a.GetParameters())
                aSignature += $"_{parameter.GetType().Name}";
            string bSignature = b.Name;
            foreach (var parameter in b.GetParameters())
                bSignature += $"_{parameter.GetType().Name}";
            return aSignature.CompareTo(bSignature);
        }

        static void WriteStubHeader(StringBuilder sb, string namespaceName, string assemblyName)
        {
            sb.AppendLine("```");
            sb.AppendLine($"Stubs generated by PyStubbler version [{typeof(StubBuilder).Assembly.GetName().Version}]");
            sb.AppendLine($"Date: {DateTime.Now.ToString("f", DateTimeFormatInfo.InvariantInfo)}");
            sb.AppendLine($"\nNamespace: {namespaceName}");
            sb.AppendLine($"\nIn Assembly: {assemblyName}");
            sb.AppendLine("\n```\n");
        }
    }
}
