using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;

namespace KismetKompiler.Library.Syntax;

public class Parameter : SyntaxNode
{
    public List<Statements.Declarations.AttributeDeclaration> Attributes { get; init; } = new();
    public ParameterModifier Modifier { get; set; }

    public TypeIdentifier Type { get; set; }

    public Identifier Identifier { get; set; }

    public virtual bool IsArray => false;
    public Expression DefaultVaue { get; set; }

    public bool IsVarArgs { get; set; }

    public Parameter()
    {
    }

    public Parameter(ParameterModifier modifier, TypeIdentifier type, Identifier identifier, Expression defaultValue)
    {
        Modifier = modifier;
        Type = type;
        Identifier = identifier;
        DefaultVaue = defaultValue;
    }

    public override string ToString()
    {
        return $"{(Modifier == ParameterModifier.Out ? "out " : "")}{Type} {Identifier}";
    }
}

public class ArrayParameter : Parameter
{
    public IntLiteral Size { get; set; }

    public override bool IsArray => true;

    public ArrayParameter()
    {

    }

    public ArrayParameter(ParameterModifier modifier, TypeIdentifier type, Identifier identifier, IntLiteral size) : base(modifier, type, identifier, null)
    {
        Size = size;
    }

    public override string ToString()
    {
        return base.ToString() + $"[{Size}]";
    }
}