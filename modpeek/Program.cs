using CommandLine;
using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek
{

    class Program
    {
        static string dataPathMods;
        static string dataPathbinariesMods;
        static string dataPathbinaries;

        static void Main(string[] rawArgs)
        {
            if (rawArgs.Length == 0)
            {
                Console.Error.WriteLine("Missing args");
                Environment.Exit(1);
                return;
            }

            var args = new Args();
            Parser parser = new Parser();
            parser.ParseArguments(rawArgs, args);

            if (args.File == null)
            {
                Console.Error.WriteLine("Missing file");
                Environment.Exit(1);
                return;
            }

            ModInfo minfo;

            FileInfo f = new FileInfo(args.File);
            if (!f.Exists)
            {
                Console.Error.WriteLine("No such file '" + args.File + "'");
                Environment.Exit(1);
                return;
            }

            loadLibPaths();



            switch (f.Extension)
            {
                case ".zip":
                    minfo = GetZipInfo(f);
                    break;
                case ".cs":
                    minfo = GetCsInfo(f);
                    break;
                case ".dll":
                    minfo = GetDllInfo(f);
                    break;
                default:
                    Console.Error.WriteLine("Invalid extension, must be zip, cs or dll");
                    return;
            }

            if (minfo == null)
            {
                Environment.Exit(1);
                return;
            }


            if (args.IdAndVersion)
            {
                Console.WriteLine(minfo.ModID + ":" + minfo.Version);
            }
            else
            {

                Console.WriteLine("Id: " + minfo.ModID);
                Console.WriteLine("Name: " + minfo.Name);
                Console.WriteLine("Version: " + minfo.Version);
                Console.WriteLine("Authors: " + string.Join(", ", minfo.Authors));
                Console.WriteLine("Website: " + minfo.Website);
                Console.WriteLine("Description: " + minfo.Description);
            }

            Environment.Exit(0);
        }


        public static string libFolderName = "Lib" + (IntPtr.Size == 4 ? "32" : "64");
        static string EnvSearchPathName;
        
        private static void loadLibPaths()
        {
            bool isWindows = false;
            if (Path.DirectorySeparatorChar == '\\')
            {
                EnvSearchPathName = "PATH";
                isWindows = true;
            }
            else
            if (IsMac())
            {
                EnvSearchPathName = "DYLD_FRAMEWORK_PATH";
            }
            else
            {
                EnvSearchPathName = "LD_LIBRARY_PATH";
            }

            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dataPathMods = Path.Combine(appdata, "VintagestoryData", "Mods");
            dataPathbinariesMods = Path.Combine(appdata, "Vintagestory", "Mods");
            dataPathbinaries = Path.Combine(appdata, "Vintagestory");

            // 1. Set dll ENV Path
            string libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, libFolderName);

            var name = EnvSearchPathName;
            var value = libPath + ";" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lib") + ";" + Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);

            // 2. Resolve dll path manually
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolve);

            // 4. Preload Cairo and OpenAL to force windows to load them from the lib folder
            if (isWindows)
            {
                LoadLibrary(Path.Combine(libPath, "openal32"));
                LoadLibrary(Path.Combine(libPath, "libcairo-2"));
            }

        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);
        [DllImport("libc")]
        static extern int uname(IntPtr buf);




        static bool IsMac()
        {
            IntPtr buf = IntPtr.Zero;
            try
            {
                buf = Marshal.AllocHGlobal(8192);
                if (uname(buf) == 0)
                {
                    string os = Marshal.PtrToStringAnsi(buf);
                    if (os == "Darwin") return true;
                }
            }
            catch
            {
            }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
            }
            return false;
        }

        private static ModInfo GetDllInfo(FileInfo f)
        {
            var assembly = Assembly.LoadFile(f.FullName);
            return loadModInfoFromAssembly(assembly);
        }

        private static ModInfo GetCsInfo(FileInfo f)
        {
            string text = File.ReadAllText(f.FullName);

            /*[assembly: ModInfo("StepUp", Version = "1.2.0", Side = "Client",
	Description = "Doubles players' step height to allow stepping up full blocks",
	Website = "https://www.vintagestory.at/forums/topic/3349-stepup-v120/",
	Authors = new []{ "copygirl" })]*/

            Regex rex = new Regex(@"\[assembly:\s*ModInfo\(\s*""([a-zA-z0-9]+)""");
            Match m = rex.Match(text);

            if (m.Success)
            {
                Regex vrex = new Regex(@"\[assembly:\s*ModInfo\(.*Version\s*=\s*""([[0-9a-z\.\-]+)""");
                Match vm = vrex.Match(text);

                return new ModInfo()
                {
                    ModID = m.Groups[1].Value,
                    Version = vm.Groups[1].Value,
                    Name = "all other modinfo attribtues are not read from cs files yet. Sorry"
                    
                };
            }

            return null;
        }


        private static ModInfo GetZipInfo(FileInfo f)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(f.FullName))
                {
                    var entry = zip.GetEntry("modinfo.json");
                    if (entry != null)
                    {
                        using (var reader = new StreamReader(entry.Open()))
                        {
                            var content = reader.ReadToEnd();
                            return JsonConvert.DeserializeObject<ModInfo>(content);
                        }
                    }
                }
            } catch (Exception e)
            {
                Console.Error.WriteLine("Failed to read zip file");
                Console.Error.WriteLine(e.ToString());
            }


            Console.Error.WriteLine("modinfo.json missing");
            return null;
        }




        private static ModInfo loadModInfoFromAssembly(Assembly assembly)
        {
            var modInfoAttr = assembly.GetCustomAttribute<ModInfoAttribute>();
            if (modInfoAttr == null)
            {
                return null;
            }

            EnumAppSide side;
            if (!Enum.TryParse(modInfoAttr.Side, true, out side))
            {
                side = EnumAppSide.Universal;
            }

            var dependencies = assembly
                .GetCustomAttributes<ModDependencyAttribute>()
                .Select(attr => new ModDependency(attr.ModID, attr.Version))
                .ToList();


            ModInfo info = new ModInfo(
                EnumModType.Code, modInfoAttr.Name, modInfoAttr.ModID, modInfoAttr.Version,
                modInfoAttr.Description, modInfoAttr.Authors, modInfoAttr.Contributors, modInfoAttr.Website,
                side, modInfoAttr.RequiredOnClient, modInfoAttr.RequiredOnServer, dependencies);

            info.NetworkVersion = modInfoAttr.NetworkVersion;

            return info;
        }








        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string dllName = new AssemblyName(args.Name).Name + ".dll";

                string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                string folderPath = Path.Combine(basePath, "Lib");


                string assemblyPath = Path.Combine(folderPath, dllName);
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                assemblyPath = Path.Combine(basePath, dllName);

                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                assemblyPath = Path.Combine(dataPathMods, dllName);

                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                assemblyPath = Path.Combine(dataPathbinaries, dllName);

                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                assemblyPath = Path.Combine(dataPathMods, dllName);

                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                return null;

            }
            catch (Exception e)
            {
                throw new Exception("Failed loading assembly " + args.Name, e);
            }
        }

    }
}
