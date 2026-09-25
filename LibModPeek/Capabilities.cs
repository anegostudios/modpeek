using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

//NOTE(Rennorb): Values must not be changed, used when reading back info.
[Flags]
public enum Capabilities {
    Reflection            = 1 << 0,
    LoadLibrary           = 1 << 1,
    DirectFileAccess      = 1 << 2,
    DirectNetworkAccess   = 1 << 3,
    Win32Access           = 1 << 4,
    UnmanagedMemoryAccess = 1 << 5,
}

public class SecurityRelevantCapabilities {
    public Capabilities    Capabilities = default;
    public bool            UsesHarmony  = false;
    public HashSet<string> Reflections  = new();
}


struct ReflectionTargetBuilder {
    public string? Type;
    public string? Method;
    public object? Generics;

    public static ReflectionTargetBuilder Merge(in ReflectionTargetBuilder _base, in ReflectionTargetBuilder overlay) => new() {
        Type     = overlay.Type     ?? _base.Type    ,
        Method   = overlay.Method   ?? _base.Method  ,
        Generics = overlay.Generics ?? _base.Generics,
    };
}

partial class ModPeek {
    static void ScanAssemblyForCapabilities<T, M, P, A, AS, MO, I, DT>(SecurityRelevantCapabilities capabilities, AS assemblyDefinition)
        where T : TypeProvider where M : MethodDeclarationProvider<T, P, A> where P : ParameterProvider<T> where A : AttributeProvider<T>  where AS : AssemblyProvider<T, MO, I, DT, M, P, A> where MO : ModuleProvider<T, I, DT, M, P, A> where I : ImportProvider where DT : DeclaredTypeProvider<T, M, P, A>
    {
        foreach(var module in assemblyDefinition.Modules) {
            foreach(var dependency in module.AssemblyReferences) {
                ProcessImportedNamespace(capabilities, dependency.FullName);
            }
        }

        var sb = new StringBuilder(512);

        foreach(var module in assemblyDefinition.Modules) {
            foreach(var type in module.CodeBearingTypeDeclarations) {
                var typeTarget = new ReflectionTargetBuilder();

                foreach(var typeAttr in type.CustomAttributes) {
                    switch(StripAttributeSuffix(typeAttr.Type.Name)) {
                        case "HarmonyPatch":
                            capabilities.Capabilities |= Capabilities.Reflection;
                            capabilities.UsesHarmony = true;

                            typeAttr.CollectPartialHarmonyPatchAttributeParameters(ref typeTarget);
                            break;
                    }
                }


                foreach(var method in type.Methods) {
                    if(method.IsStatic) { // harmony patches are always static
                        ScanMethodForHarmony<T, M, P, A>(capabilities, in typeTarget, sb, method);
                    }

                    if(method.HasBody) {
                        method.DoBodyProcessing(capabilities, in typeTarget, sb);
                    }
                }
            }
        }
    }

    static void ProcessImportedNamespace(SecurityRelevantCapabilities capabilities, ReadOnlySpan<char> _namespace)
    {
        if(_namespace.Contains("HarmonyLib", StringComparison.Ordinal)) {
            capabilities.Capabilities |= Capabilities.Reflection;
            capabilities.UsesHarmony = true;
        }
        if(_namespace.Contains("MonoMod", StringComparison.Ordinal)) {
            capabilities.Capabilities |= Capabilities.Reflection;
        }
        if(_namespace.Contains("System.Reflection", StringComparison.Ordinal)) {
            capabilities.Capabilities |= Capabilities.Reflection;
        }
        if(_namespace.Contains("System.Net", StringComparison.Ordinal)) {
            capabilities.Capabilities |= Capabilities.DirectNetworkAccess;
        }
        if(_namespace.Contains("System.Windows", StringComparison.Ordinal)) {
            capabilities.Capabilities |= Capabilities.Win32Access;
        }
    }

    static void ScanMethodForHarmony<T, M, P, A>(SecurityRelevantCapabilities capabilities, in ReflectionTargetBuilder typeTarget, StringBuilder sb, M method)
        where T : TypeProvider where M : MethodDeclarationProvider<T, P, A> where P : ParameterProvider<T> where A : AttributeProvider<T> 
    {
        var methodMightBeHarmonyPatch = false;
        var methodIsHarmonyPatch = false;
        var methodMightBeHarmonyTranspiler = false;
        var methodIsHarmonyTranspiler = false;

        var methodTarget = new ReflectionTargetBuilder();

        foreach(var methodAttr in method.CustomAttributes) {
            switch(StripAttributeSuffix(methodAttr.Type.Name)) {
                case "HarmonyPatch":
                    methodIsHarmonyPatch = true;
                    methodAttr.CollectPartialHarmonyPatchAttributeParameters(ref methodTarget);
                    break;

                case "HarmonyPrefix":
                case "HarmonyPostfix":
                    methodIsHarmonyPatch = true;
                    break;

                case "HarmonyTranspiler":
                    methodIsHarmonyTranspiler = true;
                    break;
            }
        }

        switch(method.Name) {
            case "Prefix":
            case "Postfix":
                methodMightBeHarmonyPatch = true;
                break;

            case "Transpiler":
                methodMightBeHarmonyTranspiler = true;
                break;
        }

        if(methodMightBeHarmonyPatch && typeTarget.Type != null) {
            methodIsHarmonyPatch = true;
        }

        if(methodMightBeHarmonyTranspiler && typeTarget.Type != null) {
            methodIsHarmonyTranspiler = true;
        }

        if(methodIsHarmonyPatch) {
            var mergedTarget = ReflectionTargetBuilder.Merge(_base: in typeTarget, overlay: in methodTarget);
            sb.Clear();
            FoldHarmonyPatchMethodIntoTypeString<T, M, P, A>(sb, mergedTarget, method);
            capabilities.Reflections.Add(sb.ToString());
        }

        if(methodIsHarmonyTranspiler) {
            var mergedTarget = ReflectionTargetBuilder.Merge(_base: in typeTarget, overlay: in methodTarget);
            sb.Clear();
            FoldHarmonyTranspilerMethodIntoTypeString<T, M, P, A>(sb, mergedTarget, method);
            capabilities.Reflections.Add(sb.ToString());
        }
    }

    static void FoldHarmonyPatchMethodIntoTypeString<T, M, P, A>(StringBuilder sb, ReflectionTargetBuilder target, M method)
        where T : TypeProvider where M : MethodDeclarationProvider<T, P, A> where P : ParameterProvider<T> where A : AttributeProvider<T> 
    {
        sb.Append(target.Type).Append('.').Append(target.Method).Append('(');
        int i = 0;
        foreach(var param in method.Parameters) {
            switch(param.Name) {
                case "__instance":
                case "__result":
                case "__state":
                case "__originalMethod":
                    break;

                default:
                    if(param.Name.StartsWith("___")) continue; // instance fields

                    if(i > 0) sb.Append(", ");
                    sb.Append(param.Type.Name);

                    i++;
                    break;
            }
        }
        sb.Append(')');
    }

    static void FoldHarmonyTranspilerMethodIntoTypeString<T, M, P, A>(StringBuilder sb, ReflectionTargetBuilder target, M method)
        where T : TypeProvider where M : MethodDeclarationProvider<T, P, A> where P : ParameterProvider<T> where A : AttributeProvider<T> 
    {
        sb.Append(target.Type).Append('.').Append(target.Method).Append('(');
        int i = 0;
        foreach(var param in method.Parameters) {
            switch(param.Name) {
                case "__instance":
                case "__result":
                case "__state":
                case "__originalMethod":
                    // do nothing
                    break;

                default:
                    if(param.Name.StartsWith("___")) continue; // instance fields
                    switch(param.Type.Name) {
                        case "MethodBase":
                        case "ILGenerator":
                        //case "IEnumerable<CodeInstruction>": //TODO(Rennorb) @completeness
                            // do nothing
                            break;

                        default:
                            if(i > 0) sb.Append(", ");
                            sb.Append(param.Type.Name);

                            i++;
                            break;
                    }
                    break;
            }
        }
        sb.Append(')');
    }

    interface TypeProvider {
        public string Name { get; }
        public string FullName { get; }
    }

    interface MethodDeclarationProvider<T, P, A> where T : TypeProvider where P : ParameterProvider<T> where A : AttributeProvider<T>   {
        public string         Name { get; }
        public IEnumerable<P> Parameters { get; }
        public IEnumerable<A> CustomAttributes { get; }
        public bool           IsStatic { get; }
        public bool           HasBody { get; }

        public void           DoBodyProcessing(SecurityRelevantCapabilities capabilities, in ReflectionTargetBuilder typeTarget, StringBuilder sb);
    }

    interface ParameterProvider<T> where T : TypeProvider {
        public string Name { get; }
        public T      Type { get; }
    }

    interface AttributeProvider<T> where T : TypeProvider {
        public T        Type { get; }

        public void CollectPartialHarmonyPatchAttributeParameters(ref ReflectionTargetBuilder target);
    }

    interface AssemblyProvider<T, MO, I, DT, M, P, A> where T : TypeProvider where MO : ModuleProvider<T, I, DT, M, P, A> where I : ImportProvider where M : MethodDeclarationProvider<T, P, A> where P : ParameterProvider<T> where A : AttributeProvider<T>  {
        public IEnumerable<MO> Modules { get; }
    }

    interface ModuleProvider<T, I, DT, M, P, A> where T : TypeProvider where I : ImportProvider where M : MethodDeclarationProvider<T, P, A> where P : ParameterProvider<T> where A : AttributeProvider<T> {
        public IEnumerable<I>  AssemblyReferences { get; }
        public IEnumerable<DT> CodeBearingTypeDeclarations { get; }
    }

    interface ImportProvider {
        public string FullName { get; }
    }

    interface DeclaredTypeProvider<T, M, P, A> where T : TypeProvider where P : ParameterProvider<T> where A : AttributeProvider<T>  {
        public IEnumerable<A> CustomAttributes { get; }
        public IEnumerable<M> Methods { get; }
    }

    static ReadOnlySpan<char> StripAttributeSuffix(ReadOnlySpan<char> str)
        => str.EndsWith("Attribute", StringComparison.Ordinal) ? str[..(str.Length - 9)] : str;
}
