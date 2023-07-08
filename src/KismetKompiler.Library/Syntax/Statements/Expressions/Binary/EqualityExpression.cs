namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public abstract class EqualityExpression : BinaryExpression, IOperator
{
    public int Precedence => 9;

    public EqualityExpression() : base(ValueKind.Bool)
    {
    }

    public EqualityExpression(Expression left, Expression right)
        : base(ValueKind.Bool, left, right)
    {

    }
}
