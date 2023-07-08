using KismetKompiler.Library.Syntax.Statements.Expressions;

namespace KismetKompiler.Library.Syntax.Statements.Declarations;

public class AttributeDeclaration : SyntaxNode
{
    public Identifier Identifier { get; set; }
    public List<Argument> Arguments { get; init; } = new();
}
