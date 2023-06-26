using KismetKompiler.Syntax.Statements;

namespace KismetKompiler.Syntax.Statements.Expressions.Unary;

public class NegationOperator : PrefixOperator
{
    public NegationOperator()
    {

    }

    public NegationOperator(Expression operand) : base(operand)
    {

    }

    public override string ToString()
    {
        return $"-({Operand})";
    }
}
