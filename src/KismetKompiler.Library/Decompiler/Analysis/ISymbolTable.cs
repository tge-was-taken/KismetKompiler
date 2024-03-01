using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Decompiler.Analysis
{
    public interface ISymbolTable : IEnumerable<Symbol>, IList<Symbol>
    {
        IEnumerable<Symbol> AllSymbols { get; }
        IEnumerable<Symbol> Classes { get; }
        Symbol? DefaultClass { get; }
        Symbol? FunctionClass { get; }
        IEnumerable<Symbol> Functions { get; }
        IEnumerable<Symbol> Packages { get; }
        IEnumerable<Symbol> RootSymbols { get; }

        Symbol? GetClass(string name);
        Symbol? GetSymbol(string name);
        Symbol? GetSymbolByExport(FPackageIndex index);
        Symbol? GetSymbolByImport(FPackageIndex index);
        Symbol? GetSymbolByPackageIndex(FPackageIndex index);
        ISymbolTable Union(ISymbolTable other);
    }
}