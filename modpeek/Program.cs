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
using Mono.Cecil;

namespace VintageStory.ModPeek
{

    class Program
    {
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

        private static ModInfo GetDllInfo(FileInfo f)
        {
            var assembly = AssemblyDefinition.ReadAssembly(f.FullName);
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

        private static Mono.Cecil.CustomAttributeNamedArgument GetProperty(CustomAttribute customAttribute, string name)
        {
            return customAttribute.Properties.SingleOrDefault(property => property.Name == name);
        }

        private static T GetPropertyValue<T>(CustomAttribute customAttribute, string name)
        {
            return (T) GetProperty(customAttribute, name).Argument.Value;
		}

		private static T[] GetPropertyValueArray<T>(CustomAttribute customAttribute, string name)
		{
            return (GetProperty(customAttribute, name).Argument.Value as CustomAttributeArgument[])?.Select(item => (T) item.Value).ToArray();
		}

		private static ModInfo loadModInfoFromAssembly(AssemblyDefinition assemblyDefinition)
        {
            var modInfoAttr = assemblyDefinition.CustomAttributes.SingleOrDefault(attribute => attribute.AttributeType.Name == "ModInfoAttribute");
			if (modInfoAttr == null)
            {
                return null;
            }

			string name = modInfoAttr.ConstructorArguments[0].Value as string;
			string modID = modInfoAttr.ConstructorArguments[1].Value as string;

			EnumAppSide side;
            if (!Enum.TryParse(GetPropertyValue<string>(modInfoAttr, "Side"), true, out side))
            {
                side = EnumAppSide.Universal;
            }

			var dependencies = assemblyDefinition.CustomAttributes
				.Where(attribute => attribute.AttributeType.Name == "ModDependencyAttribute")
				.Select(attribute => new ModDependency((string) attribute.ConstructorArguments[0].Value, attribute.ConstructorArguments[1].Value as string))
				.ToList();


			ModInfo info = new ModInfo(
                EnumModType.Code, name, modID,
                GetPropertyValue<string>(modInfoAttr, "Version"),
				GetPropertyValue<string>(modInfoAttr, "Description"),
                GetPropertyValueArray<string>(modInfoAttr, "Authors"),
				GetPropertyValueArray<string>(modInfoAttr, "Contributors"),
				GetPropertyValue<string>(modInfoAttr, "Website"),
				side,
				GetPropertyValue<bool?>(modInfoAttr, "RequiredOnClient").GetValueOrDefault(),
				GetPropertyValue<bool?>(modInfoAttr, "RequiredOnServer").GetValueOrDefault(),
                dependencies);

            info.NetworkVersion = GetPropertyValue<string>(modInfoAttr, "NetworkVersion");

            return info;
        }
    }
}
