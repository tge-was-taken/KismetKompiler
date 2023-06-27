using KismetKompiler.Syntax;

namespace KismetKompiler.Compiler.Exceptions;

class RedefinitionError : CompilationError
{
    public RedefinitionError(SyntaxNode syntaxNode)
        : base(syntaxNode, $"{syntaxNode.SourceInfo?.Line}:{syntaxNode.SourceInfo?.Column}: {syntaxNode} redefinition.")
    {

    }
}
