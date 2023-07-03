namespace KismetKompiler.Library.Syntax.Statements;

public interface IBlockStatement
{
    IEnumerable<CompoundStatement> Blocks { get; }
}
