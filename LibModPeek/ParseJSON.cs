using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    /// <remarks> The ModInfo obtained from this function has not been validated! </remarks>
    static bool TryParseModInfoFromJsonCaseInsensitive(JsonDocument json, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
    {
        if(json.RootElement.ValueKind != JsonValueKind.Object) {
            errorCallback(new Errors.UnexpectedJsonRootType("modinfo.json", JsonValueKind.Object, json.RootElement));
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
                    errorCallback(new Errors.UnexpectedProperty("modinfo.json", prop.Name, prop.Value.GetRawText()));
                    error = true;
                    break;
            }
        }

        return !error;
    }

    static bool TryParseWorldConfigFromJsonCaseInsensitive(string sourceStructure, JsonDocument json, out ModWorldConfiguration? worldConfig, Action<Errors.Error> errorCallback)
    {
        if(json.RootElement.ValueKind != JsonValueKind.Object) {
            errorCallback(new Errors.UnexpectedJsonRootType(sourceStructure, JsonValueKind.Object, json.RootElement));
            worldConfig = null;
            return false;
        }

        var error = false;
        worldConfig = new ModWorldConfiguration();

        foreach(var prop in json.RootElement.EnumerateObject()) {
            switch(prop.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture)) {
                //NOTE(Rennorb) Cannot apply ToLower to nameof while keeping it const, so i have to manually specify these names...
                case "$schema": // Allow usage of a json schema, even if its not in the spec.
                    if(prop.Value.ValueKind != JsonValueKind.String) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(prop.Name, JsonValueKind.String, prop.Value));
                        error = true;
                    }
                    break;

                case "playstyles": {
                    if(prop.Value.ValueKind != JsonValueKind.Array) {
                        errorCallback(new Errors.UnexpectedJsonPropertyType(nameof(ModWorldConfiguration.PlayStyles), JsonValueKind.Array, prop.Value));
                        error = true;
                        break;
                    }

                    var playstyles = new List<PlayStyle>();
                    int i = -1;
                    foreach(var element in prop.Value.EnumerateArray()) {
                        i++;
                        if(element.ValueKind != JsonValueKind.Object) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}]", JsonValueKind.Object, element));
                            error = true;
                            continue;
                        }

                        var playStyle = new PlayStyle();
                        foreach(var psProp in element.EnumerateObject()) {
                            switch(psProp.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture)) {
                                case "code":
                                    if(psProp.Value.ValueKind != JsonValueKind.String && psProp.Value.ValueKind != JsonValueKind.Null) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.Code)}", JsonValueKind.String, psProp.Value));
                                        error = true;
                                        break;
                                    }

                                    playStyle.Code = psProp.Value.GetString();
                                    break;

                                case "playlistcode":
                                    if(psProp.Value.ValueKind != JsonValueKind.String && psProp.Value.ValueKind != JsonValueKind.Null) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.PlayListCode)}", JsonValueKind.String, psProp.Value));
                                        error = true;
                                        break;
                                    }

                                    playStyle.PlayListCode = psProp.Value.GetString();
                                    break;

                                case "langcode":
                                    if(psProp.Value.ValueKind != JsonValueKind.String && psProp.Value.ValueKind != JsonValueKind.Null) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.LangCode)}", JsonValueKind.String, psProp.Value));
                                        error = true;
                                        break;
                                    }

                                    playStyle.LangCode = psProp.Value.GetString();
                                    break;

                                case "listorder":
                                    if(psProp.Value.ValueKind != JsonValueKind.Number) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.ListOrder)}", JsonValueKind.Number, psProp.Value));
                                        error = true;
                                        break;
                                    }

                                    playStyle.ListOrder = psProp.Value.GetDouble();
                                    break;
                                        
                                case "mods": {
                                    if(psProp.Value.ValueKind != JsonValueKind.Array) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.Mods)}", JsonValueKind.Array, psProp.Value));
                                        error = true;
                                        break;
                                    }

                                    var mods = new List<string>();
                                    int j = -1;
                                    foreach(var modItem in psProp.Value.EnumerateArray()) {
                                        j++;
                                        if(modItem.ValueKind != JsonValueKind.String) {
                                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.Mods)}[{j}]", JsonValueKind.String, modItem));
                                            error = true;
                                            continue;
                                        }

                                        mods.Add(modItem.GetString()!);
                                    }

                                    playStyle.Mods = mods.ToArray();
                                } break;

                                case "worldtype":
                                    if(psProp.Value.ValueKind != JsonValueKind.String && psProp.Value.ValueKind != JsonValueKind.Null) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.WorldType)}", JsonValueKind.String, psProp.Value));
                                        error = true;
                                        break;
                                    }

                                    playStyle.WorldType = psProp.Value.GetString();
                                    break;

                                case "worldconfig": {
                                    if(psProp.Value.ValueKind != JsonValueKind.Object) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.WorldConfig)}", JsonValueKind.Object, psProp.Value));
                                        error = true;
                                        playStyle.WorldConfig = JsonObject.FromJson("{}");
                                        break;
                                    }

                                    try {
                                        playStyle.WorldConfig = JsonObject.FromJson(psProp.Value.GetRawText());
                                    }
                                    catch(Exception ex) {
                                        errorCallback(new Errors.MalformedJson($"{nameof(ModWorldConfiguration.PlayStyles)}[{i}].{nameof(PlayStyle.WorldConfig)}", ex));
                                        error = true;
                                        playStyle.WorldConfig = JsonObject.FromJson("{}");
                                    }
                                } break;

                                default:
                                    errorCallback(new Errors.UnexpectedProperty($"{sourceStructure}${nameof(ModWorldConfiguration.PlayStyles)}[{i}]", psProp.Name, psProp.Value.GetRawText()));
                                    error = true;
                                    break;
                            }
                        }
                    }
                
                    worldConfig.PlayStyles = playstyles.ToArray();
                } break;

                case "worldconfigattributes": {
                    var worldConfigAttributes = new List<WorldConfigurationAttribute>();

                    int i = -1;
                    foreach(var element in prop.Value.EnumerateArray()) {
                        i++;
                        if(element.ValueKind != JsonValueKind.Object) {
                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}]", JsonValueKind.Object, element));
                            error = true;
                            continue;
                        }

                        var worldConfigAttribute = new WorldConfigurationAttribute();
                        foreach(var wcaProp in element.EnumerateObject()) {
                            switch(wcaProp.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture)) {
                                case "datatype": {
                                    if(wcaProp.Value.ValueKind != JsonValueKind.String) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.DataType)}", JsonValueKind.String, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    var dataTypeStr = wcaProp.Value.GetString()!;
                                    if (!Enum.TryParse(dataTypeStr, true, out worldConfigAttribute.DataType)) {
                                        errorCallback(new Errors.StringParsingFailure($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.DataType)}", nameof(EnumDataType), dataTypeStr));
                                        worldConfigAttribute.DataType = BROKEN_DATA_TYPE;
                                        error = true;
                                        break;
                                    }
                                } break;

                                case "category":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.String && wcaProp.Value.ValueKind != JsonValueKind.Null) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Category)}", JsonValueKind.String, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.Category = wcaProp.Value.GetString();
                                    break;

                                case "code":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.String && wcaProp.Value.ValueKind != JsonValueKind.Null) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Code)}", JsonValueKind.String, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.Code = wcaProp.Value.GetString();
                                    break;

                                case "min":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.Number) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Min)}", JsonValueKind.Number, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.Min = wcaProp.Value.GetDouble();
                                    break;

                                case "max":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.Number) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Max)}", JsonValueKind.Number, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.Max = wcaProp.Value.GetDouble();
                                    break;

                                case "step":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.Number) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Step)}", JsonValueKind.Number, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.Step = wcaProp.Value.GetDouble();
                                    break;

                                case "oncustomizescreen":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.True && wcaProp.Value.ValueKind != JsonValueKind.False) {
                                        errorCallback(new Errors.PrimitiveParsingFailure($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.OnCustomizeScreen)}", "boolean", prop.Value.GetRawText()));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.OnCustomizeScreen = wcaProp.Value.GetBoolean();
                                    break;

                                case "default":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.String && wcaProp.Value.ValueKind != JsonValueKind.Null) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Default)}", JsonValueKind.String, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.Default = wcaProp.Value.GetString();
                                    break;

                                case "values": {
                                    if(wcaProp.Value.ValueKind != JsonValueKind.Array) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Values)}", JsonValueKind.Array, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    var values = new List<string>();
                                    int j = -1;
                                    foreach(var modItem in wcaProp.Value.EnumerateArray()) {
                                        j++;
                                        if(modItem.ValueKind != JsonValueKind.String) {
                                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Values)}[{j}]", JsonValueKind.String, modItem));
                                            error = true;
                                            continue;
                                        }

                                        values.Add(modItem.GetString()!);
                                    }

                                    worldConfigAttribute.Values = values.ToArray();
                                } break;

                                case "names": {
                                    if(wcaProp.Value.ValueKind != JsonValueKind.Array) {
                                        errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Names)}", JsonValueKind.Array, wcaProp.Value));
                                        error = true;
                                        break;
                                    }

                                    var names = new List<string>();
                                    int j = -1;
                                    foreach(var modItem in wcaProp.Value.EnumerateArray()) {
                                        j++;
                                        if(modItem.ValueKind != JsonValueKind.String) {
                                            errorCallback(new Errors.UnexpectedJsonPropertyType($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.Names)}[{j}]", JsonValueKind.String, modItem));
                                            error = true;
                                            continue;
                                        }

                                        names.Add(modItem.GetString()!);
                                    }

                                    worldConfigAttribute.Names = names.ToArray();
                                } break;

                                case "onlyduringworldcreate":
                                    if(wcaProp.Value.ValueKind != JsonValueKind.True && wcaProp.Value.ValueKind != JsonValueKind.False) {
                                        errorCallback(new Errors.PrimitiveParsingFailure($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}].{nameof(WorldConfigurationAttribute.OnlyDuringWorldCreate)}", "boolean", prop.Value.GetRawText()));
                                        error = true;
                                        break;
                                    }

                                    worldConfigAttribute.OnlyDuringWorldCreate = wcaProp.Value.GetBoolean();
                                    break;

                                default:
                                    errorCallback(new Errors.UnexpectedProperty($"{nameof(ModWorldConfiguration.WorldConfigAttributes)}[{i}]", prop.Name, prop.Value.GetRawText()));
                                    error = true;
                                    break;
                            }
                        }

                        worldConfigAttributes.Add(worldConfigAttribute);
                    }

                    worldConfig.WorldConfigAttributes = worldConfigAttributes.ToArray();
                } break;


                default:
                    errorCallback(new Errors.UnexpectedProperty(sourceStructure, prop.Name, prop.Value.GetRawText()));
                    error = true;
                    break;
            }
        }

        return !error;
    }
}

