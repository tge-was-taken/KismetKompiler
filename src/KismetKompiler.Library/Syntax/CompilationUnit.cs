using KismetKompiler.Library.Syntax.Statements;

namespace KismetKompiler.Library.Syntax;

public class CompilationUnit : SyntaxNode
{
    public List<Declaration> Declarations { get; set; }

    public CompilationUnit()
    {
        Declarations = new List<Declaration>();
    }

    public CompilationUnit(List<Declaration> declarations)
    {
        Declarations = declarations;
    }
}
