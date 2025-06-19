using Vintagestory.API.Common;
using System.Text;

namespace VintageStory.ModPeek;

using static ModPeek;

public static class ModPeekTool
{
    static void Main(string[] rawArgs)
    {
        void PrintUsage()
        {
            Console.Error.Write(
$@"modpeek.exe - extract information from VintageStory mod files:

Synopsis:
	modpeek.exe [-i] [-p] [-f] input

Options:
	-f file, --file file:
		The mod file to operate on. 
		If this option is not specified the last standalone argument will be used instead.

	-i, --idandversion
		Only print   modid:version   and exit.

	-p, --always-print
		Print partial info even if errors occurred (if possible).
		If this is not set any errors will suppress normal output.

Operands:
	input
		The mod file to extract information from.
"
            );
        }

        bool idAndVersion = false;
        bool alwaysPrint = false;
        string? inputFile = null;
        for (int i = 0; i < rawArgs.Length; i++) {
            switch(rawArgs[i]) {
                case "-h": case "--help":
                    PrintUsage();
                    return;

                case "-i": case "--idandversion":
                    idAndVersion = true;
                    break;

                case "-p": case "--always-print":
                    alwaysPrint = true;
                    break;

                case "-f": case "file":
                    if (i + 1 >= rawArgs.Length) {
                        Console.Error.WriteLine($"Missing argument for {rawArgs[i]}.");
                        Environment.Exit(1); return;
                    }

                    i += 1;
                    goto default;

                default:
                    if (inputFile == null) {
                        inputFile = rawArgs[i];
                        break;
                    }
                    else {
                        if (rawArgs[i].StartsWith("-")) {
                            Console.Error.WriteLine($"Unknown option '{rawArgs[i]}'.\n");
                            PrintUsage();
                        }
                        else {
                            Console.Error.WriteLine($"Cannot process multiple files.");
                        }
                        Environment.Exit(1); return;
                    }
            }
        }

        if (inputFile == null) {
            Console.Error.WriteLine($"Missing input file.\n");
            PrintUsage();
            Environment.Exit(1); return;
        }

        var f = new FileInfo(inputFile);
        if (!f.Exists) {
            Console.Error.WriteLine($"No such file '{inputFile}'.");
            Environment.Exit(1); return;
        }

        var error = !TryExtractModInfo(f, out var modInfo, PrintErrorToStdError);
        if (modInfo == null) {
            Environment.Exit(1); return;
        }

        error |= !ValidateModInfo(modInfo, PrintErrorToStdError);
        if (error && !alwaysPrint) {
            Environment.Exit(1); return;
        }


        if (idAndVersion) {
            Console.WriteLine(modInfo.ModID + ":" + modInfo.Version);
        }
        else {
            static string EscapedAndJoinCommaSeparatedList<T>(IReadOnlyList<T> list)
            {
                var b = new StringBuilder(list.Count * 16);
                for(int i = 0; i < list.Count; i++) {
                    if(i > 0) b.Append(", ");
                    int start = b.Length;
                    b.Append(list[i]);
                    b.Replace(", ", @",\ ", start, b.Length - start);
                }
                return b.ToString();
            }

            Console.WriteLine("Id: " + modInfo.ModID);
            Console.WriteLine("Name: " + modInfo.Name);
            Console.WriteLine("Version: " + modInfo.Version);
            Console.WriteLine("Type: " + modInfo.Type);
            Console.WriteLine("Side: " + modInfo.Side);
            Console.WriteLine("RequiredOnClient: " + modInfo.RequiredOnClient);
            Console.WriteLine("RequiredOnServer: " + modInfo.RequiredOnServer);
            Console.WriteLine("NetworkVersion: " + modInfo.NetworkVersion);
            Console.WriteLine("IconPath: " + modInfo.IconPath);
            Console.WriteLine("Description: " + modInfo.Description.Replace("\r", "").Replace("\n", @"\n"));
            Console.WriteLine("Authors: " + EscapedAndJoinCommaSeparatedList(modInfo.Authors));
            Console.WriteLine("Contributors: " + EscapedAndJoinCommaSeparatedList(modInfo.Contributors));
            Console.WriteLine("Website: " + modInfo.Website);
            Console.WriteLine("Dependencies: " + string.Join(", ", modInfo.Dependencies));
        }

        Environment.Exit(error ? 1 : 0);
    }

    static void PrintErrorToStdError(Errors.Error error)
    {
        Console.Error.WriteLine(FormatError(error));
    }

    public static string FormatError(Errors.Error error)
    {
        return error switch {
            Errors.FileToSmall => "Provided mod file is to small to possibly be a mod (< 4 bytes).",
            Errors.CouldNotDetermineFileType => @"Failed to determine file type from content, must be a
	zip	(containing 'modinfo.json'),
	cs	(containing a '[assembly: ModInfo(...)]' attribute) or
	dll	(containing a '[assembly: ModInfo(...)]' attribute).
",
            Errors.MalformedArchive           err => $"The zip archive failed to decode: {err.Exception.Message}.",
            Errors.MissingFileInArchiveRoot   err => $"The zip archive is missing a file named {err.FileName} in its root."+
                (err.LikelyCompressedDirectory ? " All files in the archive share a common parent directory, so you likely compressed a directory instead of individual files." : ""),
            Errors.MalformedJson              err => $"The provided modinfo.json was malformed: {err.Exception.Message}.",
            Errors.UnexpectedJsonRootType     err => $"The root element of the modinfo.json must be a {err.ExpectedType}, but was {err.Given.ValueKind}.",
            Errors.MissingAssemblyAttribute   err => $"Missing expected assembly-attribute '{err.AttributeName}'.",
            Errors.PrimitiveParsingFailure    err => $"The {err.TargetProperty} property failed to parse as a {err.ExpectedType} (was '{err.MalformedInput}').",
            Errors.StringParsingFailure       err => $"The {err.TargetProperty} property failed to convert from a string to {err.ExpectedType} (was '{err.MalformedInput}').",
            Errors.MissingRequiredProperty    err => $"{err.TargetStructure} is missing the required property '{err.PropertyName}'.",
            Errors.UnexpectedProperty         err => $"Unexpected property '{err.PropertyName}' with value '{err.PropertyValue}'.",
            Errors.UnexpectedValue            err => $"Property '{err.TargetProperty}' was expected to be {err.Expected}, but was '{err.Given}'.",
            Errors.UnexpectedJsonPropertyType err => $"Property '{err.TargetProperty}' was expected to be of type {err.ExpectedType}, but was '{err.Given.GetRawText()}'.",
            Errors.MalformedPrimaryModID      err => $"The ModID of this mod ('{err.MalformedInput}') is malformed.",
            Errors.NotACoreMod                    => $"The provided mod is not a core mod, but the CoreMod property was set to true.",
            Errors.MalformedPrimaryVersion    err => $"The Version of this mod ('{err.MalformedInput}') is malformed.",
            Errors.MalformedNetworkVersion    err => $"The NetworkVersion of this mod ('{err.MalformedInput}') is malformed.",
            Errors.ModIDGenerationFailure     err => $"Mod name '{err.MalformedInput}' failed to be converted to a ModID: {err.Exception}.",
            Errors.MissingDependencyModID         => $"A dependency was specified that does not have target ModID set.",
            Errors.MalformedDependencyModID   err => $"Dependency '{err.MalformedInput}' specifies a malformed ModID.",
            Errors.MalformedDependencyVersion err => $"Dependency '{err.Dependency}' specifies a malformed target Version ('{err.MalformedInput}').",
            Errors.MalformedAuthorName        err => $"'{err.MalformedInput}' is not a valid author name.",
            Errors.MalformedContributorName   err => $"'{err.MalformedInput}' is not a valid contributor name.",
            _ => $"Unknown error of severity {error.Severity}: {error}."
        };
    }
}
