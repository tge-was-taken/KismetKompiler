namespace KismetKompiler.Library.Syntax.Statements.Expressions.Unary;

public class LogicalNotOperator : PrefixOperator
{
    public LogicalNotOperator()
    {

    }

    public LogicalNotOperator(Expression operand) : base(ValueKind.Bool, operand)
    {

    }

    public override string ToString()
    {
        return $"!({Operand})";
    }
}
