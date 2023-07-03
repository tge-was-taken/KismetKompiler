using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Syntax;
using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Library.Compiler;

public class CompiledExpressionContext
{
    public SyntaxNode SyntaxNode { get; init; }
    public List<KismetExpression> CompiledExpressions { get; init; } = new();
    public List<LabelSymbol> ReferencedLabels { get; init; } = new();
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

    public CompiledExpressionContext(SyntaxNode syntaxNode, int codeOffset, KismetExpression compiledExpression, IEnumerable<LabelSymbol> referencedLabels)
    {
        SyntaxNode = syntaxNode;
        CodeOffset = codeOffset;
        CompiledExpressions = new() { compiledExpression };
        ReferencedLabels = referencedLabels.ToList();
    }
}