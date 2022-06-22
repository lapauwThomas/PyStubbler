using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

using DocoptNet;
using PyStubblerLib;

namespace PyStubbler
{
    class Program
    {
        private const string UsagePatterns = @"
Usage:
    PyStubbler (-h | --help)
    PyStubbler (-V | --version)
    PyStubbler [--dest=<dest_path>] [--search=<search_path>...] [--prefix=<prefix>] [--postfix=<postfix>] [--dest-is-root] [--copylog] [--loglevel=<lv>] <target_dll>...

Options:
    -h --help                   Show this help
    -V --version                Show version
    --loglevel=<lv>             Log level (TRACE, DEBUG, INFO, WARN, ERROR, FATAL, OFF) [default: OFF]                  
    --dest=<dest_path>          Output directory
    --search=<search_path>      Path to search for referenced assemblies. Can be repeated multiple times for multiple paths
    --prefix=<prefix>           Root namespace directory prefix
    --postfix=<postfix>         Root namespace directory postfix [default: -stubs]
    --dest-is-root              Use destination path for root namespace
    --copylog                   Copy the logfile to the output directory
";

        static void Main(string[] args)
        {
            //parse input arguments
            var arguments = new Docopt().Apply(UsagePatterns, args, version: Assembly.GetExecutingAssembly().GetName().Version, exit: false);


            //write header
            Console.WriteLine($"\nPyStubbler version [{typeof(StubBuilder).Assembly.GetName().Version}]");
            var loglevel = arguments["--loglevel"] != null ? (string)arguments["--loglevel"].Value : "INFO";
            Console.WriteLine($"Logging is set to: {loglevel}\n");




            if (arguments.ContainsKey("<target_dll>"))
            {
                foreach (ValueObject targetDll in (ArrayList)arguments["<target_dll>"].Value)
                {
                    string assmPath = (string)targetDll.Value;
                    if (File.Exists(assmPath))
                    {
                        // grab dest path if provided
                        string destPath = null;
                        if (arguments["--dest"] != null && arguments["--dest"].IsString)
                            destPath = (string)arguments["--dest"].Value;

                        if(!string.IsNullOrEmpty(destPath)) Console.WriteLine($"Target path is {destPath}");

                        // grab search paths if provided
                        string[] searchPaths = null;
                        if (arguments["--search"] != null && arguments["--search"].IsList)
                        {
                            List<string> lookupPaths = new List<string>();
                            foreach (ValueObject searchPath in arguments["--search"].AsList.ToArray())
                            {
                                Console.WriteLine($"Search path {searchPath}");
                                lookupPaths.Add((string)searchPath.Value);
                            }

                            searchPaths = lookupPaths.ToArray();
                        }

                        // prepare generator configs
                        // grab pre and postfixes for root namespace dir names
                        var genCfg = new BuildConfig
                        {
                            Prefix = arguments["--prefix"] != null ? (string)arguments["--prefix"].Value : string.Empty,
                            Postfix = arguments["--postfix"] != null ? (string)arguments["--postfix"].Value : string.Empty,
                            DestPathIsRoot = arguments["--dest-is-root"] != null ? (bool)arguments["--dest-is-root"].Value : false,
                            LogLevel = loglevel
                        };

                        Console.WriteLine($"Building stubs for {assmPath}");
                        try
                        {
                            var builder = new StubBuilder();
                            var dest = builder.BuildAssemblyStubs(
                                assmPath,
                                destPath: destPath,
                                searchPaths: searchPaths,
                                cfgs: genCfg
                            );


                            Console.WriteLine($"{builder.errorsList.Count} errors occurred while generating stubs.");
                            foreach (string err in builder.errorsList)
                            {
                                Console.WriteLine(err);
                            }


                            Console.WriteLine($"Stubs saved to {dest}");
                            //Console.WriteLine($"Log saved to {StubBuilder.logfile}");
                            var copyLog = arguments["--copylog"] != null ? (bool)arguments["--copylog"].Value : false;


                            if (copyLog)
                            {
                                var destFile = Path.Combine(dest, StubBuilder.logfile);
                                Console.WriteLine($"Copying log to {destFile}");
                                try
                                {
                                    if (File.Exists(StubBuilder.logfile))
                                        File.Copy(StubBuilder.logfile, destFile, true);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Could not copy log to {destFile} \n{ex.Message}");
                                }
                            }

                        }
                        catch (Exception sgEx)
                        {
                            Console.WriteLine($"Error: failed generating stubs | {sgEx.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error: can not find {assmPath}");
                    }
                }
            }

            string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Console.WriteLine($"Log saved to {Path.Combine(path, StubBuilder.logfile)}");

#if DEBUG //only add in debug builds
            //prevent immediate exit if debugger is active to check output
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("[Debug] End of program - Any key to exit");
                Console.ReadKey();
            }
#endif
        }
    }
}
