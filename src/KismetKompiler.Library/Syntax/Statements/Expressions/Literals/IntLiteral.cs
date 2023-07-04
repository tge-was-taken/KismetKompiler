namespace KismetKompiler.Library.Syntax.Statements.Expressions.Literals;

public class IntLiteral : Literal<int>, IEquatable<IntLiteral>
{
    public IntLiteral() : base(ValueKind.Int)
    {
    }

    public IntLiteral(int value) : base(ValueKind.Int, value)
    {
    }

    public bool Equals(IntLiteral other)
    {
        return Value == other?.Value;
    }

    public static implicit operator IntLiteral(int value) => new IntLiteral(value);
}
