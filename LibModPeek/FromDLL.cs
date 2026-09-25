using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;
using System.Text.Json;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    /// <remarks> The ModInfo obtained from this function has not been validated! </remarks>
    public static bool TryExtractModInfoAndWorldConfigFromDll(byte[] bytes, out ModInfo? modInfo, out ModWorldConfiguration? worldConfig, SecurityRelevantCapabilities capabilities, Action<Errors.Error> errorCallback)
    {
        //TODO(Rennorb) @correctness: Improve distinction between missing and null. :MissingVsNull
        static CustomAttributeNamedArgument GetProperty(CustomAttribute customAttribute, string propName)
        {
            return customAttribute.Properties.SingleOrDefault(property => property.Name == propName);
        }

        static T GetPropertyValue<T>(CustomAttribute customAttribute, string propName)
        {
            return (T) GetProperty(customAttribute, propName).Argument.Value;
        }

        static T[]? GetPropertyValueArray<T>(CustomAttribute customAttribute, string propName)
        {
            return (GetProperty(customAttribute, propName).Argument.Value as CustomAttributeArgument[])?.Select(item => (T)item.Value).ToArray();
        }

        var assemblyDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes)); // no need to dispose here

        var modInfoAttr = assemblyDefinition.CustomAttributes.SingleOrDefault(attribute => attribute.AttributeType.Name == nameof(ModInfoAttribute));
        if (modInfoAttr == null) {
            errorCallback(new Errors.MissingAssemblyAttribute(nameof(ModInfoAttribute)));
            modInfo = null;
            worldConfig = null;
            return false;
        }

        var name = modInfoAttr.ConstructorArguments[0].Value as string;
        var modID = modInfoAttr.ConstructorArguments[1].Value as string;

        if (!Enum.TryParse(GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.Side)), true, out EnumAppSide side)) {
            side = EnumAppSide.Universal;
        }

        var modDependencies = assemblyDefinition.CustomAttributes
            .Where(attr => attr.AttributeType.Name == nameof(ModDependencyAttribute))
            .Select(attr => NewDependencyUnchecked(attr.ConstructorArguments[0].Value as string, attr.ConstructorArguments[1].Value as string))
            .ToList();

        modInfo = new ModInfo(
            EnumModType.Code, name, modID,
            GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.Version)),
            GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.Description)),
            GetPropertyValueArray<string>(modInfoAttr, nameof(ModInfoAttribute.Authors)),
            GetPropertyValueArray<string>(modInfoAttr, nameof(ModInfoAttribute.Contributors)),
            GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.Website)),
            side,
            GetPropertyValue<bool?>(modInfoAttr, nameof(ModInfoAttribute.RequiredOnClient)).GetValueOrDefault(true),
            GetPropertyValue<bool?>(modInfoAttr, nameof(ModInfoAttribute.RequiredOnServer)).GetValueOrDefault(true),
            modDependencies
        ) {
            NetworkVersion = GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.NetworkVersion)),
            IconPath = GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.IconPath)),
        };

        bool ok;

        var worldConfigString = GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.WorldConfig));
        if (worldConfigString != null) { // :MissingVsNull
            var worldConfigLocation = $"{nameof(ModInfoAttribute)}.{nameof(ModInfoAttribute.WorldConfig)}";
            try {
                var worldConfigJson = JsonDocument.Parse(worldConfigString, new() {
                    AllowTrailingCommas = true,
                });
                ok = TryParseWorldConfigFromJsonCaseInsensitive(worldConfigLocation, worldConfigJson, out worldConfig, errorCallback);
            }
            catch(Exception e) {
                errorCallback(new Errors.MalformedJson(worldConfigLocation, e));
                worldConfig = null;
                ok = false;
            }
        }
        else {
            worldConfig = null;
            ok = true;
        }

        ScanAssemblyForCapabilities<MonoTypeProvider, MonoMethodDeclarationProvider, MonoParameterProvider, MonoAttributeProvider, MonoAssemblyProvider, MonoModuleProvider, MonoImportProvider, MonoDeclaredTypeProvider>(capabilities, new(assemblyDefinition));

        return ok;
    }


    readonly struct MonoTypeProvider(TypeReference type) : TypeProvider {
        public readonly string Name => type.Name;
        public readonly string FullName => type.FullName;
    }

    readonly struct MonoMethodDeclarationProvider(MethodDefinition method) : MethodDeclarationProvider<MonoTypeProvider, MonoParameterProvider, MonoAttributeProvider> {
        public readonly string                             Name             => method.Name;
        public readonly IEnumerable<MonoParameterProvider> Parameters       => method.Parameters.Select(p => new MonoParameterProvider(p));
        public readonly IEnumerable<MonoAttributeProvider> CustomAttributes => method.CustomAttributes.Select(a => new MonoAttributeProvider(a));
        public readonly bool                               IsStatic         => method.IsStatic;
        public readonly bool                               HasBody          => method.HasBody;

        public readonly void DoBodyProcessing(SecurityRelevantCapabilities capabilities, in ReflectionTargetBuilder typeTarget, StringBuilder sb)
        {
            foreach(var instruction in method.Body.Instructions) {
                if(instruction.OpCode.Code == Code.Call) {
                    var calledMethod = (MethodReference)instruction.Operand;
                    var declaringType = calledMethod.DeclaringType.FullName;
                    switch(declaringType) {
                        case "System.Activator":
                            capabilities.Capabilities |= Capabilities.Reflection;
                            break;

                        case "System.IO.Path":
                            switch(calledMethod.Name) {
                                case "Exists":
                                case "GetFullPath":
                                case "GetRandomFileName":
                                case "GetTempFileName":
                                    capabilities.Capabilities |= Capabilities.DirectFileAccess;
                                    break;
                            }
                            break;

                        case "System.IO.Directory":
                        case "System.IO.DriveInfo":
                        case "System.IO.File":
                        case "System.IO.FileInfo":
                        case "System.IO.FileStream":
                        case "System.IO.FileSystemInfo":
                        case "System.IO.FileSystemWatcher":
                        case "System.IO.RandomAccess":
                            capabilities.Capabilities |= Capabilities.DirectFileAccess;
                            break;

                        case "System.IO.StreamReader":
                        case "System.IO.StreamWriter":
                            if(calledMethod.Name == ".ctor" && calledMethod.HasParameters && calledMethod.Parameters[0].ParameterType.FullName == "System.String") {
                                capabilities.Capabilities |= Capabilities.DirectFileAccess;
                            }
                            break;

                        case "System.IO.UnmanagedMemoryAccessor":
                        case "System.IO.UnmanagedMemoryStream":
                            capabilities.Capabilities |= Capabilities.UnmanagedMemoryAccess;
                            break;

                        case "System.IO.Compression.ZipFile":
                        case "System.IO.Compression.ZipFileExtensions":
                            capabilities.Capabilities |= Capabilities.DirectFileAccess;
                            break;

                        default:
                            if(declaringType.StartsWith("System.Reflection")) {
                                capabilities.Capabilities |= Capabilities.Reflection;
                            }
                            else if(declaringType.StartsWith("System.IO.MemoryMappedFiles")) {
                                capabilities.Capabilities |= Capabilities.DirectFileAccess;
                            }
                            else if(declaringType.StartsWith("System.Net")) {
                                capabilities.Capabilities |= Capabilities.DirectNetworkAccess;
                            }
                            break;
                    }
                }
            }
        }
    }

    readonly struct MonoParameterProvider(ParameterDefinition param) : ParameterProvider<MonoTypeProvider>
    {
        public string           Name => param.Name;
        public MonoTypeProvider Type => new(param.ParameterType);
    }

    readonly struct MonoAttributeProvider(CustomAttribute attr) : AttributeProvider<MonoTypeProvider>
    {
        public readonly MonoTypeProvider            Type                 => new(attr.AttributeType);

        public void CollectPartialHarmonyPatchAttributeParameters(ref ReflectionTargetBuilder target)
        {
            // [HarmonyPatch(Type)]
            // [HarmonyPatch(Type, Type[])]
            // [HarmonyPatch(String)]
            // [HarmonyPatch(String, PropertyMethod)]
            // [HarmonyPatch(Type[])]
            // [HarmonyPatch(Type, String, Type[])]

            const string TYPE_NAME_FULL__TYPE   = "System.Type";
            const string TYPE_NAME_FULL__STRING = "System.String";
            const string TYPE_NAME__METHOD_TYPE = "MethodType";

            var args = attr.ConstructorArguments;
            switch(args.Count) {
                case 0: break; // can happen on the class attribute if the method itself specifies all the data
                case 1: {
                    var type0 = args[0].Type;
                    if(type0.FullName == TYPE_NAME_FULL__TYPE) {
                        target.Type = args[0].Value.ToString();
                    }
                    else if(type0.FullName == TYPE_NAME_FULL__STRING) {
                        target.Method = (string)args[0].Value;
                    }
                    else if(type0.IsArray) {
                        // argument types
                    }
                    //else if(type0.Name == TYPE_NAME__METHOD_TYPE) {
                    //    // dont care
                    //}
                    break;
                }

                case 2: {
                    var type0 = args[0].Type;
                    var type1 = args[1].Type;
                    if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.IsArray) {
                        target.Type = args[0].Value.ToString();
                        target.Generics = args[1].Value;
                    }
                    if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.Name == TYPE_NAME__METHOD_TYPE) {
                        target.Type = args[0].Value.ToString();
                        // args[1] = PropertyMethod.Setter, but lets not care for now
                    }
                    if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.FullName == TYPE_NAME_FULL__STRING) {
                        target.Type   = args[0].Value.ToString();
                        target.Method = (string)args[1].Value;
                    }
                    else if(type0.FullName == TYPE_NAME_FULL__STRING && type1.IsArray) {
                        target.Method = (string)args[0].Value;
                        target.Generics = args[1].Value;
                    }
                    else if(type0.FullName == TYPE_NAME_FULL__STRING && type1.Name == TYPE_NAME__METHOD_TYPE) {
                        target.Method = (string)args[0].Value;
                        // args[1] = PropertyMethod.Setter, but lets not care for now
                    }
                    else if(type0.Name == TYPE_NAME__METHOD_TYPE) {
                        // args[0] = PropertyMethod.Setter, but lets not care for now
                        target.Generics = args[1].Value;
                    }
                    //else if(type0.IsArray && type1.IsArray) {
                    //    // argument types
                    //    // variatons
                    //}
                    break;
                }

                case 3: {
                    var type0 = args[0].Type;
                    var type1 = args[1].Type;
                    var type2 = args[2].Type;
                    if(type0.FullName == TYPE_NAME_FULL__STRING && type1.FullName == TYPE_NAME_FULL__STRING && type2.IsArray) {
                        target.Type = (string)args[0].Value;
                        target.Method = (string)args[1].Value;
                        target.Generics = args[2].Value;
                    }
                    if(type0.FullName == TYPE_NAME_FULL__STRING && type1.IsArray && type2.IsArray) {
                        target.Method = (string)args[0].Value;
                        target.Generics = args[1].Value;
                        // args[2] = ArgumentType[] argumentVariations, but lets not care for now
                    }
                    if(type0.FullName == TYPE_NAME_FULL__STRING && type2.Name == TYPE_NAME__METHOD_TYPE) {
                        target.Type = (string)args[0].Value;
                        target.Method = (string)args[1].Value;
                        // args[2] = PropertyMethod.Setter, but lets not care for now
                    }
                    if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.FullName == TYPE_NAME__METHOD_TYPE && type2.IsArray) {
                        target.Type = args[0].Value.ToString();
                        // args[1] = PropertyMethod.Setter, but lets not care for now
                        // args[2] = ArgumentType[] argumentVariations, but lets not care for now
                    }
                    if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.FullName == TYPE_NAME_FULL__STRING && type2.Name == TYPE_NAME__METHOD_TYPE) {
                        target.Type = args[0].Value.ToString();
                        target.Method = (string)args[1].Value;
                        // args[2] = PropertyMethod.Setter, but lets not care for now
                    }
                    if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.FullName == TYPE_NAME_FULL__STRING && type2.IsArray) {
                        target.Type = args[0].Value.ToString();
                        target.Method = (string)args[1].Value;
                        // args[2] = ArgumentType[] argumentVariations, but lets not care for now
                    }
                    else if(type0.Name == TYPE_NAME__METHOD_TYPE) {
                        // args[0] = PropertyMethod.Setter, but lets not care for now
                        target.Generics = args[1].Value;
                        // args[2] = ArgumentType[] argumentVariations, but lets not care for now
                    }
                    break;
                }

                case 4: {
                    var type0 = args[0].Type;
                    var type1 = args[1].Type;
                    var type2 = args[2].Type;
                    var type3 = args[3].Type;
                    if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.Name == TYPE_NAME__METHOD_TYPE && type2.IsArray && type3.IsArray) {
                        target.Type = args[0].Value.ToString();
                    }
                    else if(type0.FullName == TYPE_NAME_FULL__TYPE && type1.FullName == TYPE_NAME_FULL__STRING && type2.IsArray && type3.IsArray) {
                        target.Type = args[0].Value.ToString();
                        target.Method = (string)args[1].Value;
                    }
                    break;
                }
            }
        }
    }

    readonly struct MonoAssemblyProvider(AssemblyDefinition ass) : AssemblyProvider<MonoTypeProvider, MonoModuleProvider, MonoImportProvider, MonoDeclaredTypeProvider, MonoMethodDeclarationProvider, MonoParameterProvider, MonoAttributeProvider> {
        public readonly IEnumerable<MonoModuleProvider> Modules => ass.Modules.Select(m => new MonoModuleProvider(m));
    }

    readonly struct MonoModuleProvider(ModuleDefinition module) :  ModuleProvider<MonoTypeProvider, MonoImportProvider, MonoDeclaredTypeProvider, MonoMethodDeclarationProvider, MonoParameterProvider, MonoAttributeProvider> {
        public readonly IEnumerable<MonoImportProvider>       AssemblyReferences          => module.AssemblyReferences.Select(r => new MonoImportProvider(r));
        public readonly IEnumerable<MonoDeclaredTypeProvider> CodeBearingTypeDeclarations => module.Types.Where(t => !t.IsEnum).Select(t => new MonoDeclaredTypeProvider(t));
    }

    readonly struct MonoImportProvider(AssemblyNameReference _ref) : ImportProvider {
        public readonly string FullName => _ref.FullName;
    }

    readonly struct MonoDeclaredTypeProvider(TypeDefinition type) :  DeclaredTypeProvider<MonoTypeProvider, MonoMethodDeclarationProvider, MonoParameterProvider, MonoAttributeProvider> {
        public IEnumerable<MonoAttributeProvider>         CustomAttributes => type.CustomAttributes.Select(a => new MonoAttributeProvider(a));
        public IEnumerable<MonoMethodDeclarationProvider> Methods          => type.Methods.Select(m => new MonoMethodDeclarationProvider(m));
    }
}
