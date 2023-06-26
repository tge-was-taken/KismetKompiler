using KismetKompiler.Compiler;
using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Declarations;
using KismetKompiler.Syntax.Statements.Expressions;
using KismetKompiler.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Syntax.Statements.Expressions.Literals;
using System.Diagnostics;
using Enum = KismetKompiler.Compiler.Enum;

namespace KismetKompiler;

internal class Scope
{
    public Scope? Parent { get; }

    public Dictionary<string, FunctionInfo> Functions { get; }

    public Dictionary<string, ProcedureInfo> Procedures { get; }

    public Dictionary<string, VariableInfo> Variables { get; }

    public Dictionary<string, Enum> Enums { get; }

    public LabelInfo BreakLabel { get; set; }

    public LabelInfo ContinueLabel { get; set; }

    public Dictionary<Expression, LabelInfo> SwitchLabels { get; set; }

    public Dictionary<string, LabelInfo> Labels { get; set; }

    public Scope(Scope? parent)
    {
        Parent = parent;
        Functions = new Dictionary<string, FunctionInfo>();
        Procedures = new Dictionary<string, ProcedureInfo>();
        Variables = new Dictionary<string, VariableInfo>();
        Enums = new Dictionary<string, Enum>();
        Labels = new();
    }

    public bool TryGetBreakLabel(out LabelInfo label)
    {
        if (BreakLabel != null)
        {
            label = BreakLabel;
            return true;
        }

        if (Parent != null)
            return Parent.TryGetBreakLabel(out label);

        label = null;
        return false;
    }

    public bool TryGetContinueLabel(out LabelInfo label)
    {
        if (ContinueLabel != null)
        {
            label = ContinueLabel;
            return true;
        }

        if (Parent != null)
            return Parent.TryGetContinueLabel(out label);

        label = null;
        return false;
    }

    public bool TryGetFunction(string name, out FunctionInfo function)
    {
        if (!Functions.TryGetValue(name, out function))
        {
            if (Parent == null)
                return false;

            if (!Parent.TryGetFunction(name, out function))
                return false;
        }

        return true;
    }

    public bool TryGetProcedure(string name, out ProcedureInfo procedure)
    {
        if (!Procedures.TryGetValue(name, out procedure))
        {
            if (Parent == null)
                return false;

            if (!Parent.TryGetProcedure(name, out procedure))
                return false;
        }

        return true;
    }

    public bool TryGetVariable(string name, out VariableInfo variable)
    {
        if (!Variables.TryGetValue(name, out variable))
        {
            if (Parent == null)
                return false;

            if (!Parent.TryGetVariable(name, out variable))
                return false;
        }

        return true;
    }

    public bool TryGetEnum(string name, out Enum enumDeclaration)
    {
        if (!Enums.TryGetValue(name, out enumDeclaration))
        {
            if (Parent == null)
                return false;

            if (!Parent.TryGetEnum(name, out enumDeclaration))
                return false;
        }

        return true;
    }

    public bool TryGetLabel(string name, out LabelInfo label)
    {
        if (!Labels.TryGetValue(name, out label))
        {
            if (Parent == null)
                return false;

            if (!Parent.TryGetLabel(name, out label))
                return false;
        }

        return true;
    }

    public bool TryGetSwitchLabel(SyntaxNode syntaxNode, out LabelInfo label)
    {
        if (SwitchLabels == null || (label = SwitchLabels.SingleOrDefault(x => x.Key.GetHashCode() == syntaxNode.GetHashCode()).Value) == null)
        {
            if (Parent == null)
            {
                label = null;
                return false;
            }

            if (!Parent.TryGetSwitchLabel(syntaxNode, out label))
                return false;
        }

        return true;
    }

    public bool TryDeclareLabel(LabelDeclaration label)
    {
        if (TryGetLabel(label.Identifier.Text, out _))
            return false;

        var labelInfo = new LabelInfo()
        {
            CodeOffset = 0,
            Declaration = label,
            IsResolved = false,
            Name = label.Identifier.Text,
        };
        Labels[label.Identifier.Text] = labelInfo;

        return true;
    }

    public bool TryDeclareFunction(FunctionDeclaration declaration)
    {
        if (TryGetFunction(declaration.Identifier.Text, out _))
            return false;

        var function = new FunctionInfo();
        function.Declaration = declaration;
        function.Index = (short)declaration.Index.Value;

        Functions[declaration.Identifier.Text] = function;

        return true;
    }

    public bool TryDeclareProcedure(ProcedureDeclaration declaration, out ProcedureInfo procedure)
    {
        if (TryGetProcedure(declaration.Identifier.Text, out procedure))
        {
            return false;
        }

        var p = new ProcedureInfo();
        p.Declaration = declaration;
        p.Index = declaration.Index == null ? (short)Procedures.Count : (short)declaration.Index.Value;
        Debug.Assert(Procedures.All(x => x.Value.Index != p.Index), "Same procedure index used by multiple procedures");
        Procedures[declaration.Identifier.Text] = procedure = p;
        return true;
    }

    //public bool TryDeclareProcedure(ProcedureDeclaration declaration, Procedure compiled, out ProcedureInfo procedure)
    //{
    //    if (!TryDeclareProcedure(declaration, out procedure))
    //        return false;

    //    procedure.Compiled = compiled;
    //    procedure.OriginalCompiled = compiled.Clone();
    //    return true;
    //}

    public bool TryDeclareVariable(VariableDeclaration declaration)
    {
        if (TryGetVariable(declaration.Identifier.Text, out _))
            return false;

        var variable = new VariableInfo();
        variable.Declaration = declaration;

        Variables[declaration.Identifier.Text] = variable;

        return true;
    }

    public bool TryDeclareVariable(VariableInfo variable)
    {
        if (TryGetVariable(variable.Declaration.Identifier.Text, out _))
            return false;

        Variables[variable.Declaration.Identifier.Text] = variable;

        return true;
    }

    public bool TryDeclareEnum(EnumDeclaration declaration)
    {
        if (TryGetEnum(declaration.Identifier.Text, out _))
            return false;

        var enumType = new Enum
        {
            Declaration = declaration,
            Members = declaration.Values.ToDictionary(x => x.Identifier.Text, y => y.Value)
        };

        int nextMemberValue = 0;
        bool anyImplicitValues = false;

        for (int i = 0; i < enumType.Members.Count; i++)
        {
            var key = enumType.Members.Keys.ElementAt(i);
            var value = enumType.Members[key];

            if (value == null)
            {
                enumType.Members[key] = new IntLiteral(nextMemberValue++);
                anyImplicitValues = true;
            }
            else
            {
                if (!TryGetNextMemberValue(enumType.Members, value, out nextMemberValue))
                {
                    // Only error if there are any implicit values
                    if (anyImplicitValues)
                        return false;
                }
            }
        }

        Enums[declaration.Identifier.Text] = enumType;

        return true;
    }

    private bool TryGetNextMemberValue(Dictionary<string, Expression> members, Expression enumValue, out int nextMemberValue)
    {
        if (enumValue is IntLiteral intLiteral)
        {
            nextMemberValue = intLiteral.Value + 1;
            return true;
        }
        if (enumValue is Identifier identifier)
        {
            if (members.TryGetValue(identifier.Text, out var value))
            {
                if (!TryGetNextMemberValue(members, value, out nextMemberValue))
                    return false;
            }
        }

        nextMemberValue = -1;
        return false;
    }
}
