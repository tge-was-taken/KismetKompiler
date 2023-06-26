using KismetKompiler.Syntax.Statements;

namespace KismetKompiler.Syntax.Statements.Expressions.Binary;

public class DivisionAssignmentOperator : CompoundAssignmentOperator
{
    public DivisionAssignmentOperator()
    {

    }

    public DivisionAssignmentOperator(Expression left, Expression right)
        : base(left, right)
    {
    }

    public override string ToString()
    {
        return $"{Left} *= ({Right})";
    }
}
