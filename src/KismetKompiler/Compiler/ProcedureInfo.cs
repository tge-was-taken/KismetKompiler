using KismetKompiler.Syntax.Statements.Declarations;

namespace KismetKompiler.Compiler
{
    internal class ProcedureInfo
    {
        public ProcedureDeclaration Declaration { get; set; }

        public KismetScriptFunction Compiled { get; set; }

        public KismetScriptFunction OriginalCompiled { get; set; }

        public short Index { get; set; }
    }
}