namespace KismetKompiler.Library.Syntax.Statements;

public class WhileStatement : Statement, IBlockStatement
{
    public Expression Condition { get; set; }

    public CompoundStatement Body { get; set; }

    IEnumerable<CompoundStatement> IBlockStatement.Blocks => new[] { Body }.Where(x => x != null);

    public WhileStatement()
    {
    }

    public WhileStatement(Expression condition, CompoundStatement body)
    {
        Condition = condition;
        Body = body;
    }
}
