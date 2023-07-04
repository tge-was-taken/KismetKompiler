using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;

namespace KismetKompiler.Library.Syntax.Statements.Expressions;

public class CastOperator : UnaryExpression, IOperator
{
    public TypeIdentifier TypeIdentifier { get; set; }

    public int Precedence => 2;

    public CastOperator() : base(ValueKind.Unresolved)
    {

    }

    public override int GetDepth() => 1;
}