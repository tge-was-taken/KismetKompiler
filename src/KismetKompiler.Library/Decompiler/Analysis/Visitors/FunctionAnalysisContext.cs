using UAssetAPI;

namespace KismetKompiler.Library.Decompiler.Analysis.Visitors;

public class FunctionAnalysisContext
{
    public required UnrealPackage Asset { get; init; }
    public required SymbolTable Symbols { get; init; }
    public required SymbolTable InferredSymbols { get; init; }
    public required HashSet<MemberAccessContext> UnexpectedMemberAccesses { get; init; } = new();
}
