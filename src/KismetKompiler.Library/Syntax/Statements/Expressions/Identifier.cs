using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements;

namespace KismetKompiler.Library.Syntax.Statements.Expressions;

public class Identifier : Expression
{
    public string Text { get; set; }

    public Identifier() : base(ValueKind.Unresolved)
    {
    }

    public Identifier(ValueKind kind) : base(kind)
    {
    }

    public Identifier(string text) : base(ValueKind.Unresolved)
    {
        Text = text;
    }

    public Identifier(ValueKind kind, string text) : base(kind)
    {
        Text = text;
    }

    public override string ToString()
    {
        return Text;
    }

    public override int GetHashCode()
    {
        return Text.GetHashCode();
    }

    public override int GetDepth() => 1;
}
