namespace KismetKompiler.Library.Syntax;

public class DefaultSwitchLabel : SwitchLabel
{
    public DefaultSwitchLabel()
    {
    }

    public DefaultSwitchLabel(params Statement[] statements)
        : base(statements)
    {
    }

    public override string ToString()
    {
        return "default";
    }
}