using KismetKompiler.Library.Syntax.Statements.Expressions;

namespace KismetKompiler.Library.Syntax.Statements.Declarations
{
    [Flags]
    public enum ClassModifiers
    {
        Public = 1 << 1,
        Private = 1 << 2,
        Abstract = 1 << 3,
        Static = 1 << 4,
    }

    public class ClassDeclaration : Declaration
    {
        public ClassDeclaration() : base(DeclarationType.Class)
        {
        }

        public ClassModifiers Modifiers { get; set; } = 0;
        public List<Identifier> InheritedTypeIdentifiers { get; init; } = new();
        public List<Declaration> Declarations { get; init; } = new();

        public Identifier? BaseClassIdentifier => InheritedTypeIdentifiers.FirstOrDefault();
    }
}
