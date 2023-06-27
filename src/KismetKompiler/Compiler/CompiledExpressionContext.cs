using KismetKompiler.Syntax;
using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Compiler;

public class CompiledExpressionContext
{
    public SyntaxNode SyntaxNode { get; init; }
    public List<KismetExpression> CompiledExpressions { get; init; } = new();
    public List<LabelInfo> ReferencedLabels { get; init; } = new();
    public int CodeOffset { get; init; }

    public CompiledExpressionContext()
    {

    }

    public CompiledExpressionContext(SyntaxNode syntaxNode, int codeOffset, KismetExpression compiledExpression)
    {
        SyntaxNode = syntaxNode;
        CodeOffset = codeOffset;
        CompiledExpressions = new() { compiledExpression };
    }

    public CompiledExpressionContext(SyntaxNode syntaxNode, int codeOffset, KismetExpression compiledExpression, IEnumerable<LabelInfo> referencedLabels)
    {
        SyntaxNode = syntaxNode;
        CodeOffset = codeOffset;
        CompiledExpressions = new() { compiledExpression };
        ReferencedLabels = referencedLabels.ToList();
    }
}