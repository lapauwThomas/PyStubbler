using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Config;

namespace PyStubblerLib
{
    internal class PSLogManager
    {

        public static string Logfilename = "PyStubBuilderLog.log";
        // A Logger dispenser for the current assembly (Remember to call Flush on application exit)
        public static LogFactory Instance { get { return _instance.Value; } }
        private static Lazy<LogFactory> _instance = new Lazy<LogFactory>(BuildLogFactory);

        // 
        // Use a config file located next to our current assembly dll 
        // eg, if the running assembly is c:\path\to\MyComponent.dll 
        // the config filepath will be c:\path\to\MyComponent.nlog 
        // 
        // WARNING: This will not be appropriate for assemblies in the GAC 
        // 
        private static LogFactory BuildLogFactory()
        {
            //// Use name of current assembly to construct NLog config filename 
            //Assembly thisAssembly = Assembly.GetExecutingAssembly();
            ////string configFilePath = Path.ChangeExtension(thisAssembly.Location, "StubblerLogConfig.nlog");
            //string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            //string folder = Path.GetDirectoryName(exePath);

            //string configFilePath = Path.Combine(folder, "StubblerLogConfig.nlog");
            LogFactory logFactory = new LogFactory();
            //logFactory.Configuration = new XmlLoggingConfiguration(configFilePath, true, logFactory);


            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile")
            {
                FileName = Logfilename,
                Layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss}|${level:uppercase=true}|${message} ${exception:innerFormat=Message,StackTrace}",
                DeleteOldFileOnStartup = true

            };


            var logconsole = new NLog.Targets.ConsoleTarget("logconsole") { Layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss}|${level:uppercase=true}|${message} ${exception:innerFormat=Message,StackTrace}" };

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Off, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Apply config           
            logFactory.Configuration = config;

            return logFactory;
        }

        public static void SetLogLevel(string level)
        {
            var config = Instance.Configuration;

            LogLevel newLevel;
            switch (level)
            {
                case "OFF":
                    newLevel = LogLevel.Off;
                    break;
                case "TRACE":
                    newLevel = LogLevel.Trace;
                    break;
                case "DEBUG":
                    newLevel = LogLevel.Debug;
                    break;
                case "INFO":
                    newLevel = LogLevel.Info;
                    break;
                case "WARN":
                    newLevel = LogLevel.Warn;
                    break;
                case "ERROR":
                    newLevel = LogLevel.Error;
                    break;
                case "FATAL":
                    newLevel = LogLevel.Fatal;
                    break;
                default:
                    return;
            }
            Instance.GetCurrentClassLogger().Log(LogLevel.Info, $"Setting logger level to {newLevel.Name}");
            foreach (var rule in Instance.Configuration.LoggingRules)
            {
                for (int i = newLevel.Ordinal; i <= LogLevel.Fatal.Ordinal; i++)
                {
                    rule.EnableLoggingForLevel(LogLevel.FromOrdinal(i));
                }
                for (int i = 0; i < newLevel.Ordinal; i++)
                {
                    rule.DisableLoggingForLevel(LogLevel.FromOrdinal(i));
                }
            }
            Instance.ReconfigExistingLoggers();
        }
    }

    internal static class NlogTools
    {

        internal static string LogIEnumerable(this IEnumerable num, string messagePrefix, string elementSeparator = "\n")
        {
            var sb = new StringBuilder();
            sb.Append(messagePrefix);
            foreach (object o in num)
            {
                sb.Append(o.ToString() + elementSeparator);
            }

            return sb.ToString();
        }


    }
}
