namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class EqualityOperator : BinaryExpression, IOperator
{
    public int Precedence => 9;

    public EqualityOperator() : base(ValueKind.Bool)
    {
    }

    public EqualityOperator(Expression left, Expression right)
        : base(ValueKind.Bool, left, right)
    {

    }

    public override string ToString()
    {
        return $"({Left}) == ({Right})";
    }
}
