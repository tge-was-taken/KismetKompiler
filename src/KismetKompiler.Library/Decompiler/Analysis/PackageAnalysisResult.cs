namespace KismetKompiler.Library.Decompiler.Analysis;

public class PackageAnalysisResult
{
    public required IReadOnlyList<Symbol> AllSymbols { get; init; }
    public required IReadOnlyList<Symbol> RootSymbols { get; init; }
}
