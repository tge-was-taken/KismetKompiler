using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler;

public record KismetExpressionContext<T>(
    KismetExpression Expression,
    int CodeStartOffset,
    T Tag)
{
    public int? CodeEndOffset { get; set; }
}
