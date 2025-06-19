using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    /// <remarks> The ModInfo obtained from this function has not been validated! </remarks>
    public static bool TryExtractModInfo(FileInfo f, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
    {
        var bytes = File.ReadAllBytes(f.FullName);
        if (bytes.Length < 4) {
            errorCallback(new Errors.FileToSmall());
            modInfo = null;
            return false;
        }

        switch (f.Extension) {
            case ".zip": return TryExtractModInfoFromZip(bytes, out modInfo, errorCallback);
            case ".cs" : return TryExtractModInfoFromCs (bytes, out modInfo, errorCallback);
            case ".dll": return TryExtractModInfoFromDll(bytes, out modInfo, errorCallback);
        }

        var magic = BitConverter.ToUInt32(bytes, 0);
        //NOTE(Rennorb): The only byteswap intrinsic on this version of dotnet is System.Net.HostToNetwork and I don't want to import that.
        if (BitConverter.IsLittleEndian) magic = ((magic >> 24) & 0x000000FF) | ((magic >> 8) & 0x0000FF00) | ((magic << 8) & 0x00FF0000) | ((magic << 24) & 0xFF000000);

        if (magic == 0x504B0304) return TryExtractModInfoFromZip(bytes, out modInfo, errorCallback);
        //NOTE(Rennorb): Technically speaking this is the MS DOS header and is optional, but realistically every dll is going to have it.
        if ((magic & 0xffff0000) == 0x4D5A0000) return TryExtractModInfoFromDll(bytes, out modInfo, errorCallback);

        if (TryExtractModInfoFromCs(bytes, out modInfo, errorCallback)) return true;

        errorCallback(new Errors.CouldNotDetermineFileType());
        return false;
    }
}
