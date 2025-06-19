using System.IO.Compression;
using System.Text.Json;
using Vintagestory.API.Common;

namespace VintageStory.ModPeek;

public static partial class ModPeek
{
    // See tests for examples of the attributes we are trying to parse.
    /// <remarks> The ModInfo obtained from this function has not been validated! </remarks>
    public static bool TryExtractModInfoFromZip(byte[] bytes, out ModInfo? modInfo, Action<Errors.Error> errorCallback)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            var modInfoEntry = zip.GetEntry("modinfo.json");
            if (modInfoEntry == null) {
                // Figure out if all entries start with the same path prefix. In that case the user zipped a folder instead of individual files.
                string? commonPathPrefix = null;
                foreach(var entry in zip.Entries) {
                    var firstSeparator = entry.FullName.IndexOf('/');
                    if (firstSeparator == -1) { // Indicates an individual file in the root, we are already good.
                        commonPathPrefix = null;
                        break;
                    }

                    if (commonPathPrefix == null) {
                        commonPathPrefix = entry.FullName.Substring(0, firstSeparator + 1);
                    }
                    else if (!entry.FullName.StartsWith(commonPathPrefix)) { // Found other file / folder, so its not just one root directory.
                        commonPathPrefix = null;
                        break;
                    }
                }
                errorCallback(new Errors.MissingFileInArchiveRoot("modinfo.json", commonPathPrefix != null));
                modInfo = null;
                return false;
            }

            using var inputStream = modInfoEntry.Open();
            try {
                var root = JsonDocument.Parse(inputStream, new() {
                    AllowTrailingCommas = true,
                });
                return TryParseModInfoFromJsonCaseInsensitive(root, out modInfo, errorCallback);
            }
            catch(Exception e) {
                errorCallback(new Errors.MalformedJson(e));
                modInfo = null;
                return false;
            }
        }
        catch (Exception e)
        {
            errorCallback(new Errors.MalformedArchive(e));
            modInfo = null;
            return false;
        }
    }
}

