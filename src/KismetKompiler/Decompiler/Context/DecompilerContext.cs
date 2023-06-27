using UAssetAPI.ExportTypes;
using UAssetAPI;

namespace KismetKompiler.Decompiler.Context
{
    public class DecompilerContext
    {
        public required UAsset Asset { get; init; }
        public required ClassExport Class { get; init; }
        public required FunctionExport Function { get; init; }

        public DecompilerContext() { }
    }
}
