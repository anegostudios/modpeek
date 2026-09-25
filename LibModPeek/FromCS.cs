using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    // See tests for examples of the attributes we are trying to parse.
    /// <remarks> The ModInfo obtained from this function has not been validated! </remarks>
    public static bool TryExtractModInfoAndWorldConfigFromCs(byte[] bytes, out ModInfo? modInfo, out ModWorldConfiguration? worldConfig, SecurityRelevantCapabilities capabilities, Action<Errors.Error> errorCallback)
    {
        var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true); // no need to dispose here
        var sourceText = SourceText.From(reader, bytes.Length);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var compilationRoot = syntaxTree.GetCompilationUnitRoot();

        modInfo = new ModInfo() {
            Type = EnumModType.Code,
        };
        worldConfig = null;

        var error = false;
        var dependencies = new List<ModDependency>();
        foreach (var attrList in compilationRoot.AttributeLists) {
            foreach (var attribute in attrList.Attributes) {
                switch (attribute.Name.ToString()) {
                    case "ModInfo":
                    case nameof(ModInfoAttribute): {
                        if(attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) {
                            errorCallback(new Errors.MissingRequiredProperty(nameof(ModInfoAttribute), nameof(ModInfoAttribute.Name)));
                            error = true;
                            break;
                        }

                        var attrArgs = attribute.ArgumentList.Arguments;
                        
                        if(!TryParseString(attrArgs[0].Expression, out modInfo.Name)) {
                            errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Name), "string", attrArgs[0].Expression.ToString()));
                            error = true;
                        }

                        if (attrArgs.Count < 2) {
                            break;
                        }
                        

                        if (attrArgs[1].NameEquals == null) {
                            if (!TryParseString(attrArgs[1].Expression, out var modID)) {
                                errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.ModID), "string", attrArgs[1].Expression.ToString()));
                                error = true;
                            }
                            else {
                                modInfo.ModID = modID;
                            }
                        }

                        foreach (var arg in attrArgs) {
                            if (arg.NameEquals == null)  continue;

                            var propName = arg.NameEquals.Name.Identifier.ValueText;
                            var propValueExpr = arg.Expression;
                            switch (propName) {
                                case nameof(ModInfoAttribute.Version):
                                    if (!TryParseString(propValueExpr, out modInfo.Version)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Version), "string", propValueExpr.ToString()));
                                        modInfo.Version = BROKEN_VERSION;
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.NetworkVersion):
                                    if (!TryParseString(propValueExpr, out modInfo.NetworkVersion)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.NetworkVersion), "string", propValueExpr.ToString()));
                                        modInfo.NetworkVersion = BROKEN_VERSION;
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.Side): {
                                    if (!TryParseString(propValueExpr, out var sideStr)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Side), "string", propValueExpr.ToString()));
                                        error = true;
                                        break;
                                    }

                                    if (!Enum.TryParse(sideStr, true, out EnumAppSide side)) {
                                        errorCallback(new Errors.StringParsingFailure(nameof(ModInfoAttribute.Side), nameof(EnumAppSide), propValueExpr.ToString()));
                                        error = true;
                                        break;
                                    }

                                    modInfo.Side = side;
                                } break;

                                case nameof(ModInfoAttribute.RequiredOnClient): {
                                    if (!TryParseBoolean(propValueExpr, out var requiredOnClient)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.RequiredOnClient), "boolean", propValueExpr.ToString()));
                                        error = true;
                                        break;
                                    }

                                    modInfo.RequiredOnClient = requiredOnClient;
                                } break;

                                case nameof(ModInfoAttribute.RequiredOnServer): {
                                    if (!TryParseBoolean(propValueExpr, out var requiredOnServer)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.RequiredOnServer), "boolean", propValueExpr.ToString()));
                                        error = true;
                                        break;
                                    }

                                    modInfo.RequiredOnServer = requiredOnServer;
                                } break;

                                case nameof(ModInfoAttribute.Description):
                                    if (!TryParseString(propValueExpr, out modInfo.Description)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Description), "string", propValueExpr.ToString()));
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.Website):
                                    if (!TryParseString(propValueExpr, out modInfo.Website)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Website), "string", propValueExpr.ToString()));
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.IconPath):
                                    if (!TryParseString(propValueExpr, out modInfo.IconPath)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Website), "string", propValueExpr.ToString()));
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.WorldConfig):
                                    if (!TryParseString(propValueExpr, out var worldConfigString) || worldConfigString == null) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.WorldConfig), "string", propValueExpr.ToString()));
                                        error = true;
                                        break;
                                    }

                                    var worldConfigLocation = $"{nameof(ModInfoAttribute)}.{nameof(ModInfoAttribute.WorldConfig)}[@{propValueExpr.GetLocation()}]";
                                    try {
                                        var worldConfigJson = JsonDocument.Parse(worldConfigString, new() {
                                            AllowTrailingCommas = true,
                                        });
                                        error |= !TryParseWorldConfigFromJsonCaseInsensitive(worldConfigLocation, worldConfigJson, out worldConfig, errorCallback);
                                    }
                                    catch(Exception e) {
                                        errorCallback(new Errors.MalformedJson(worldConfigLocation, e));
                                        worldConfig = null;
                                        error = true;
                                        break;
                                    }

                                    break;

                                case nameof(ModInfoAttribute.Authors): {
                                    if (!TryParseArrayInitializer(propValueExpr, out var initializer)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Authors), "string array", propValueExpr.ToString()));
                                        error = true;
                                    }

                                    var authors = new List<string>();
                                    int j = 0;
                                    foreach (var elementExpression in initializer!.Expressions) {
                                        if (!TryParseString(elementExpression, out var author)) {
                                            errorCallback(new Errors.PrimitiveParsingFailure($"{nameof(ModInfoAttribute.Authors)}[{j}]", "string", elementExpression.ToString()));
                                        }
                                        else {
                                            authors.Add(author!);
                                        }
                                        j++;
                                    }
                                    modInfo.Authors = authors;
                                } break;

                                case nameof(ModInfoAttribute.Contributors): {
                                    if (!TryParseArrayInitializer(propValueExpr, out var initializer)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.Contributors), "string array", propValueExpr.ToString()));
                                        error = true;
                                    }

                                    var contributors = new List<string>();
                                    int j = 0;
                                    foreach (var elementExpression in initializer!.Expressions) {
                                        if (!TryParseString(elementExpression, out var contributor)) {
                                            errorCallback(new Errors.PrimitiveParsingFailure($"{nameof(ModInfoAttribute.Contributors)}[{j}]", "string", elementExpression.ToString()));
                                        }
                                        else {
                                            contributors.Add(contributor!);
                                        }
                                        j++;
                                    }
                                    modInfo.Contributors = contributors;
                                } break;

                                case nameof(ModInfoAttribute.CoreMod): {
                                    if (!TryParseBoolean(propValueExpr, out modInfo.CoreMod)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.CoreMod), "boolean", propValueExpr.ToString()));
                                        error = true;
                                        break;
                                    }
                                } break;

                                default:
                                    errorCallback(new Errors.UnexpectedProperty("modinfo.json", propName, propValueExpr.ToString()));
                                    error = true;
                                    break;
                            }
                        }

                    } break;

                    case "ModDependency":
                    case nameof(ModDependencyAttribute): {
                        if(attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) {
                            errorCallback(new Errors.MissingRequiredProperty($"{nameof(ModDependencyAttribute)}[@{attribute.GetLocation()}]", nameof(ModDependencyAttribute.ModID)));
                            error = true;
                            break;
                        }

                        var attrArgs = attribute.ArgumentList.Arguments;
                        
                        if(!TryParseString(attrArgs[0].Expression, out var depName)) {
                            errorCallback(new Errors.PrimitiveParsingFailure($"{nameof(ModDependencyAttribute)}[@{attribute.GetLocation()}]{nameof(ModDependencyAttribute.ModID)}", "string", attrArgs[0].Expression.ToString()));
                            error = true;
                        }

                        string? depVersion = null;
                        if(attrArgs.Count > 1 && !TryParseString(attrArgs[1].Expression, out depVersion)) {
                            errorCallback(new Errors.PrimitiveParsingFailure($"{nameof(ModDependencyAttribute)}[@{attribute.GetLocation()}]{nameof(ModDependencyAttribute.Version)}", "string", attrArgs[1].Expression.ToString()));
                            error = true;
                        }

                        if (depName != null) {
                            dependencies.Add(NewDependencyUnchecked(depName, depVersion));
                        }
                    } break;
                }
            }
        }

        var ok = false;

        if (modInfo == null) {
            errorCallback(new Errors.MissingAssemblyAttribute("ModInfo"));
            worldConfig = null;
            ok = false;
        }
        else {
            modInfo.Dependencies = dependencies;
            ok = !error;
        }

        //TODO(Rennorb) @perf: Use already loaded assemblies?
        var compilation = CSharpCompilation.Create(null, [ syntaxTree ], [ MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(IServerAPI).Assembly.Location) ]);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        ScanAssemblyForCapabilities<SourceTypeProvider, SourceMethodDeclarationProvider, SourceParameterProvider, SourceAttributeProvider, SourceAssemblyProvider, SourceModuleProvider, SourceImportProvider, SourceDeclaredTypeProvider>(capabilities, new(semanticModel));

        return ok;


        static bool TryParseString(ExpressionSyntax expression, out string? parsed)
        {
            parsed = null;
            if (expression is not LiteralExpressionSyntax literal) return false;
            switch (literal.Token.Kind()) {
                case SyntaxKind.MultiLineRawStringLiteralToken:
                case SyntaxKind.SingleLineRawStringLiteralToken:
                case SyntaxKind.StringLiteralToken:
                    parsed = literal.Token.ValueText;
                    return true;

                case SyntaxKind.NullLiteralExpression:
                    return true;

                default:
                    Console.Error.WriteLine(literal.Token.Kind());
                    return false;
            }
        }

        static bool TryParseBoolean(ExpressionSyntax expression, out bool parsed)
        {
            parsed = false;
            if (expression is not LiteralExpressionSyntax literal) return false;
            switch (literal.Token.Kind()) {
                case SyntaxKind.FalseKeyword:
                case SyntaxKind.FalseLiteralExpression:
                    parsed = false;
                    return true;

                case SyntaxKind.TrueKeyword:
                case SyntaxKind.TrueLiteralExpression:
                    parsed = true;
                    return true;

                default:
                    return false;
            }
        }

        static bool TryParseArrayInitializer(ExpressionSyntax expression, out InitializerExpressionSyntax? initializer)
        {
            initializer = null;
            switch (expression) {
                case ArrayCreationExpressionSyntax arrayExpression:
                    initializer = arrayExpression.Initializer;
                    return initializer != null;

                case ImplicitArrayCreationExpressionSyntax arrayExpression:
                    initializer = arrayExpression.Initializer;
                    return initializer != null;

                //case CollectionExpressionSyntax collectionExpression:
                //    initializer = arrayExpression.Initializer;
                //    return true;

                default:
                    return false;
            }
        }
    }

    readonly struct SourceTypeProvider(TypeSyntax type, SemanticModel sem) : TypeProvider {
        public readonly string Name
        {
            get
            {
                var _type = sem.GetTypeInfo(type);
                return _type.Type!.Name;
            }
        }

        public readonly string FullName
        {
            get
            {
                var _type = sem.GetTypeInfo(type);
                return _type.Type!.ToString() ?? "?";
            }
        }
    }

    readonly struct SourceMethodDeclarationProvider(MethodDeclarationSyntax method, SemanticModel sem) : MethodDeclarationProvider<SourceTypeProvider, SourceParameterProvider, SourceAttributeProvider> {
        public readonly string Name => method.Identifier.ToString();
        public readonly IEnumerable<SourceParameterProvider> Parameters
        {
            get
            {
                var _sem = sem;
                return method.ParameterList.Parameters.Select(p => new SourceParameterProvider(p, _sem));
            }
        }

        public readonly IEnumerable<SourceAttributeProvider> CustomAttributes
        {
            get
            {
                var _sem = sem;
                return method.AttributeLists.SelectMany(list => list.Attributes.Select(a => new SourceAttributeProvider(a, _sem)));
            }
        }

        public readonly bool IsStatic => method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        public readonly bool HasBody => method.Body != null || method.ExpressionBody != null;

        public readonly void DoBodyProcessing(SecurityRelevantCapabilities capabilities, in ReflectionTargetBuilder typeTarget, StringBuilder sb)
        {
            var walker = new MethodWalker(capabilities, sem, sb);
            walker.VisitMethodDeclaration(method);
        }

        class MethodWalker(SecurityRelevantCapabilities capabilities, SemanticModel sem, StringBuilder sb) : CSharpSyntaxWalker
        {
            public override void VisitInvocationExpression(InvocationExpressionSyntax invocation)
            {
                switch(invocation.Expression) {
                    case MemberAccessExpressionSyntax memberAccess:
                        var calledType = sem.GetTypeInfo(memberAccess.Expression);
                        var calledTypeString = calledType.Type!.ToString() ?? "?";
                        switch(calledTypeString) {
                            case "System.Activator":
                                capabilities.Capabilities |= Capabilities.Reflection;
                                break;

                            case "System.IO.Path":
                                switch(memberAccess.Name.ToString()) {
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

                            case "System.IO.UnmanagedMemoryAccessor":
                            case "System.IO.UnmanagedMemoryStream":
                                capabilities.Capabilities |= Capabilities.UnmanagedMemoryAccess;
                                break;

                            case "System.IO.Compression.ZipFile":
                            case "System.IO.Compression.ZipFileExtensions":
                                capabilities.Capabilities |= Capabilities.DirectFileAccess;
                                break;

                            default:
                                if(calledTypeString.StartsWith("System.Reflection")) {
                                    capabilities.Capabilities |= Capabilities.Reflection;
                                }
                                else if(calledTypeString.Contains("MemoryMappedFiles")) {
                                    capabilities.Capabilities |= Capabilities.DirectFileAccess;
                                }
                                else if(calledTypeString.StartsWith("System.Net")) {
                                    capabilities.Capabilities |= Capabilities.DirectNetworkAccess;
                                }
                                break;
                        }
                        break;
                }

                base.VisitInvocationExpression(invocation);
            }

            public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax newSyntax)
            {
                var type = sem.GetTypeInfo(newSyntax.Type);
                ProcessConstructor(type, newSyntax.ArgumentList);

                base.VisitObjectCreationExpression(newSyntax);
            }

            public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
            {
                var type = sem.GetTypeInfo(node);
                ProcessConstructor(type, node.ArgumentList);

                 base.VisitImplicitObjectCreationExpression(node);
            }

            void ProcessConstructor(in TypeInfo type, ArgumentListSyntax? argumentList)
            {
                var createdTypeString = type.Type!.ToString();
                switch(createdTypeString) {
                    case "System.IO.StreamReader":
                    case "System.IO.StreamWriter":
                        if(argumentList != null) {
                            var firstArg = argumentList.Arguments[0];
                            if(firstArg.Expression != null) {
                                var expressionType = sem.GetTypeInfo(firstArg.Expression);
                                var expressionTypeName = expressionType.Type!.ToString();
                                if(expressionTypeName == "System.String") {
                                    capabilities.Capabilities |= Capabilities.DirectFileAccess;
                                }
                            }
                            else { // might be reordered with 'name: expression' semantics
                                foreach(var arg in argumentList.Arguments) {
                                    if(arg.NameColon == null) break; // still has to be the "first" arg, so if we see another expression arg we can break (named args must be after expression args)

                                    if(arg.NameColon.Name.Identifier.ToString() == "path") {
                                        capabilities.Capabilities |= Capabilities.DirectFileAccess;
                                        break;
                                    }
                                }
                            }
                        }
                        break;

                    case "System.IO.DriveInfo":
                    case "System.IO.FileInfo":
                    case "System.IO.FileStream":
                    case "System.IO.FileSystemInfo":
                    case "System.IO.FileSystemWatcher":
                    case "System.IO.RandomAccess":
                        capabilities.Capabilities |= Capabilities.DirectFileAccess;
                        break;

                    case "System.IO.UnmanagedMemoryAccessor":
                    case "System.IO.UnmanagedMemoryStream":
                        capabilities.Capabilities |= Capabilities.UnmanagedMemoryAccess;
                        break;

                    case "System.IO.Compression.ZipFile":
                        capabilities.Capabilities |= Capabilities.DirectFileAccess;
                        break;
                }
            }
        }
    }

    readonly struct SourceParameterProvider(ParameterSyntax param, SemanticModel sem) : ParameterProvider<SourceTypeProvider> {
        public string             Name => param.Identifier.ToString();
        public SourceTypeProvider Type => new(param.Type, sem);
    }

    readonly struct SourceAttributeProvider(AttributeSyntax attr, SemanticModel sem) : AttributeProvider<SourceTypeProvider> {
        public readonly SourceTypeProvider Type => new(attr.Name, sem);

        public void CollectPartialHarmonyPatchAttributeParameters(ref ReflectionTargetBuilder target)
        {
            if(attr.ArgumentList == null) return;

            //TODO(Rennorb) @completeness: Implement the argument parser for this.
            // This is going to be really annoying (0-4 args, optional args, named args), and it does not influence the actual detection, so i opted to skip it for now.

            target.Type = ""; // other logic relies on the type being set at some point.
        }
    }

    readonly struct SourceAssemblyProvider(SemanticModel sem) : AssemblyProvider<SourceTypeProvider, SourceModuleProvider, SourceImportProvider, SourceDeclaredTypeProvider, SourceMethodDeclarationProvider, SourceParameterProvider, SourceAttributeProvider> {
        public readonly IEnumerable<SourceModuleProvider> Modules => [ new(sem, sem.SyntaxTree.GetCompilationUnitRoot()) ];
    }

    struct SourceModuleProvider(SemanticModel sem, CompilationUnitSyntax module) :  ModuleProvider<SourceTypeProvider, SourceImportProvider, SourceDeclaredTypeProvider, SourceMethodDeclarationProvider, SourceParameterProvider, SourceAttributeProvider> {
        public readonly IEnumerable<SourceImportProvider> AssemblyReferences => module.Usings.Select(r => new SourceImportProvider(r));

        public readonly IEnumerable<SourceDeclaredTypeProvider> CodeBearingTypeDeclarations
        {
            get
            {
                var _sem = sem;
                return module.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => t.Kind() != SyntaxKind.EnumDeclaration).Select(t => new SourceDeclaredTypeProvider(t, _sem));
            }
        }
    }

    static string? FoldName(StringBuilder sb, TypeSyntax type, bool skipLeftmost, bool prepend)
    {
        switch(type) {
            case QualifiedNameSyntax qname: {
                string? leftmost;
                if(prepend) {
                    FoldName(sb, qname.Right, skipLeftmost: false, prepend: true);
                    sb.Insert(0, '.');
                    leftmost = FoldName(sb, qname.Left, skipLeftmost, prepend: true);
                }
                else {
                    leftmost = FoldName(sb, qname.Left, skipLeftmost, prepend: false);
                    sb.Append('.');
                    FoldName(sb, qname.Right, skipLeftmost: false, prepend: false);
                }
                return leftmost;
            }

            case SimpleNameSyntax sname: {
                var leftmost = sname.Identifier.ToString();
                if(!skipLeftmost) {
                    if(prepend) sb.Insert(0, leftmost);
                    else        sb.Append(leftmost);
                }
                return leftmost;
            }

            default:
                Debug.Assert(false, "FoldName called with incorrect type "+type.GetType());
                return null;
        }
    }

    readonly struct SourceImportProvider(UsingDirectiveSyntax _ref) : ImportProvider {
        public readonly string FullName => _ref.Name?.ToString() ?? "";
    }

    readonly struct SourceDeclaredTypeProvider(TypeDeclarationSyntax type, SemanticModel sem) :  DeclaredTypeProvider<SourceTypeProvider, SourceMethodDeclarationProvider, SourceParameterProvider, SourceAttributeProvider> {
        public IEnumerable<SourceAttributeProvider> CustomAttributes
        {
            get
            {
                var _sem = sem;
                return type.AttributeLists.SelectMany(list => list.Attributes.Select(a => new SourceAttributeProvider(a, _sem)));
            }
        }

        public IEnumerable<SourceMethodDeclarationProvider> Methods
        {
            get
            {
                var _sem = sem;
                return type.Members.Where(m => m.Kind() == SyntaxKind.MethodDeclaration).Select(m => new SourceMethodDeclarationProvider((MethodDeclarationSyntax)m, _sem));
            }
        }
    }
}
