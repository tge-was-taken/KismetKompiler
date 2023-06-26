namespace KismetKompiler.Syntax.Statements;

public interface IBlockStatement
{
    IEnumerable<CompoundStatement> Blocks { get; }
}
