namespace KismetKompiler.Library.Syntax.Statements;

public class ForStatement : Statement, IBlockStatement
{
    public Statement Initializer { get; set; }

    public Expression Condition { get; set; }

    public Expression AfterLoop { get; set; }

    public CompoundStatement Body { get; set; }

    IEnumerable<CompoundStatement> IBlockStatement.Blocks => new[] { Body }.Where(x => x != null);

    public ForStatement()
    {
    }

    public ForStatement(Statement initializer, Expression condition, Expression afterLoop, CompoundStatement body)
    {
        Initializer = initializer;
        Condition = condition;
        AfterLoop = afterLoop;
        Body = body;
    }
}
