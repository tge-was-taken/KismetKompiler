using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Compiler;

class FunctionState
{
    public string Name { get; init; }
    public List<CompiledExpressionContext> AllExpressions { get; init; } = new();
    public Dictionary<KismetExpression, CompiledExpressionContext> ExpressionContextLookup { get; init; } = new();
    public List<CompiledExpressionContext> PrimaryExpressions { get; init; } = new();
    public int CodeOffset { get; set; } = 0;
    public LabelInfo ReturnLabel { get; set; }
}
