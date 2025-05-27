using System.Text;
using Vintagestory.API.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VintageStory.ModPeek;

static partial class ModPeek
{
    // See tests for examples of the attributes we are trying to parse.
    static bool TryGetCsInfo(byte[] bytes, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
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
                                        error = true;
                                    }
                                    break;

                                case nameof(ModInfoAttribute.NetworkVersion):
                                    if (!TryParseString(propValueExpr, out modInfo.NetworkVersion)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.NetworkVersion), "string", propValueExpr.ToString()));
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
                                    // Ignored for single file mods, but add a case here so we don't produce a warning.
                                    break;

                                case nameof(ModInfoAttribute.WorldConfig):
                                    if (!TryParseString(propValueExpr, out var _)) {
                                        errorCallback(new Errors.PrimitiveParsingFailure(nameof(ModInfoAttribute.WorldConfig), "string", propValueExpr.ToString()));
                                        error = true;
                                    }
                                    //TODO(Rennorb) @completeness: I am unsure what this is supposed to do or where to store it.
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

                                default:
                                    errorCallback(new Errors.UnexpectedProperty(propName, propValueExpr.ToString()));
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

        if (modInfo == null) {
            errorCallback(new Errors.MissingAssemblyAttribute("ModInfo"));
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
