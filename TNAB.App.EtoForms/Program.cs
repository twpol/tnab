using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using Eto.Forms;

namespace TNAB.App.EtoForms
{
    class Program
    {
        const string WindowsApplicationName = "windows";
        const string RuntimeName = "Microsoft.NETCore.App";
        const string WindowsRuntimeName = "Microsoft.WindowsDesktop.App";

        [STAThread]
        public static void Main(string[] args)
        {
            new Application(GetPlatform()).Run(new MainForm());
        }

        static string GetPlatform()
        {
            if (OperatingSystem.IsWindows() && ConfigureWindowsRuntime()) return Eto.Platforms.WinForms;
            return Eto.Platforms.Gtk;
        }

        [SupportedOSPlatform("Windows")]
        static bool ConfigureWindowsRuntime()
        {
            var windowsApplicationPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), WindowsApplicationName);
            if (!Directory.Exists(windowsApplicationPath)) return Error($"WARNING: Missing <{WindowsApplicationName}> application path <{windowsApplicationPath}>");
            var runtimePath = RuntimeEnvironment.GetRuntimeDirectory();
            if (!runtimePath.Contains(RuntimeName)) return Error($"WARNING: Missing <{RuntimeName}> from runtime path <{runtimePath}>");
            var windowsRuntimePath = runtimePath.Replace(RuntimeName, WindowsRuntimeName);
            if (!Directory.Exists(windowsRuntimePath)) return Error($"WARNING: Missing <{WindowsRuntimeName}> runtime path <{windowsRuntimePath}>");
            var paths = new[] { windowsApplicationPath, windowsRuntimePath };
            AssemblyLoadContext.Default.Resolving += (sender, e) =>
            {
                foreach (var path in paths) if (TryLoadAssembly(sender, Path.Combine(path, e.Name + ".dll"), out var assembly)) return assembly;
                return null;
            };
            return true;
        }

        static bool TryLoadAssembly(AssemblyLoadContext context, string file, out Assembly assembly) => (assembly = File.Exists(file) ? context.LoadFromAssemblyPath(file) : null) != null;

        static bool Error(string message)
        {
            Console.Error.WriteLine(message);
            return false;
        }
    }
}
