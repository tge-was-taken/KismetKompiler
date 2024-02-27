using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;
using System.Text;

namespace KismetKompiler.Library.Syntax.Statements.Declarations;

public enum ProcedureModifier
{
    Public = 1 << 0,
    Private = 1 << 1,
    Sealed = 1 << 2,
    Virtual = 1 << 3,
    Protected = 1 << 4,
    Static = 1 << 5,
    Override = 1 << 6,
}

public class ProcedureDeclaration : Declaration, IBlockStatement
{
    public ProcedureModifier Modifiers { get; set; } = 0;

    public bool IsPublic => Modifiers.HasFlag(ProcedureModifier.Public);
    public bool IsPrivate => Modifiers.HasFlag(ProcedureModifier.Private);
    public bool IsSealed => Modifiers.HasFlag(ProcedureModifier.Sealed);
    public bool IsVirtual => !IsSealed || IsStatic;
    public bool IsOverride => IsVirtual || Modifiers.HasFlag(ProcedureModifier.Override); // TODO
    public bool IsProtected => Modifiers.HasFlag(ProcedureModifier.Protected);
    public bool IsStatic => Modifiers.HasFlag(ProcedureModifier.Static);

    public bool IsExternal => Body == null;

    public IntLiteral Index { get; set; }

    public TypeIdentifier ReturnType { get; set; }

    public List<Parameter> Parameters { get; set; }

    public CompoundStatement Body { get; set; }

    IEnumerable<CompoundStatement> IBlockStatement.Blocks => new[] { Body }.Where(x => x != null);

    public ProcedureDeclaration() : base(DeclarationType.Procedure)
    {
        Parameters = new List<Parameter>();
    }

    public ProcedureDeclaration(TypeIdentifier returnType, Identifier identifier, List<Parameter> parameters, CompoundStatement body) : base(DeclarationType.Procedure, identifier)
    {
        ReturnType = returnType;
        Parameters = parameters;
        Body = body;
    }

    public ProcedureDeclaration(IntLiteral index, TypeIdentifier returnType, Identifier identifier, List<Parameter> parameters, CompoundStatement body) : base(DeclarationType.Procedure, identifier)
    {
        Index = index;
        ReturnType = returnType;
        Parameters = parameters;
        Body = body;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append($"{ReturnType} {Identifier}(");
        if (Parameters.Count > 0)
            builder.Append(Parameters[0]);

        for (int i = 1; i < Parameters.Count; i++)
        {
            builder.Append($", {Parameters[i]}");
        }

        builder.Append(")");

        if (Body != null)
        {
            builder.Append($"{{ {Body} }}");
        }

        return builder.ToString();
    }
}
