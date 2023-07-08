namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public abstract class BitwiseOperator : BinaryExpression, IOperator
{
    public abstract int Precedence { get; }

    public BitwiseOperator() : base(ValueKind.Unresolved)
    {
    }

    public BitwiseOperator(Expression left, Expression right)
        : base(ValueKind.Unresolved, left, right)
    {

    }
}
