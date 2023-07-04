namespace KismetKompiler.Library.Syntax.Statements.Expressions;

public enum MemberExpressionKind
{
    Dot,
    Pointer
}

public class MemberExpression : Expression, IOperator
{
    public Expression Context { get; set; }

    public Expression Member { get; set; }

    public MemberExpressionKind Kind { get; set; }

    public int Precedence => 2;

    public MemberExpression() : base(ValueKind.Unresolved)
    {
    }

    public override string ToString()
    {
        return $"{Context}.{Member}";
    }

    public override int GetDepth() => 1 + Context.GetDepth() + Member.GetDepth();
}
