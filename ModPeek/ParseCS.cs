using System.Text;
using Vintagestory.API.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VintageStory.ModPeek;

static partial class ModPeek
{

    /*
    [assembly: ModInfo("StepUp", Version = "1.2.0", Side = "Client",
        Description = "Doubles players' step height to allow stepping up full blocks",
        Website = "https://www.vintagestory.at/forums/topic/3349-stepup-v120/",
        Authors = new []{ "copygirl" }
    )]
    */

    //NOTE(Rennorb): This is still not perfect and will get tripped up on things like additional assembly attributes in comments.
    // The only way to properly do this parsing would be to tokenize the file and do it based on the ast representation.
    // Nevertheless, this is a lot better than the previous version.
    static bool TryGetCsInfo(byte[] bytes, out ModInfo? modInfo, Action<Error> errorCallback)
    {
        var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true); // no need to dispose here
        var sourceText = SourceText.From(reader, bytes.Length);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var compilationRoot = syntaxTree.GetCompilationUnitRoot();

        modInfo = new ModInfo() {
            Type = EnumModType.Code,
        };

        var error = false;
        var dependencies = new List<ModDependency>();
        foreach (var attrList in compilationRoot.AttributeLists) {
            foreach (var attribute in attrList.Attributes) {
                switch (attribute.Name.ToString()) {
                    case "ModInfo":
                    case nameof(ModInfoAttribute): {
                        if(attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) {
                            errorCallback(Errors.MissingRequiredProperty(nameof(ModInfoAttribute), nameof(ModInfoAttribute.Name)));
                            error = true;
                            break;
                        }

                        var attrArgs = attribute.ArgumentList.Arguments;
                        
                        if(!TryParseString(attrArgs[0].Expression, out modInfo.Name)) {
                            errorCallback(Errors.PrimitiveParsingFailure(attrArgs[0].Expression.ToString(), "string", nameof(ModInfoAttribute.Name)));
                            error = true;
                        }

                        if (attrArgs.Count < 2) {
                            break;
                        }
                        

                        if (attrArgs[1].NameEquals == null) {
                            if (!TryParseString(attrArgs[1].Expression, out var modID)) {
                                errorCallback(Errors.PrimitiveParsingFailure(attrArgs[1].Expression.ToString(), "string", nameof(ModInfoAttribute.ModID)));
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
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string", nameof(ModInfoAttribute.Version)));
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.NetworkVersion):
                                    if (!TryParseString(propValueExpr, out modInfo.NetworkVersion)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string", nameof(ModInfoAttribute.NetworkVersion)));
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.Side): {
                                    if (!TryParseString(propValueExpr, out var sideStr)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string", nameof(ModInfoAttribute.Side)));
                                        error = true;
                                        break;
                                    }

                                    if (!Enum.TryParse(sideStr, true, out EnumAppSide side)) {
                                        errorCallback(Errors.StringParsingFailure(propValueExpr.ToString(), nameof(EnumAppSide), nameof(ModInfoAttribute.Side)));
                                        error = true;
                                        break;
                                    }

                                    modInfo.Side = side;
                                } break;

                                case nameof(ModInfoAttribute.RequiredOnClient): {
                                    if (!TryParseBoolean(propValueExpr, out var requiredOnClient)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "boolean", nameof(ModInfoAttribute.RequiredOnClient)));
                                        error = true;
                                        break;
                                    }

                                    modInfo.RequiredOnClient = requiredOnClient;
                                } break;

                                case nameof(ModInfoAttribute.RequiredOnServer): {
                                    if (!TryParseBoolean(propValueExpr, out var requiredOnServer)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "boolean", nameof(ModInfoAttribute.RequiredOnServer)));
                                        error = true;
                                        break;
                                    }

                                    modInfo.RequiredOnServer = requiredOnServer;
                                } break;

                                case nameof(ModInfoAttribute.Description):
                                    if (!TryParseString(propValueExpr, out modInfo.Description)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string", nameof(ModInfoAttribute.Description)));
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.Website):
                                    if (!TryParseString(propValueExpr, out modInfo.Website)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string", nameof(ModInfoAttribute.Website)));
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.IconPath):
                                    // Ignored for single file mods, but add a case here so we don't produce a warning.
                                    break;

                                case nameof(ModInfoAttribute.WorldConfig):
                                    if (!TryParseString(propValueExpr, out var _)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string", nameof(ModInfoAttribute.WorldConfig)));
                                        error = true;
                                    }
                                    //TODO(Rennorb) @completeness: I am unsure what this is supposed to do or where to store it.
                                    break;

                                case nameof(ModInfoAttribute.Authors): {
                                    if (!TryParseArrayInitializer(propValueExpr, out var initializer)) {
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string array", nameof(ModInfoAttribute.Authors)));
                                        error = true;
                                    }

                                    var authors = new List<string>();
                                    int j = 0;
                                    foreach (var elementExpression in initializer!.Expressions) {
                                        if (!TryParseString(elementExpression, out var author)) {
                                            errorCallback(Errors.PrimitiveParsingFailure(elementExpression.ToString(), "string", $"{nameof(ModInfoAttribute.Authors)}[{j}]"));
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
                                        errorCallback(Errors.PrimitiveParsingFailure(propValueExpr.ToString(), "string array", nameof(ModInfoAttribute.Contributors)));
                                        error = true;
                                    }

                                    var contributors = new List<string>();
                                    int j = 0;
                                    foreach (var elementExpression in initializer!.Expressions) {
                                        if (!TryParseString(elementExpression, out var contributor)) {
                                            errorCallback(Errors.PrimitiveParsingFailure(elementExpression.ToString(), "string", $"{nameof(ModInfoAttribute.Contributors)}[{j}]"));
                                        }
                                        else {
                                            contributors.Add(contributor!);
                                        }
                                        j++;
                                    }
                                    modInfo.Contributors = contributors;
                                } break;

                                default:
                                    errorCallback(Errors.UnexpectedProperty(propName, propValueExpr.ToString()));
                                    error = true;
                                    break;
                            }
                        }

                    } break;

                    case "ModDependency":
                    case nameof(ModDependencyAttribute): {
                        if(attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) {
                            errorCallback(Errors.MissingRequiredProperty($"{nameof(ModDependencyAttribute)}[@{attribute.GetLocation()}]", nameof(ModDependencyAttribute.ModID)));
                            error = true;
                            break;
                        }

                        var attrArgs = attribute.ArgumentList.Arguments;
                        
                        if(!TryParseString(attrArgs[0].Expression, out var depName)) {
                            errorCallback(Errors.PrimitiveParsingFailure(attrArgs[0].Expression.ToString(), "string", $"{nameof(ModDependencyAttribute)}[@{attribute.GetLocation()}]{nameof(ModDependencyAttribute.ModID)}"));
                            error = true;
                        }

                        string? depVersion = null;
                        if(attrArgs.Count > 1 && !TryParseString(attrArgs[1].Expression, out depVersion)) {
                            errorCallback(Errors.PrimitiveParsingFailure(attrArgs[1].Expression.ToString(), "string", $"{nameof(ModDependencyAttribute)}[@{attribute.GetLocation()}]{nameof(ModDependencyAttribute.Version)}"));
                            error = true;
                        }

                        if (depName != null) {
                            dependencies.Add(NewDependencyUnchecked(depName, depVersion));
                        }
                    } break;
                }
            }
        }

        if (modInfo == null) {
            errorCallback(Errors.MissingAssemblyAttribute("ModInfo"));
            return false;
        }

        modInfo.Dependencies = dependencies;
        return !error;


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
                case SyntaxKind.FalseLiteralExpression:
                    parsed = false;
                    return true;

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
}
