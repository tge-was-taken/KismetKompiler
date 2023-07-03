using KismetKompiler.Library.Syntax.Statements;

namespace KismetKompiler.Library.Syntax;

public class ConditionSwitchLabel : SwitchLabel
{
    public Expression Condition { get; set; }

    public ConditionSwitchLabel()
    {
    }

    public ConditionSwitchLabel(Expression condition, params Statement[] statements)
        : base(statements)
    {
        Condition = condition;
    }

    public override string ToString()
    {
        return $"case {Condition}:";
    }
}