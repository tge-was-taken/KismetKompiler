using KismetKompiler.Syntax.Statements;

namespace KismetKompiler.Syntax.Statements.Expressions.Unary;


public class PostfixDecrementOperator : PostfixOperator
{
    public PostfixDecrementOperator()
    {

    }

    public PostfixDecrementOperator(Expression operand) : base(operand)
    {

    }

    public override string ToString()
    {
        return $"({Operand})--";
    }
}
