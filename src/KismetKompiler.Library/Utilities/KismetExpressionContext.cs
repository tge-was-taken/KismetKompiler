using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Library.Utilities;

public record KismetExpressionContext<T>(
    KismetExpression Expression,
    int CodeStartOffset,
    T Tag)
{
    public int? CodeEndOffset { get; set; }
}
