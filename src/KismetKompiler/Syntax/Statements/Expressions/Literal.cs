using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Expressions.Literals;

namespace KismetKompiler.Syntax.Statements.Expressions;

public abstract class Literal : Expression
{
    protected Literal(ValueKind kind) : base(kind)
    {
    }

    public override int GetDepth() => 1;
}


public abstract class Literal<T> : Literal
{
    public T Value { get; set; }

    protected Literal(ValueKind kind) : base(kind)
    {
    }

    protected Literal(ValueKind kind, T value) : base(kind)
    {
        Value = value;
    }

    public static implicit operator T(Literal<T> value) => value.Value;

    public override string ToString()
    {
        return Value.ToString();
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
