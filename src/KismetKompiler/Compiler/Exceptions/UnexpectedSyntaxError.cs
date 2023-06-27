using KismetKompiler.Syntax;

namespace KismetKompiler.Compiler.Exceptions;

class UnexpectedSyntaxError : CompilationError
{
    public UnexpectedSyntaxError(SyntaxNode syntaxNode)
        : base(syntaxNode, $"{syntaxNode.SourceInfo?.Line}:{syntaxNode.SourceInfo?.Column}: {syntaxNode} was unexpected at this time.")
    {
    }
}
