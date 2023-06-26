using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Expressions;

namespace KismetKompiler.Syntax.Statements.Declarations;

public class EnumValueDeclaration : Declaration
{
    public Expression Value { get; set; }

    public EnumValueDeclaration() : base(DeclarationType.EnumLabel)
    {
    }

    public EnumValueDeclaration(Identifier identifier) : base(DeclarationType.EnumLabel, identifier)
    {
    }

    public EnumValueDeclaration(Identifier identifier, Expression value) : base(DeclarationType.EnumLabel, identifier)
    {
        Value = value;
    }

    public override string ToString()
    {
        return $"{Identifier} = {Value}";
    }
}