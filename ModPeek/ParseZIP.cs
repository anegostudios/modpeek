using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

static partial class ModPeek
{
    // See tests for examples of the attributes we are trying to parse.
    static bool TryGetZipInfo(byte[] bytes, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
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
                errorCallback(new Errors.MissingFileInArchiveRoot("modinfo.json", commonPathPrefix != null));
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
                errorCallback(new Errors.MalformedJson(e));
                modInfo = null;
                return false;
            }
        }
        catch (Exception e)
        {
            errorCallback(new Errors.MalformedArchive(e));
            modInfo = null;
            return false;
        }
    }

    static bool TryParseJsonCaseInsensitive(JToken root, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
    {
        if(root.Type != JTokenType.Object) {
            errorCallback(new Errors.UnexpectedJsonRootType(JTokenType.Object, root));
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
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Name), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.Name = prop.Value.ToObject<string>();
                    break;

                case "modid":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.ModID), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.ModID = prop.Value.ToObject<string>();
                    break;

                case "version":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Version), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.Version = prop.Value.ToObject<string>();
                    break;

                case "networkversion":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.NetworkVersion), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.NetworkVersion = prop.Value.ToObject<string>();
                    break;

                case "texturesize":
                    if(prop.Value.Type != JTokenType.Integer) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.TextureSize), JTokenType.Integer, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.TextureSize = prop.Value.ToObject<int>();
                    break;

                case "side": {
                    if(prop.Value.Type != JTokenType.String) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Side), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    var sideStr = prop.Value.ToObject<string>();
                    if (!Enum.TryParse(sideStr, true, out EnumAppSide side)) {
                        errorCallback(new Errors.StringParsingFailure(nameof(ModInfo.Side), nameof(EnumAppSide), sideStr));
                        error = true;
                        break;
                    }

                    modInfo.Side = side;
                } break;

                case "type": {
                    if(prop.Value.Type != JTokenType.String) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Type), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    var typeStr = prop.Value.ToObject<string>();
                    if (!Enum.TryParse(typeStr, true, out EnumModType type)) {
                        errorCallback(new Errors.StringParsingFailure(nameof(ModInfo.Type), nameof(EnumModType), typeStr));
                        type = EnumModType.Code; // Likely most restricted category, we don't have a neutral default.
                        error = true;
                        break;
                    }

                    modInfo.Type = type;
                } break;

                case "requiredonclient":
                    if(prop.Value.Type != JTokenType.Boolean) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.RequiredOnClient), JTokenType.Boolean, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.RequiredOnClient = prop.Value.ToObject<bool>();
                    break;

                case "requiredonserver":
                    if(prop.Value.Type != JTokenType.Boolean) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.RequiredOnServer), JTokenType.Boolean, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.RequiredOnServer = prop.Value.ToObject<bool>();
                    break;

                case "description":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Description), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.Description = prop.Value.ToObject<string>();
                    break;

                case "website":
                    if(prop.Value.Type != JTokenType.String && prop.Value.Type != JTokenType.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Website), JTokenType.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.Website = prop.Value.ToObject<string>();
                    break;

                case "authors": {
                    if(prop.Value.Type != JTokenType.Array) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Authors), JTokenType.Array, prop.Value));
                        error = true;
                        break;
                    }

                    var authors = new List<string>();
                    int i = 0;
                    foreach(var element in prop.Value.Values()) {
                        if(element.Type != JTokenType.String) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModInfo.Authors)}[{i}]", JTokenType.String, element));
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
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Contributors), JTokenType.Array, prop.Value));
                        error = true;
                        break;
                    }

                    var contributors = new List<string>();
                    int i = 0;
                    foreach(var element in prop.Value.Values()) {
                        if(element.Type != JTokenType.String) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModInfo.Contributors)}[{i}]", JTokenType.String, element));
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
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Dependencies), JTokenType.Object, prop.Value));
                        error = true;
                        break;
                    }

                    var dependencies = new List<ModDependency>();
                    foreach(var depProp in ((JObject)prop.Value).Properties()) {
                        if(depProp.Value.Type != JTokenType.String && depProp.Value.Type != JTokenType.Null) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModInfo.Dependencies)}[{depProp.Name}]", JTokenType.String, depProp.Value));
                            error = true;
                            continue;
                        }

                        dependencies.Add(NewDependencyUnchecked(depProp.Name, depProp.Value.ToObject<string>()));
                    }

                    modInfo.Dependencies = dependencies;
                } break;

                default:
                    errorCallback(new Errors.UnexpectedProperty(prop.Name, prop.Value.ToString(Formatting.None)));
                    error = true;
                    break;
            }
        }

        return !error;
    }
}

