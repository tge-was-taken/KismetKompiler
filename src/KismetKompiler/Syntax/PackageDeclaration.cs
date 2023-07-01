using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Expressions;
using KismetKompiler.Syntax.Statements.Expressions.Identifiers;

namespace KismetKompiler.Syntax;

public class PackageDeclaration : Declaration
{
    public PackageDeclaration() : base(DeclarationType.Package)
    {
    }

    public List<Declaration> Declarations { get; init; } = new();

    public override string ToString()
    {
        return $"from \"{Identifier.Text}\" import {{}}";
    }
}