using Mono.Cecil;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    static bool TryGetDllInfo(byte[] bytes, out ModInfo? modInfo, Action<Error> errorCallback)
    {
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
            errorCallback(Errors.MissingAssemblyAttribute(nameof(ModInfoAttribute)));
            modInfo = null;
            return false;
        }

        var name = modInfoAttr.ConstructorArguments[0].Value as string;
        var modID = modInfoAttr.ConstructorArguments[1].Value as string;

        if (!Enum.TryParse(GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.Side)), true, out EnumAppSide side)) {
            side = EnumAppSide.Universal;
        }

        var dependencies = assemblyDefinition.CustomAttributes
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
            GetPropertyValue<bool?>(modInfoAttr, nameof(ModInfoAttribute.RequiredOnClient)).GetValueOrDefault(),
            GetPropertyValue<bool?>(modInfoAttr, nameof(ModInfoAttribute.RequiredOnServer)).GetValueOrDefault(),
            dependencies
        ) {
            NetworkVersion = GetPropertyValue<string>(modInfoAttr, nameof(ModInfoAttribute.NetworkVersion)),
        };
        return true;
    }
}