using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

static partial class ModPeek
{
    static bool TryGetZipInfo(byte[] bytes, out ModInfo? modInfo, Action<Error> errorCallback)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            var modInfoEntry = zip.GetEntry("modinfo.json");
            if (modInfoEntry == null) {
                // Figure out if all entries start with the same path prefix. In that case the user zipped a folder instead of individual files.
                string? commonPathPrefix = null;
                foreach(var entry in zip.Entries) {
                    var firstSeparator = entry.FullName.IndexOf('/');
                    if (firstSeparator == -1) { // Indicates an individual file in the root, we are already good.
                        commonPathPrefix = null;
                        break;
                    }

                    if (commonPathPrefix == null) {
                        commonPathPrefix = entry.FullName.Substring(0, firstSeparator + 1);
                    }
                    else if (!entry.FullName.StartsWith(commonPathPrefix)) { // Found other file / folder, so its not just one root directory.
                        commonPathPrefix = null;
                        break;
                    }
                }
                errorCallback(Errors.MissingFileInArchiveRoot("modinfo.json", commonPathPrefix != null));
                modInfo = null;
                return false;
            }

            using var reader = new StreamReader(modInfoEntry.Open());
            var content = reader.ReadToEnd();
            try {
                var root = JToken.Parse(content);
                return TryParseJsonCaseInsensitive(root, out modInfo, errorCallback);
            }
            catch(Exception e) {
                errorCallback(Errors.MalformedJson(e));
                modInfo = null;
                return false;
            }
        }
        catch (Exception e)
        {
            errorCallback(Errors.MalformedArchive(e));
            modInfo = null;
            return false;
        }
    }

    static bool TryParseJsonCaseInsensitive(JToken root, out ModInfo? modInfo, Action<Error> errorCallback)
    {
        if(root.Type != JTokenType.Object) {
            errorCallback(Errors.UnexpectedJsonRootType(root, JTokenType.Object));
            Console.Error.WriteLine($"The root json node must be an object, but was a {root.Type}.");
            modInfo = null;
            return false;
        }

        var error = false;
        modInfo = new ModInfo();

        foreach(var prop in ((JObject)root).Properties()) {
            switch(prop.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture)) {
                //NOTE(Rennorb) Cannot apply ToLower to nameof while keeping it const, so i have to manually specify these names...
                case "name":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.String, nameof(ModInfo.Name)));
                        error = true;
                        break;
                    }

                    modInfo.Name = prop.Value.ToObject<string>();
                    break;

                case "modid":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.String, nameof(ModInfo.ModID)));
                        error = true;
                        break;
                    }

                    modInfo.ModID = prop.Value.ToObject<string>();
                    break;

                case "version":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.String, nameof(ModInfo.Version)));
                        error = true;
                        break;
                    }

                    modInfo.Version = prop.Value.ToObject<string>();
                    break;

                case "networkversion":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.String, nameof(ModInfo.NetworkVersion)));
                        error = true;
                        break;
                    }

                    modInfo.NetworkVersion = prop.Value.ToObject<string>();
                    break;

                case "texturesize":
                    if(prop.Value.Type != JTokenType.Integer) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.Integer, nameof(ModInfo.TextureSize)));
                        error = true;
                        break;
                    }

                    modInfo.TextureSize = prop.Value.ToObject<int>();
                    break;

                case "side": {
                    if(prop.Value.Type != JTokenType.String) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.String, nameof(ModInfo.Side)));
                        error = true;
                        break;
                    }

                    var sideStr = prop.Value.ToObject<string>();
                    if (!Enum.TryParse(sideStr, true, out EnumAppSide side)) {
                        errorCallback(Errors.StringParsingFailure(sideStr, nameof(EnumAppSide), nameof(ModInfo.Side)));
                        error = true;
                        break;
                    }

                    modInfo.Side = side;
                } break;

                case "type": {
                    if(prop.Value.Type != JTokenType.String) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.String, nameof(ModInfo.Type)));
                        error = true;
                        break;
                    }

                    var typeStr = prop.Value.ToObject<string>();
                    if (!Enum.TryParse(typeStr, true, out EnumModType type)) {
                        errorCallback(Errors.StringParsingFailure(typeStr, nameof(EnumModType), nameof(ModInfo.Type)));
                        type = EnumModType.Code; // Likely most restricted category, we don't have a neutral default.
                        error = true;
                        break;
                    }

                    modInfo.Type = type;
                } break;

                case "requiredonclient":
                    if(prop.Value.Type != JTokenType.Boolean) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.Boolean, nameof(ModInfo.RequiredOnClient)));
                        error = true;
                        break;
                    }

                    modInfo.RequiredOnClient = prop.Value.ToObject<bool>();
                    break;

                case "requiredonserver":
                    if(prop.Value.Type != JTokenType.Boolean) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.Boolean, nameof(ModInfo.RequiredOnServer)));
                        error = true;
                        break;
                    }

                    modInfo.RequiredOnServer = prop.Value.ToObject<bool>();
                    break;

                case "description":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        Console.Error.WriteLine($"The Description property is not a string ('{prop.Value.ToString(Formatting.None)}').");
                        error = true;
                        break;
                    }

                    modInfo.Description = prop.Value.ToObject<string>();
                    break;

                case "website":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.String, nameof(ModInfo.Website)));
                        error = true;
                        break;
                    }

                    modInfo.Website = prop.Value.ToObject<string>();
                    break;

                case "authors": {
                    if(prop.Value.Type != JTokenType.Array) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.Array, nameof(ModInfo.Authors)));
                        error = true;
                        break;
                    }

                    var authors = new List<string>();
                    int i = 0;
                    foreach(var element in prop.Value.Values()) {
                        if(element.Type != JTokenType.String) {
                            errorCallback(Errors.UnexpectedJsonPropertyType(element, JTokenType.String, $"{nameof(ModInfo.Authors)}[{i}]"));
                            error = true;
                        }
                        else {
                            authors.Add(element.ToObject<string>());
                        }
                        i++;
                    }

                    modInfo.Authors = authors;
                } break;

                case "contributors":{
                    if(prop.Value.Type != JTokenType.Array) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.Array, nameof(ModInfo.Contributors)));
                        error = true;
                        break;
                    }

                    var contributors = new List<string>();
                    int i = 0;
                    foreach(var element in prop.Value.Values()) {
                        if(element.Type != JTokenType.String) {
                            errorCallback(Errors.UnexpectedJsonPropertyType(element, JTokenType.String, $"{nameof(ModInfo.Contributors)}[{i}]"));
                            error = true;
                        }
                        else {
                            contributors.Add(element.ToObject<string>());
                        }
                        i++;
                    }

                    modInfo.Contributors = contributors;
                } break;

                case "dependencies":{
                    if(prop.Value.Type != JTokenType.Object) {
                        errorCallback(Errors.UnexpectedJsonPropertyType(prop.Value, JTokenType.Object, nameof(ModInfo.Dependencies)));
                        error = true;
                        break;
                    }

                    var dependencies = new List<ModDependency>();
                    foreach(var depProp in ((JObject)prop.Value).Properties()) {
                        if(depProp.Value.Type != JTokenType.String && depProp.Value.Type != JTokenType.Null) {
                            errorCallback(Errors.UnexpectedJsonPropertyType(depProp.Value, JTokenType.String, $"{nameof(ModInfo.Dependencies)}[{depProp.Name}]"));
                            error = true;
                            continue;
                        }

                        dependencies.Add(NewDependencyUnchecked(depProp.Name, depProp.Value.ToObject<string>()));
                    }

                    modInfo.Dependencies = dependencies;
                } break;

                default:
                    errorCallback(Errors.UnexpectedProperty(prop.Name, prop.Value.ToString(Formatting.None)));
                    error = true;
                    break;
            }
        }

        return !error;
    }
}

