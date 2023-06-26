using KismetKompiler.Syntax.Statements.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KismetKompiler.Syntax.Statements.Declarations
{
    [Flags]
    public enum ClassModifiers
    {
        Public = 1 << 1,
        Private = 1 << 2,
    }

    public class ClassDeclaration : Declaration
    {
        public ClassDeclaration() : base(DeclarationType.Class)
        {
        }

        public List<Attribute> Attributes { get; set; } = new();
        public ClassModifiers Modifiers { get; set; } = 0;
        public List<Identifier> InheritedTypeIdentifiers { get; init; } = new();
        public List<Declaration> Declarations { get; init; } = new();

        public Identifier? BaseClassIdentifier => InheritedTypeIdentifiers.FirstOrDefault();
    }
}
