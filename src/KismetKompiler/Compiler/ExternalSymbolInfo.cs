using KismetKompiler.Syntax.Statements;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Compiler;

internal class ExternalSymbolInfo
{
    public Declaration Declaration { get; set; }
    public FPackageIndex PackageIndex { get; set; }
}