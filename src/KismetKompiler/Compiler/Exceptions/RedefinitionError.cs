using KismetKompiler.Compiler.Context;
using KismetKompiler.Compiler.Symbols;
using KismetKompiler.Syntax;

namespace KismetKompiler.Compiler.Exceptions;

class RedefinitionError : CompilationError
{
    public RedefinitionError(SyntaxNode syntaxNode)
        : base(syntaxNode, $"{syntaxNode.SourceInfo?.Line}:{syntaxNode.SourceInfo?.Column}: {syntaxNode} redefinition.")
    {

    }

    public RedefinitionError(Symbol symbol)
        : this(symbol.GetSyntaxNode()) { }
    
}
