using KismetKompiler.Library.Syntax.Statements;

namespace KismetKompiler.Library.Syntax;

public class CompilationUnit : SyntaxNode
{
    public List<PackageDeclaration> Imports { get; set; }

    public List<Declaration> Declarations { get; set; }

    public CompilationUnit()
    {
        Imports = new List<PackageDeclaration>();
        Declarations = new List<Declaration>();
    }

    public CompilationUnit(List<PackageDeclaration> imports, List<Declaration> declarations)
    {
        Imports = imports;
        Declarations = declarations;
    }
}
