using KismetKompiler.Library.Syntax;

namespace KismetKompiler.Library.Compiler.Exceptions;

public class CompilationError : Exception
{
    public CompilationError(SyntaxNode syntaxNode, string message)
        : base(message)
    {

    }
}
