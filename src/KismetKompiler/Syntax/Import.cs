using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Expressions;
using KismetKompiler.Syntax.Statements.Expressions.Identifiers;

namespace KismetKompiler.Syntax;

public class Import : SyntaxNode
{
    public string PackageName { get; set; }

    public List<Declaration> Declarations { get; init; } = new();

    public override string ToString()
    {
        return $"from \"{PackageName}\" import {{}}";
    }
}