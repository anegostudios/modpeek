using System.Text.Json;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    /// <remarks> The ModInfo obtained from this function has not been validated! </remarks>
    static bool TryParseModInfoFromJsonCaseInsensitive(JsonDocument json, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
    {
        if(json.RootElement.ValueKind != JsonValueKind.Object) {
            errorCallback(new Errors.UnexpectedJsonRootType(JsonValueKind.Object, json.RootElement));
            Console.Error.WriteLine($"The root json node must be an object, but was a {json.RootElement.ValueKind}.");
            modInfo = null;
            return false;
        }

        var error = false;
        modInfo = new ModInfo();

        foreach(var prop in json.RootElement.EnumerateObject()) {
            switch(prop.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture)) {
                //NOTE(Rennorb) Cannot apply ToLower to nameof while keeping it const, so i have to manually specify these names...
                case "custom":
                    // Custom field that is completely ignored by validation.
                    break;

                case "$schema": // Allow usage of a json schema, even if its not in the spec.
                    if(prop.Value.ValueKind != JsonValueKind.String) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(prop.Name, JsonValueKind.String, prop.Value));
                        error = true;
                    }
                    break;

                case "name":
                    if(prop.Value.ValueKind != JsonValueKind.String && prop.Value.ValueKind != JsonValueKind.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Name), JsonValueKind.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.Name = prop.Value.GetString();
                    break;

                case "modid":
                    if(prop.Value.ValueKind != JsonValueKind.String && prop.Value.ValueKind != JsonValueKind.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.ModID), JsonValueKind.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.ModID = prop.Value.GetString();
                    break;

                case "version":
                    if(prop.Value.ValueKind != JsonValueKind.String && prop.Value.ValueKind != JsonValueKind.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Version), JsonValueKind.String, prop.Value));
                        modInfo.Version = BROKEN_VERSION;
                        error = true;
                        break;
                    }

                    modInfo.Version = prop.Value.GetString();
                    break;

                case "networkversion":
                    if(prop.Value.ValueKind != JsonValueKind.String && prop.Value.ValueKind != JsonValueKind.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.NetworkVersion), JsonValueKind.String, prop.Value));
                        modInfo.NetworkVersion = BROKEN_VERSION;
                        error = true;
                        break;
                    }

                    modInfo.NetworkVersion = prop.Value.GetString();
                    break;

                case "texturesize":
                    if(prop.Value.ValueKind != JsonValueKind.Number) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.TextureSize), JsonValueKind.Number, prop.Value));
                        error = true;
                        break;
                    }

                    if(!prop.Value.TryGetInt32(out modInfo.TextureSize)) {
                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfo.TextureSize), "int32", prop.Value.GetRawText()));
                        error = true;
                    }
                    break;

                case "side": {
                    if(prop.Value.ValueKind != JsonValueKind.String) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Side), JsonValueKind.String, prop.Value));
                        error = true;
                        break;
                    }

                    var sideStr = prop.Value.GetString()!;
                    if (!Enum.TryParse(sideStr, true, out EnumAppSide side)) {
                        errorCallback(new Errors.StringParsingFailure(nameof(ModInfo.Side), nameof(EnumAppSide), sideStr));
                        error = true;
                        break;
                    }

                    modInfo.Side = side;
                } break;

                case "type": {
                    if(prop.Value.ValueKind != JsonValueKind.String) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Type), JsonValueKind.String, prop.Value));
                        error = true;
                        break;
                    }

                    var typeStr = prop.Value.GetString()!;
                    if (!Enum.TryParse(typeStr, true, out EnumModType type)) {
                        errorCallback(new Errors.StringParsingFailure(nameof(ModInfo.Type), nameof(EnumModType), typeStr));
                        type = EnumModType.Code; // Likely most restricted category, we don't have a neutral default.
                        error = true;
                        break;
                    }

                    modInfo.Type = type;
                } break;

                case "requiredonclient":
                    if(prop.Value.ValueKind != JsonValueKind.True && prop.Value.ValueKind != JsonValueKind.False) {
                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfo.RequiredOnClient), "boolean", prop.Value.GetRawText()));
                        error = true;
                        break;
                    }

                    modInfo.RequiredOnClient = prop.Value.GetBoolean();
                    break;

                case "requiredonserver":
                    if(prop.Value.ValueKind != JsonValueKind.True && prop.Value.ValueKind != JsonValueKind.False) {
                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfo.RequiredOnServer), "boolean", prop.Value.GetRawText()));
                        error = true;
                        break;
                    }

                    modInfo.RequiredOnServer = prop.Value.GetBoolean();
                    break;

                case "iconpath": {
                    if(prop.Value.ValueKind != JsonValueKind.String) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.IconPath), JsonValueKind.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.IconPath = prop.Value.GetString();
                } break;

                case "description":
                    if(prop.Value.ValueKind != JsonValueKind.String && prop.Value.ValueKind != JsonValueKind.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Description), JsonValueKind.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.Description = prop.Value.GetString();
                    break;

                case "website":
                    if(prop.Value.ValueKind != JsonValueKind.String && prop.Value.ValueKind != JsonValueKind.Null) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Website), JsonValueKind.String, prop.Value));
                        error = true;
                        break;
                    }

                    modInfo.Website = prop.Value.GetString();
                    break;

                case "authors": {
                    if(prop.Value.ValueKind != JsonValueKind.Array) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Authors), JsonValueKind.Array, prop.Value));
                        error = true;
                        break;
                    }

                    var authors = new List<string>();
                    int i = 0;
                    foreach(var element in prop.Value.EnumerateArray()) {
                        if(element.ValueKind != JsonValueKind.String) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModInfo.Authors)}[{i}]", JsonValueKind.String, element));
                            error = true;
                        }
                        else {
                            authors.Add(element.GetString()!);
                        }
                        i++;
                    }

                    modInfo.Authors = authors;
                } break;

                case "contributors":{
                    if(prop.Value.ValueKind != JsonValueKind.Array) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Contributors), JsonValueKind.Array, prop.Value));
                        error = true;
                        break;
                    }

                    var contributors = new List<string>();
                    int i = 0;
                    foreach(var element in prop.Value.EnumerateArray()) {
                        if(element.ValueKind != JsonValueKind.String) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModInfo.Contributors)}[{i}]", JsonValueKind.String, element));
                            error = true;
                        }
                        else {
                            contributors.Add(element.GetString()!);
                        }
                        i++;
                    }

                    modInfo.Contributors = contributors;
                } break;

                case "dependencies":{
                    if(prop.Value.ValueKind != JsonValueKind.Object) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModInfo.Dependencies), JsonValueKind.Object, prop.Value));
                        error = true;
                        break;
                    }

                    var dependencies = new List<ModDependency>();
                    foreach(var depProp in prop.Value.EnumerateObject()) {
                        if(depProp.Value.ValueKind != JsonValueKind.String && depProp.Value.ValueKind != JsonValueKind.Null) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModInfo.Dependencies)}[{depProp.Name}]", JsonValueKind.String, depProp.Value));
                            error = true;
                            continue;
                        }

                        dependencies.Add(NewDependencyUnchecked(depProp.Name, depProp.Value.GetString()));
                    }

                    modInfo.Dependencies = dependencies;
                } break;

                default:
                    errorCallback(new Errors.UnexpectedProperty(prop.Name, prop.Value.GetRawText()));
                    error = true;
                    break;
            }
        }

        return !error;
    }
}

