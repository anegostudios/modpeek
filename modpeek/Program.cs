using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Runtime.CompilerServices;

namespace VintageStory.ModPeek;

public static partial class ModPeek
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

        var error = !TryGetModInfo(f, out var modInfo, PrintErrorToStdError);
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

    public static bool TryGetModInfo(FileInfo f, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
    {
        var bytes = File.ReadAllBytes(f.FullName);
        if (bytes.Length < 4) {
            Console.Error.WriteLine("File size below sensible (< 4 bytes).");
            modInfo = null;
            return false;
        }

        switch (f.Extension) {
            case ".zip": return TryGetZipInfo(bytes, out modInfo, errorCallback);
            case ".cs" : return TryGetCsInfo (bytes, out modInfo, errorCallback);
            case ".dll": return TryGetDllInfo(bytes, out modInfo, errorCallback);
        }

        var magic = BitConverter.ToUInt32(bytes, 0);
        //NOTE(Rennorb): The only byteswap intrinsic on this version of dotnet is System.Net.HostToNetwork and I don't want to import that.
        if (BitConverter.IsLittleEndian) magic = ((magic >> 24) & 0x000000FF) | ((magic >> 8) & 0x0000FF00) | ((magic << 8) & 0x00FF0000) | ((magic << 24) & 0xFF000000);
                
        if (magic == 0x504B0304) return TryGetZipInfo(bytes, out modInfo, errorCallback);
        //NOTE(Rennorb): Technically speaking this is the MS DOS header and is optional, but realistically every dll is going to have it.
        if ((magic & 0xffff0000) == 0x4D5A0000) return TryGetDllInfo(bytes, out modInfo, errorCallback);

        if (TryGetCsInfo(bytes, out modInfo, errorCallback)) return true;

        Console.Error.WriteLine(
@"Failed to determine file type from content, must be a
	zip	(containing 'modinfo.json'),
	cs	(containing a '[assembly: ModInfo(...)]' attribute) or
	dll	(containing a '[assembly: ModInfo(...)]' attribute).
"
        );
        return false;
    }

    /// <summary>Validates the data inside a ModInfo and sets invalid fields to blank / default values.</summary>
    /// <returns>False if any data is invalid.</returns>
    public static bool ValidateModInfo(ModInfo modInfo, Action<Errors.Error> errorCallback)
    {
        var error = false;
            
        if (string.IsNullOrWhiteSpace(modInfo.Name)) {
            errorCallback(new Errors.MissingRequiredProperty(nameof(ModInfo), nameof(modInfo.Name)));
            error = true;
            modInfo.Name = null;
        }

        if (string.IsNullOrWhiteSpace(modInfo.ModID)) {
            if (!string.IsNullOrWhiteSpace(modInfo.Name)) {
                try {
                    modInfo.ModID = ModInfo.ToModID(modInfo.Name);
                }
                catch (ArgumentException e) {
                    errorCallback(new Errors.ModIDGenerationFailure(e, modInfo.Name!));
                    modInfo.ModID = null;
                    error = true;
                }
            }
            else {
                errorCallback(new Errors.MissingRequiredProperty(nameof(ModInfo), nameof(modInfo.ModID)));
                modInfo.ModID = null;
                error = true;
            }
        }
        else if (!ModInfo.IsValidModID(modInfo.ModID)) {
            errorCallback(new Errors.MalformedPrimaryModID(modInfo.ModID));
            error = true;
            modInfo.ModID = null;
        }

        if (string.IsNullOrWhiteSpace(modInfo.Version)) {
            errorCallback(new Errors.MissingRequiredProperty(nameof(ModInfo), nameof(modInfo.Version)));
            error = true;
            modInfo.Version = null;
        }
        else if(modInfo.Version == BROKEN_VERSION) {
            modInfo.Version = null;
        }
        else {
            if (!IsValidVersion(modInfo.Version)) {
                errorCallback(new Errors.MalformedPrimaryVersion(modInfo.Version));
                error = true;
                modInfo.Version = null;
            }
        }

        if (string.IsNullOrWhiteSpace(modInfo.NetworkVersion)) {
            modInfo.NetworkVersion = modInfo.Version; // The "spec" (https://wiki.vintagestory.at/Special:MyLanguage/Modinfo) sais this defaults to the specified version.
        }
        else if(modInfo.NetworkVersion == BROKEN_VERSION) {
            modInfo.NetworkVersion = null;
        }
        else {
            if (!IsValidVersion(modInfo.NetworkVersion)) {
                errorCallback(new Errors.MalformedNetworkVersion(modInfo.NetworkVersion));
                error = true;
                modInfo.NetworkVersion = null;
            }
        }

        if (!Enum.IsDefined(typeof(EnumModType), modInfo.Type)) {
            errorCallback(new Errors.UnexpectedValue(nameof(modInfo.Type), nameof(EnumModType), modInfo.Type.ToString()));
            error = true;
            // Code mods are probably the ones with the highest security restrictions, so we pick that as a fallback.
            // We don't have a neutral default.
            modInfo.Type = EnumModType.Code;
        }

        if (!Enum.IsDefined(typeof(EnumAppSide), modInfo.Side)) {
            errorCallback(new Errors.UnexpectedValue(nameof(modInfo.Side), nameof(EnumAppSide), modInfo.Side.ToString()));
            error = true;
            modInfo.Side = 0;
        }

        if (string.IsNullOrWhiteSpace(modInfo.IconPath)) {
            modInfo.IconPath = null; // unify
        }
        else {
            var err = false;
            try {
                var tempPath = Path.GetTempPath();
                var testPath = Path.GetFullPath(Path.Combine(tempPath, modInfo.IconPath));
                err = !testPath.StartsWith(tempPath, StringComparison.Ordinal);
            }
            catch { err = true; }

            if (err) {
                errorCallback(new Errors.StringParsingFailure(nameof(modInfo.IconPath), "a relative path within the mod", modInfo.IconPath));
                modInfo.IconPath = null;
                error = true;
            }
        }

        if (string.IsNullOrWhiteSpace(modInfo.Website)) {
            modInfo.Website = ""; // unify the value
        }
        else {
            try {
                _ = new Uri(modInfo.Website);
            }
            catch {
                errorCallback(new Errors.StringParsingFailure(nameof(modInfo.Website), "URL", modInfo.Website));
                modInfo.Website = null;
                error = true;
            }
        }

        var authors = (modInfo.Authors as List<string>) ?? modInfo.Authors.ToList();
        for (int i = authors.Count - 1; i >= 0; i--) {
            var author = authors[i];
            foreach(var c in author) {
                if(c == '\n' || c == '\r') {
                    errorCallback(new Errors.MalformedAuthorName(author));
                    error = true;
                    authors.RemoveAt(i);
                    break;
                }
            }
        }
        if (authors.Count != modInfo.Authors.Count) modInfo.Authors = authors;

        var contributors = (modInfo.Contributors as List<string>) ?? modInfo.Contributors.ToList();
        for (int i = contributors.Count - 1; i >= 0; i--) {
            var contributor = contributors[i];
            foreach(var c in contributor) {
                if(c == '\n' || c == '\r') {
                    errorCallback(new Errors.MalformedContributorName(contributor));
                    error = true;
                    contributors.RemoveAt(i);
                    break;
                }
            }
        }
        if (contributors.Count != modInfo.Contributors.Count) modInfo.Contributors = contributors;

        var dependencies = (modInfo.Dependencies as List<ModDependency>) ?? modInfo.Dependencies.ToList();
        for (int i = dependencies.Count - 1; i >= 0; i--) {
            var dependency = dependencies[i];
            if (string.IsNullOrWhiteSpace(dependency.ModID)) {
                errorCallback(new Errors.MissingDependencyModID());
                error = true;
                dependencies.RemoveAt(i);
                continue;
            }
            if (!ModInfo.IsValidModID(dependency.ModID)) {
                errorCallback(new Errors.MalformedDependencyModID(dependency.ModID));
                dependencies.RemoveAt(i);
                error = true;
                continue;
            }

            if (string.IsNullOrEmpty(dependency.Version) || dependency.Version == "*") {
                if (s_modIDProp == null) FindDependencyBackingFields();
                s_versionProp!.SetValue(dependency, null); // unify the value
            }
            else if (!IsValidVersion(dependency.Version)) {
                errorCallback(new Errors.MalformedDependencyVersion(dependency.ModID, dependency.Version));
                error = true;
                dependencies.RemoveAt(i);
                continue;
            }
        }
        if (modInfo.Dependencies.Count != dependencies.Count) modInfo.Dependencies = dependencies;

        return !error;
    }

    const string BROKEN_VERSION = "broken";
    const string VERSION_REGEX = @"^\d{1,5}\.\d{1,4}\.\d{1,4}(?:-(?:rc|pre|dev)\.\d{1,4})?$";
    static readonly Regex s_versionRegex = new(VERSION_REGEX);
    static bool IsValidVersion(string versionString)
    {
        return s_versionRegex.IsMatch(versionString);
    }

    static FieldInfo? s_modIDProp;
    static FieldInfo? s_versionProp;
    static void FindDependencyBackingFields()
    {
        foreach(var member in typeof(ModDependency).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            if (member.MemberType != MemberTypes.Field) continue;
            var name = member.Name.ToLower();
            if (name.Contains(nameof(ModDependency.ModID).ToLower())) {
                s_modIDProp = (FieldInfo)member;
            }
            else if (name.Contains(nameof(ModDependency.Version).ToLower())) {
                s_versionProp = (FieldInfo)member;
            }
        }
        Debug.Assert(s_modIDProp != null && s_versionProp != null);
    }
    /// <summary> Hacky way to crate a dependency object without invoking its constructor.
    /// The checks in there are not sufficient and i want to do them in a later place. </summary>
    static ModDependency NewDependencyUnchecked(string? modID, string? version)
    {
        if (s_modIDProp == null) FindDependencyBackingFields();
        var dep = (ModDependency)RuntimeHelpers.GetUninitializedObject(typeof(ModDependency));
        s_modIDProp!.SetValue(dep, modID);
        s_versionProp!.SetValue(dep, version);
        return dep;
    }

    static void PrintErrorToStdError(Errors.Error error)
    {
        Console.Error.WriteLine(FormatError(error));
    }

    public static string FormatError(Errors.Error error)
    {
        return error switch {
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
