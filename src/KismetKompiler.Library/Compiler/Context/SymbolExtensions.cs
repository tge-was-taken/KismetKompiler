using KismetKompiler.Library.Syntax;

namespace KismetKompiler.Library.Compiler.Context;

public static class SymbolExtensions
{
    public static SyntaxNode GetSyntaxNode(this Symbol symbol)
    {
        if (symbol is IDeclaredSymbol declaredSymbol)
        {
            return declaredSymbol.Declaration;
        }
        else
        {
            return null;
        }
    }
}
