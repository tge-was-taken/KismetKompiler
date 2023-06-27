using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements.Declarations;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Compiler
{
    internal class VariableInfo
    {
        public Parameter? Parameter { get; set; }
        public required VariableDeclaration Declaration { get; set; }
        public FPackageIndex? PackageIndex { get; set; }
        public FFieldPath? FieldPath { get; set; }
        public bool AllowShadowing { get; set; } = false;
    }
}