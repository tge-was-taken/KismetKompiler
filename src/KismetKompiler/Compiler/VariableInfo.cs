using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements.Declarations;

namespace KismetKompiler.Compiler
{
    internal class VariableInfo
    {
        public Parameter Parameter { get; set; }
        public VariableDeclaration Declaration { get; set; }
    }
}