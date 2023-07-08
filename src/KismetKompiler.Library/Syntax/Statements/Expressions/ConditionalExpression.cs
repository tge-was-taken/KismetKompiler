namespace KismetKompiler.Library.Syntax.Statements.Expressions;

public class ConditionalExpression : Expression, IOperator
{
    public Expression Condition { get; set; }
    public Expression ValueIfTrue { get; set; }
    public Expression ValueIfFalse { get; set; }

    public int Precedence => 16;

    public ConditionalExpression() : base(ValueKind.Unresolved)
    {
    }


    public override int GetDepth() => 1 + Condition.GetDepth() + ValueIfTrue.GetDepth() + ValueIfFalse.GetDepth();
}
