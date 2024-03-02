using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Analysis;

public class MemberAccessContext
{
    public required KismetExpression ContextExpression { get; init; }
    public required Symbol ContextSymbol { get; init; }
    public required KismetExpression MemberExpression { get; init; }
    public required Symbol MemberSymbol { get; init; }

    public override string ToString()
    {
        return $"{ContextExpression} {ContextSymbol} {MemberExpression} {MemberSymbol}";
    }
}
