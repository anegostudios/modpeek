using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;
public static partial class ModPeek
{
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

        switch (modInfo.ModID) {
            case "game": case "creative": case "survival":
                // These are the core mods
                break;

            default:
                if (modInfo.CoreMod) {
                    errorCallback(new Errors.NotACoreMod());
                    modInfo.CoreMod = false;
                    error = true;
                }
                break;
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
            errorCallback(new Errors.UnexpectedValue(nameof(ModInfo), nameof(modInfo.Type), nameof(EnumModType), modInfo.Type.ToString()));
            error = true;
            // Code mods are probably the ones with the highest security restrictions, so we pick that as a fallback.
            // We don't have a neutral default.
            modInfo.Type = EnumModType.Code;
        }

        if (!Enum.IsDefined(typeof(EnumAppSide), modInfo.Side)) {
            errorCallback(new Errors.UnexpectedValue(nameof(ModInfo), nameof(modInfo.Side), nameof(EnumAppSide), modInfo.Side.ToString()));
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
                errorCallback(new Errors.MalformedDependencyModID($"{nameof(ModInfo)}.{nameof(modInfo.Dependencies)}[{dependency.ModID}]", dependency.ModID));
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


    const EnumDataType BROKEN_DATA_TYPE = (EnumDataType)(-1);

    /// <summary>Validates the data inside a ModWorldConfiguration and sets invalid fields to blank / default values.</summary>
    /// <returns>False if any data is invalid.</returns>
    public static bool ValidateWorldConfig(ModWorldConfiguration worldConfig, Action<Errors.Error> errorCallback)
    {
        var error = false;

        if (worldConfig.PlayStyles == null) {
            errorCallback(new Errors.MissingRequiredProperty(nameof(ModWorldConfiguration), nameof(ModWorldConfiguration.PlayStyles)));
            error = true;
            worldConfig.PlayStyles = [];
        }
        else {
            var psList = new List<PlayStyle>(worldConfig.PlayStyles.Length);
            int i = -1;
            foreach(var playstyle in worldConfig.PlayStyles) {
                i++;

                if (string.IsNullOrWhiteSpace(playstyle.Code)) {
                    errorCallback(new Errors.UnexpectedValue(nameof(ModWorldConfiguration), $"{nameof(worldConfig.PlayStyles)}[{i}].{nameof(playstyle.Code)}", "non-empty string", playstyle.Code));
                    error = true;
                    playstyle.Code = ""; // unify
                }

                if (string.IsNullOrWhiteSpace(playstyle.PlayListCode)) {
                    errorCallback(new Errors.UnexpectedValue(nameof(ModWorldConfiguration), $"{nameof(worldConfig.PlayStyles)}[{i}].{nameof(playstyle.PlayListCode)}", "non-empty string", playstyle.PlayListCode));
                    error = true;
                    playstyle.PlayListCode = ""; // unify
                }

                if (string.IsNullOrWhiteSpace(playstyle.LangCode)) {
                    errorCallback(new Errors.UnexpectedValue(nameof(ModWorldConfiguration), $"{nameof(worldConfig.PlayStyles)}[{i}].{nameof(playstyle.LangCode)}", "non-empty string", playstyle.LangCode));
                    error = true;
                    playstyle.LangCode = ""; // unify
                }

                var modsList = new List<string>(playstyle.Mods.Length);
                int j = -1;
                foreach(var modId in playstyle.Mods) {
                    j++;

                    if(!ModInfo.IsValidModID(modId)) {
                        errorCallback(new Errors.MalformedDependencyModID($"{nameof(ModWorldConfiguration)}.{nameof(worldConfig.PlayStyles)}[{i}].{nameof(playstyle.Mods)}[{j}]", modId));
                        error = true;
                        continue;
                    }

                    modsList.Add(modId);
                }
                playstyle.Mods = modsList.ToArray();

                if (string.IsNullOrWhiteSpace(playstyle.WorldType)) {
                    errorCallback(new Errors.UnexpectedValue(nameof(ModWorldConfiguration), $"{nameof(worldConfig.PlayStyles)}[{i}].{nameof(playstyle.WorldType)}", "non-empty string", playstyle.WorldType));
                    error = true;
                    playstyle.WorldType = ""; // unify
                }

                psList.Add(playstyle);
            }
            worldConfig.PlayStyles = psList.ToArray();
        }

        if (worldConfig.WorldConfigAttributes == null) {
            errorCallback(new Errors.MissingRequiredProperty(nameof(ModWorldConfiguration), nameof(ModWorldConfiguration.WorldConfigAttributes)));
            error = true;
            worldConfig.WorldConfigAttributes = [];
        }
        else {
            var attributeList = new List<WorldConfigurationAttribute>(worldConfig.WorldConfigAttributes.Length);
            int i = -1;
            foreach(var attribute in worldConfig.WorldConfigAttributes) {
                i++;
                bool skipAttribute = false;

                if(attribute.DataType == BROKEN_DATA_TYPE) {
                    skipAttribute = true; // Already errored in the parsing phase, no error here.
                }
                else if(!Enum.IsDefined(attribute.DataType)) {
                    errorCallback(new Errors.UnexpectedValue(nameof(ModWorldConfiguration), $"{worldConfig.WorldConfigAttributes}[{i}].{nameof(attribute.DataType)}", nameof(EnumDataType), attribute.DataType.ToString()));
                    error = true;
                    skipAttribute = true;
                }

                if (string.IsNullOrWhiteSpace(attribute.Category)) {
                    errorCallback(new Errors.UnexpectedValue(nameof(ModWorldConfiguration), $"{nameof(worldConfig.WorldConfigAttributes)}[{i}].{nameof(attribute.Category)}", "non-empty string", attribute.Category));
                    error = true;
                    attribute.Category = ""; // unify
                }

                if (string.IsNullOrWhiteSpace(attribute.Code)) {
                    errorCallback(new Errors.UnexpectedValue(nameof(ModWorldConfiguration), $"{nameof(worldConfig.WorldConfigAttributes)}[{i}].{nameof(attribute.Code)}", "non-empty string", attribute.Code));
                    error = true;
                    skipAttribute = true;
                }

                if (attribute.Names != null) {
                    if (attribute.Values == null) {
                        errorCallback(new Errors.MissingRequiredProperty(nameof(ModWorldConfiguration), $"{nameof(worldConfig.WorldConfigAttributes)}[{i}].{nameof(attribute.Values)}") { Severity = Errors.Severity.Warning });
                        error = true;
                        skipAttribute = true;
                    }
                    else if (attribute.Values.Length != attribute.Names.Length) {
                        errorCallback(new Errors.ArrayLengthMismatch(nameof(ModWorldConfiguration),
                            $"{nameof(worldConfig.WorldConfigAttributes)}[{i}].{nameof(attribute.Values)}",
                            $"{nameof(worldConfig.WorldConfigAttributes)}[{i}].{nameof(attribute.Names)}"
                        ));
                        error = true;
                        skipAttribute = true;
                    }
                }

                if(!skipAttribute) attributeList.Add(attribute);
            }
            worldConfig.WorldConfigAttributes = attributeList.ToArray();
        }

        return !error;
    }
}
