using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    /// <remarks> The ModInfo obtained from this function has not been validated! </remarks>
    public static bool TryExtractModInfoAndWorldConfig(FileInfo f, out ModInfo? modInfo, out ModWorldConfiguration? worldConfig, Action<Errors.Error> errorCallback)
    {
        var bytes = File.ReadAllBytes(f.FullName);
        if (bytes.Length < 4) {
            errorCallback(new Errors.FileToSmall());
            modInfo = null;
            worldConfig = null;
            return false;
        }

        switch (f.Extension) {
            case ".zip": return TryExtractModInfoAndWorldConfigFromZip(bytes, out modInfo, out worldConfig, errorCallback);
            case ".cs" : return TryExtractModInfoAndWorldConfigFromCs (bytes, out modInfo, out worldConfig, errorCallback);
            case ".dll": return TryExtractModInfoAndWorldConfigFromDll(bytes, out modInfo, out worldConfig, errorCallback);
        }

        var magic = BitConverter.ToUInt32(bytes, 0);
        //NOTE(Rennorb): The only byteswap intrinsic on this version of dotnet is System.Net.HostToNetwork and I don't want to import that.
        if (BitConverter.IsLittleEndian) magic = ((magic >> 24) & 0x000000FF) | ((magic >> 8) & 0x0000FF00) | ((magic << 8) & 0x00FF0000) | ((magic << 24) & 0xFF000000);

        if (magic == 0x504B0304) return TryExtractModInfoAndWorldConfigFromZip(bytes, out modInfo, out worldConfig, errorCallback);
        //NOTE(Rennorb): Technically speaking this is the MS DOS header and is optional, but realistically every dll is going to have it.
        if ((magic & 0xffff0000) == 0x4D5A0000) return TryExtractModInfoAndWorldConfigFromDll(bytes, out modInfo, out worldConfig, errorCallback);

        if (TryExtractModInfoAndWorldConfigFromCs(bytes, out modInfo, out worldConfig, errorCallback)) return true;

        errorCallback(new Errors.CouldNotDetermineFileType());
        return false;
    }
}
