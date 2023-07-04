using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Expressions;

namespace KismetKompiler.Library.Syntax;

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