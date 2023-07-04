using KismetKompiler.Library.Compiler.Context;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Compiler.Intermediate;

public class IntermediatePackageIndex : FPackageIndex
{
    public IntermediatePackageIndex(Symbol symbol)
    {
        Symbol = symbol;
        IsDummy = true;
    }

    public Symbol Symbol { get; }
}
