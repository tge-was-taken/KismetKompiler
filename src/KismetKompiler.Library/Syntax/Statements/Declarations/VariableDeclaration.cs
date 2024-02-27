using System.Text;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;

namespace KismetKompiler.Library.Syntax.Statements.Declarations;

public class VariableDeclaration : Declaration
{
    public VariableModifier Modifiers { get; set; }

    public TypeIdentifier Type { get; set; }

    public Expression Initializer { get; set; }

    public virtual bool IsArray => false;

    public VariableDeclaration() : base(DeclarationType.Variable) { }

    public VariableDeclaration(VariableModifier modifier, TypeIdentifier type, Identifier identifier, Expression initializer)
        : base(DeclarationType.Variable, identifier)
    {
        Modifiers = modifier;
        Type = type;
        Initializer = initializer;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append($"{Modifiers} ");

        builder.Append($"{Type} {Identifier}");
        if (Initializer != null)
        {
            builder.Append($" = {Initializer}");
        }

        return builder.ToString();
    }
}

public class ArrayVariableDeclaration : VariableDeclaration
{
    public IntLiteral Size { get; set; }

    public override bool IsArray => true;

    public ArrayVariableDeclaration()
    {
    }

    public ArrayVariableDeclaration(VariableModifier modifier, TypeIdentifier type, Identifier identifier, IntLiteral size, Expression initializer)
        : base(modifier, type, identifier, initializer)
    {
        Size = size;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append($"{Modifiers} ");

        builder.Append($"{Type} {Identifier}[{Size}]");
        if (Initializer != null)
        {
            builder.Append($" = {Initializer}");
        }

        return builder.ToString();
    }
}
