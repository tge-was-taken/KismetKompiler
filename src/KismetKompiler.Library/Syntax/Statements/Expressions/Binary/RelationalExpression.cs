namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public abstract class RelationalExpression : BinaryExpression, IOperator
{
    public int Precedence => 9;

    public RelationalExpression() : base(ValueKind.Bool)
    {
    }
}