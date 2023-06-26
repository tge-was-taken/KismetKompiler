using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Expressions.Identifiers;

namespace KismetKompiler.Syntax.Statements.Expressions;

public class CastOperator : UnaryExpression, IOperator
{
    public TypeIdentifier TypeIdentifier { get; set; }

    public int Precedence => 2;

    public CastOperator() : base(ValueKind.Unresolved)
    {
        
    }

    public override int GetDepth() => 1;
}