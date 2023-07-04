namespace KismetKompiler.Library.Syntax;

public abstract class SwitchLabel : SyntaxNode
{
    public List<Statement> Body { get; set; }

    protected SwitchLabel()
    {
        Body = new List<Statement>();
    }

    protected SwitchLabel(params Statement[] statements)
    {
        Body = statements.ToList();
    }
}