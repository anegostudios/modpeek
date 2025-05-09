using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using System.Runtime.Serialization;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;


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
        if (error || modInfo == null) {
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
            Console.WriteLine("Id: " + modInfo.ModID);
            Console.WriteLine("Name: " + modInfo.Name);
            Console.WriteLine("Version: " + modInfo.Version);
            Console.WriteLine("NetworkVersion: " + modInfo.NetworkVersion);
            Console.WriteLine("Description: " + modInfo.Description.Replace("\r", "").Replace("\n", @"\n"));
            Console.WriteLine("Authors: " + string.Join(", ", modInfo.Authors));
            Console.WriteLine("Contributors: " + string.Join(", ", modInfo.Contributors));
            Console.WriteLine("Website: " + modInfo.Website);
            Console.WriteLine("Dependencies: " + string.Join(", ", modInfo.Dependencies));
        }

        Environment.Exit(error ? 1 : 0);
    }

    public static bool TryGetModInfo(FileInfo f, out ModInfo? modInfo, Action<Error> errorCallback)
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
    public static bool ValidateModInfo(ModInfo modInfo, Action<Error> errorCallback)
    {
        var error = false;
            
        if (string.IsNullOrWhiteSpace(modInfo.Name)) {
            errorCallback(Errors.MissingRequiredProperty(nameof(ModInfo), nameof(modInfo.Name)));
            error = true;
            modInfo.Name = null;
        }

        if (string.IsNullOrWhiteSpace(modInfo.ModID)) {
            if (!string.IsNullOrWhiteSpace(modInfo.Name)) {
                try {
                    modInfo.ModID = ModInfo.ToModID(modInfo.Name);
                }
                catch (ArgumentException e) {
                    errorCallback(Errors.ModIDGenerationFailure(e, modInfo.Name!));
                    modInfo.ModID = null;
                    error = true;
                }
            }
            else {
                errorCallback(Errors.MissingRequiredProperty(nameof(ModInfo), nameof(modInfo.ModID)));
                modInfo.ModID = null;
                error = true;
            }
        }
        else if (!ModInfo.IsValidModID(modInfo.ModID)) {
            errorCallback(Errors.MalformedModID(modInfo.ModID, nameof(modInfo.ModID), Errors.Severity.Fatal));
            error = true;
            modInfo.ModID = null;
        }

        if (string.IsNullOrWhiteSpace(modInfo.Version)) {
            modInfo.Version = null; // unify the value
        }
        else {
            if (!IsValidVersion(modInfo.Version)) {
                errorCallback(Errors.MalformedVersion(modInfo.Version, nameof(modInfo.Version)));
                error = true;
                modInfo.Version = null;
            }
        }

        if (string.IsNullOrWhiteSpace(modInfo.NetworkVersion)) {
            modInfo.NetworkVersion = null; // unify the value
        }
        else {
            if (!IsValidVersion(modInfo.NetworkVersion)) {
                errorCallback(Errors.MalformedVersion(modInfo.NetworkVersion, nameof(modInfo.NetworkVersion)));
                error = true;
                modInfo.NetworkVersion = null;
            }
        }

        if (!Enum.IsDefined(typeof(EnumModType), modInfo.Type)) {
            errorCallback(Errors.UnexpectedValue(modInfo.Type.ToString(), nameof(EnumModType), nameof(modInfo.Type)));
            error = true;
            // Code probably the one wit the highest security restrictions, so we pick this one as a fallback.
            // We don't have a neutral default.
            modInfo.Type = EnumModType.Code;
        }

        if (!Enum.IsDefined(typeof(EnumAppSide), modInfo.Side)) {
            errorCallback(Errors.UnexpectedValue(modInfo.Side.ToString(), nameof(EnumAppSide), nameof(modInfo.Side)));
            error = true;
            modInfo.Side = 0;
        }

        if (string.IsNullOrWhiteSpace(modInfo.Website)) {
            modInfo.Website = null; // unify the value
        }
        else {
            try {
                _ = new Uri(modInfo.Website);
            }
            catch {
                errorCallback(Errors.StringParsingFailure(modInfo.Website, "URL", nameof(modInfo.Website)));
                modInfo.Website = null;
                error = true;
            }
        }

        var authors = (modInfo.Authors as List<string>) ?? modInfo.Authors.ToList();
        for (int i = authors.Count - 1; i >= 0; i--) {
            var author = authors[i];
            foreach(var c in author) {
                if(c == '\n' || c == '\r') {
                    errorCallback(Errors.MalformedAuthorName(author));
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
                    errorCallback(Errors.MalformedContributorName(contributor));
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
                errorCallback(Errors.MissingDependencyModID());
                error = true;
                dependencies.RemoveAt(i);
                continue;
            }
            if (!ModInfo.IsValidModID(dependency.ModID)) {
                errorCallback(Errors.MalformedDependencyModID(dependency.ModID));
                dependencies.RemoveAt(i);
                error = true;
                continue;
            }

            if (string.IsNullOrEmpty(dependency.Version) || dependency.Version == "*") {
                if (s_modIDProp == null) FindDependencyBackingFields();
                s_versionProp!.SetValue(dependency, null); // unify the value
            }
            else if (!IsValidVersion(dependency.Version)) {
                errorCallback(Errors.MalformedDependencyVersion(dependency.Version, dependency.ModID));
                error = true;
                dependencies.RemoveAt(i);
                continue;
            }
        }
        if (modInfo.Dependencies.Count != dependencies.Count) modInfo.Dependencies = dependencies;

        return !error;
    }

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
        var dep = (ModDependency)FormatterServices.GetUninitializedObject(typeof(ModDependency));
        s_modIDProp!.SetValue(dep, modID);
        s_versionProp!.SetValue(dep, version);
        return dep;
    }

    static void PrintErrorToStdError(Error error)
    {
        Console.Error.WriteLine(FormatError(error));
    }

    public static string FormatError(Error error)
    {
        return error.Kind switch {
            Errors.Kind.MalformedArchive           => $"The zip archive failed to decode: {error.MalformedArchive.Exception.Message}.",
            Errors.Kind.MissingFileInArchiveRoot   => $"The zip archive is missing a file named {error.MissingFileInArchiveRoot.FileName} in its root."+
                (error.MissingFileInArchiveRoot.LikelyCompressedDirectory ? " All files in the archive share a common parent directory, so you likely compressed a directory instead of individual files." : ""),
            Errors.Kind.MalformedJson              => $"The provided modinfo.json was malformed: {error.MalformedJson.Exception.Message}.",
            Errors.Kind.UnexpectedJsonRootType     => $"The root element of the modinfo.json must be a {error.UnexpectedJsonPropertyType.Expected}, but was {error.UnexpectedJsonPropertyType.Given.Type}.",
            Errors.Kind.MissingAssemblyAttribute   => $"Missing expected assembly-attribute '{error.MissingAssemblyAttribute.AttributeName}'.",
            Errors.Kind.PrimitiveParsingFailure    => $"The {error.ParsingFailure.TargetProperty} property failed to parse as a {error.ParsingFailure.ExpectedType} (was '{error.ParsingFailure.MalformedInput}').",
            Errors.Kind.StringParsingFailure       => $"The {error.ParsingFailure.TargetProperty} property failed to convert from a string to {error.ParsingFailure.ExpectedType} (was '{error.ParsingFailure.MalformedInput}').",
            Errors.Kind.MissingRequiredProperty    => $"{error.MissingRequiredProperty.TargetStructure} is missing the required property '{error.MissingRequiredProperty.PropertyName}'.",
            Errors.Kind.UnexpectedProperty         => $"Unexpected property '{error.UnexpectedProperty.PropertyName}' with value '{error.UnexpectedProperty.PropertyValue}'.",
            Errors.Kind.UnexpectedValue            => $"Property '{error.UnexpectedValue.TargetProperty}' was expected to be {error.UnexpectedValue.Expected}, but was '{error.UnexpectedValue.Given}'.",
            Errors.Kind.UnexpectedJsonPropertyType => $"Property '{error.UnexpectedJsonPropertyType.TargetProperty}' was expected to be of type {error.UnexpectedJsonPropertyType.Expected}, but was '{error.UnexpectedJsonPropertyType.Given.ToString(Newtonsoft.Json.Formatting.None)}'.",
            Errors.Kind.MalformedModID             => $"The {error.MalformedModID.TargetProperty} property contains a malformed ModID ('{error.MalformedModID.MalformedInput}').",
            Errors.Kind.MalformedVersion           => $"The {error.MalformedVersion.TargetProperty} property contains a malformed Version ('{error.MalformedVersion.MalformedInput}').",
            Errors.Kind.ModIDGenerationFailure     => $"Mod name '{error.ModIDGenerationFailure.MalformedInput}' failed to be converted to a ModID: {error.ModIDGenerationFailure.Exception}.",
            Errors.Kind.MissingDependencyModID     => $"A dependency was specified that does not have target ModID set.",
            Errors.Kind.MalformedDependencyModID   => $"{error.MalformedModID.TargetProperty} specifies a malformed ModID.",
            Errors.Kind.MalformedDependencyVersion => $"{error.MalformedVersion.TargetProperty} specifies a malformed target Version ('{error.MalformedVersion.MalformedInput}').",
            Errors.Kind.MalformedAuthorName        => $"'{error.MalformedAuthorName.MalformedInput}' is not a valid author name.",
            Errors.Kind.MalformedContributorName   => $"'{error.MalformedContributorName.MalformedInput}' is not a valid contributor name.",
            _ => "Unknown error: " + error.Kind,
        };
    }
}


//NOTE(Rennorb): These TryParse and Validate functions don't fast-fail, instead they report every warning they can detect.
// We do this so that we are able to report all warnings at once, and a user doesn't have to go though multiple cycles of 
// validating if there are multiple issues with the ModInfo.

[StructLayout(LayoutKind.Explicit)]
public struct Error(Errors.Severity severity, Errors.Kind kind) {
    [FieldOffset(0)] public Errors.Severity Severity = severity;
    [FieldOffset(1)] public Errors.Kind     Kind     = kind;


    [FieldOffset(8)] public _MalformedArchive MalformedArchive;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MalformedArchive
    {
        public Exception Exception;
    }

    [FieldOffset(8)] public _MalformedJson MalformedJson;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MalformedJson
    {
        public Exception Exception;
    }

    [FieldOffset(8)] public _MissingAssemblyAttribute MissingAssemblyAttribute;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MissingAssemblyAttribute
    {
        public string AttributeName;
    }

    [FieldOffset(8)] public _MissingFileInArchiveRoot MissingFileInArchiveRoot;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MissingFileInArchiveRoot
    {
        public string FileName;
        object _pad1;
        object _pad2;
        public bool   LikelyCompressedDirectory;
    }

    [FieldOffset(8)] public _ParsingFailure ParsingFailure;
    [StructLayout(LayoutKind.Sequential)]
    public struct _ParsingFailure
    {
        public string MalformedInput;
        public string ExpectedType;
        public string TargetProperty;
    }

    [FieldOffset(8)] public _MissingRequiredProperty MissingRequiredProperty;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MissingRequiredProperty
    {
        public string TargetStructure;
        public string PropertyName;
    }

    [FieldOffset(8)] public _UnexpectedProperty UnexpectedProperty;
    [StructLayout(LayoutKind.Sequential)]
    public struct _UnexpectedProperty
    {
        public string PropertyName;
        public string PropertyValue;
    }

    [FieldOffset(8)] public _UnexpectedValue UnexpectedValue;
    [StructLayout(LayoutKind.Sequential)]
    public struct _UnexpectedValue
    {
        public string Given;
        public string Expected;
        public string TargetProperty;
    }

    [FieldOffset(8)] public _UnexpectedJsonPropertyType UnexpectedJsonPropertyType;
    [StructLayout(LayoutKind.Sequential)]
    public struct _UnexpectedJsonPropertyType
    {
        public JToken     Given;
        public string     TargetProperty;
        object _pad; // just a fix for wired alignment behavior
        public JTokenType Expected;
    }

    [FieldOffset(8)] public _MalformedModID MalformedModID;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MalformedModID
    {
        public string MalformedInput;
        public string TargetProperty;
    }

    [FieldOffset(8)] public _MalformedVersion MalformedVersion;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MalformedVersion
    {
        public string MalformedInput;
        public string TargetProperty;
    }

    [FieldOffset(8)] public _ModIDGenerationFailure ModIDGenerationFailure;
    [StructLayout(LayoutKind.Sequential)]
    public struct _ModIDGenerationFailure
    {
        public Exception Exception;
        public string MalformedInput;
    }

    [FieldOffset(8)] public _MalformedAuthorName MalformedAuthorName;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MalformedAuthorName
    {
        public string MalformedInput;
    }

    [FieldOffset(8)] public _MalformedContributorName MalformedContributorName;
    [StructLayout(LayoutKind.Sequential)]
    public struct _MalformedContributorName
    {
        public string MalformedInput;
    }
}

public static class Errors
{
    public enum Severity : byte { Fatal, Warning }
    public enum Kind : byte {
        MalformedArchive,
        MissingFileInArchiveRoot,

        MalformedJson,
        UnexpectedJsonRootType,

        MissingAssemblyAttribute,

        /// <summary> Can occur when a document gets parsed into primitive types. </summary>
        PrimitiveParsingFailure,
        /// <summary> Can occur when a string gets parsed into a different type. </summary>
        StringParsingFailure,

        MissingRequiredProperty,
        UnexpectedProperty,
        UnexpectedValue,
        UnexpectedJsonPropertyType,

        MalformedModID,
        MalformedVersion,
        ModIDGenerationFailure,

        MissingDependencyModID,
        MalformedDependencyModID,
        MalformedDependencyVersion,

        MalformedAuthorName,
        MalformedContributorName,
    }

    public static Error MalformedArchive(Exception exception) => new(Severity.Fatal, Kind.MalformedArchive) { MalformedArchive = { Exception = exception } };
    public static Error MalformedJson(Exception exception) => new(Severity.Fatal, Kind.MalformedJson) { MalformedJson = { Exception = exception } };
    public static Error UnexpectedJsonRootType(JToken given, JTokenType expected) => new(Severity.Fatal, Kind.UnexpectedJsonRootType) { UnexpectedJsonPropertyType = { Given = given, Expected = expected, TargetProperty = "<root>" } };
    public static Error MissingAssemblyAttribute(string missingAttributeName) => new(Severity.Fatal, Kind.MissingAssemblyAttribute) { MissingAssemblyAttribute = { AttributeName = missingAttributeName } };
    public static Error MissingFileInArchiveRoot(string missingFileName, bool likelyCompressedDirectory) => new(Severity.Fatal, Kind.MissingFileInArchiveRoot) { MissingFileInArchiveRoot = { FileName = missingFileName, LikelyCompressedDirectory = likelyCompressedDirectory } };
    public static Error PrimitiveParsingFailure(string malformedInput, string expectedType, string targetProperty) => new(Severity.Warning, Kind.PrimitiveParsingFailure) { ParsingFailure = { MalformedInput = malformedInput, ExpectedType = expectedType, TargetProperty = targetProperty } };
    public static Error StringParsingFailure(string malformedInput, string expectedType, string targetProperty) => new(Severity.Warning, Kind.StringParsingFailure) { ParsingFailure = { MalformedInput = malformedInput, ExpectedType = expectedType, TargetProperty = targetProperty } };
    public static Error MissingRequiredProperty(string targetStructure, string propertyName) => new(Severity.Fatal, Kind.MissingRequiredProperty) { MissingRequiredProperty = { TargetStructure = targetStructure, PropertyName = propertyName } };
    public static Error UnexpectedProperty(string propertyName, string propertyValue) => new(Severity.Warning, Kind.UnexpectedProperty) { UnexpectedProperty = { PropertyName = propertyName, PropertyValue = propertyValue } };
    public static Error UnexpectedValue(string given, string expected, string targetProperty) => new(Severity.Warning, Kind.UnexpectedValue) { UnexpectedValue = { Given = given, Expected = expected, TargetProperty = targetProperty } };
    public static Error UnexpectedJsonPropertyType(JToken given, JTokenType expected, string targetProperty) => new(Severity.Warning, Kind.UnexpectedJsonPropertyType) { UnexpectedJsonPropertyType = { Given = given, Expected = expected, TargetProperty = targetProperty } };
    public static Error MalformedModID(string malformedInput, string targetProperty, Severity severity) => new(severity, Kind.MalformedModID) { MalformedModID = { MalformedInput = malformedInput, TargetProperty = targetProperty } };
    public static Error MalformedVersion(string malformedInput, string targetProperty) => new(Severity.Warning, Kind.MalformedVersion) { MalformedVersion = { MalformedInput = malformedInput, TargetProperty = targetProperty } };
    public static Error ModIDGenerationFailure(Exception exception, string malformedInput) => new(Severity.Fatal, Kind.ModIDGenerationFailure) { ModIDGenerationFailure = { Exception = exception, MalformedInput = malformedInput } };
    public static Error MissingDependencyModID() => new(Severity.Warning, Kind.MissingDependencyModID) { };
    public static Error MalformedDependencyModID(string malformedInput) => new(Severity.Warning, Kind.MalformedDependencyModID) { MalformedModID = { MalformedInput = malformedInput, TargetProperty = $"{nameof(ModInfo.Dependencies)}[{malformedInput}]" } };
    public static Error MalformedDependencyVersion(string malformedInput, string targetDependency) => new(Severity.Warning, Kind.MalformedDependencyVersion) { MalformedVersion = { MalformedInput = malformedInput, TargetProperty = $"{nameof(ModInfo.Dependencies)}[{targetDependency}]" } };
    public static Error MalformedAuthorName(string malformedInput) => new(Severity.Warning, Kind.MalformedAuthorName) { MalformedAuthorName = { MalformedInput = malformedInput } };
    public static Error MalformedContributorName(string malformedInput) => new(Severity.Warning, Kind.MalformedContributorName) { MalformedContributorName = { MalformedInput = malformedInput } };
}
