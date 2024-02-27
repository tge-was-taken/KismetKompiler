namespace KismetKompiler.Library.Decompiler.Analysis;

public class KismetAnalysisResult
{
    public required IReadOnlyList<Symbol> AllSymbols { get; init; }
    public required IReadOnlyList<Symbol> RootSymbols { get; init; }
}
