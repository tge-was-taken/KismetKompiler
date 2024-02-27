using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Analysis;

public class MemberAccessContext
{
    public required EX_Context ContextExpression { get; init; }
    public required Symbol ContextSymbol { get; init; }
    public required KismetExpression VariableExpression { get; init; }
    public required Symbol VariableSymbol { get; init; }

    public override string ToString()
    {
        return $"{ContextExpression} {ContextSymbol} {VariableExpression} {VariableSymbol}";
    }
}
