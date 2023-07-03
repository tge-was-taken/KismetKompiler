using UAssetAPI.ExportTypes;
using UAssetAPI;

namespace KismetKompiler.Library.Decompiler.Context
{
    public class DecompilerContext
    {
        public required UnrealPackage Asset { get; init; }
        public required ClassExport Class { get; init; }
        public required FunctionExport Function { get; init; }

        public DecompilerContext() { }
    }
}
