using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Syntax;

namespace KismetKompiler.Library.Compiler.Exceptions;

public class RedefinitionError : CompilationError
{
    public RedefinitionError(SyntaxNode syntaxNode)
        : base(syntaxNode, $"{syntaxNode.SourceInfo?.Line}:{syntaxNode.SourceInfo?.Column}: {syntaxNode} redefinition.")
    {

    }

    public RedefinitionError(Symbol symbol)
        : this(symbol.Declaration) { }

}
