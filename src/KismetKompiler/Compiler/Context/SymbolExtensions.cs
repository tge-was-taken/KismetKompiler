using KismetKompiler.Compiler.Symbols;
using KismetKompiler.Syntax;

namespace KismetKompiler.Compiler.Context;

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
