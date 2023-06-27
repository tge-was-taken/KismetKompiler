using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements.Declarations;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Compiler
{
    internal class ProcedureInfo
    {
        public ProcedureDeclaration Declaration { get; set; }
        public bool IsExternal { get; set; }
        public FPackageIndex PackageIndex { get; set; }
    }
}