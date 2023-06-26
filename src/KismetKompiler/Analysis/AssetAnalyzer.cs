using UAssetAPI;
using UAssetAPI.ExportTypes;

namespace KismetKompiler.Analysis;

internal class AssetAnalyzer
{
    public void Analyze(string directoryPath)
    {
        var propertyExportTypes = new HashSet<string>();
        var propertyExportValues = new Dictionary<string, List<string>>();

        foreach (var uassetFile in Directory.EnumerateFiles(directoryPath, "*.uasset", SearchOption.AllDirectories))
        {
            var asset = new UAsset(uassetFile, UAssetAPI.UnrealTypes.EngineVersion.VER_UE4_23);
            foreach (var export in asset.Exports)
            {
                if (export is PropertyExport propertyExport)
                {
                    //export.
                }
            }
        }
    }
}
