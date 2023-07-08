namespace KismetKompiler.Library.Syntax.Statements.Expressions;

public abstract class PrimaryExpression : Expression
{
    protected PrimaryExpression(ValueKind kind) : base(kind)
    {
    }
}
