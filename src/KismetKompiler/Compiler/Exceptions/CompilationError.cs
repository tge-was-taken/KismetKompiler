using KismetKompiler.Syntax;

namespace KismetKompiler.Compiler.Exceptions;

class CompilationError : Exception
{
    public CompilationError(SyntaxNode syntaxNode, string message)
        : base(message)
    {

    }
}
