namespace KismetKompiler.Library.Syntax.Statements.Expressions.Unary;

public abstract class PostfixOperator : UnaryExpression, IOperator
{
    public int Precedence => 2;

    protected PostfixOperator() : base(ValueKind.Unresolved)
    {
    }

    protected PostfixOperator(Expression operand) : base(ValueKind.Unresolved, operand)
    {

    }
}
