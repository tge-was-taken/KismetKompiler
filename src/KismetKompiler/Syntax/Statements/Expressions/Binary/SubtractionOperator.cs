using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Expressions;

namespace KismetKompiler.Syntax.Statements.Expressions.Binary;

public class SubtractionOperator : BinaryExpression, IOperator
{
    public int Precedence => 6;

    public SubtractionOperator() : base(ValueKind.Unresolved)
    {
    }

    public SubtractionOperator(Expression left, Expression right)
        : base(ValueKind.Unresolved, left, right)
    {

    }

    public override string ToString()
    {
        return $"({Left}) - ({Right})";
    }
}
