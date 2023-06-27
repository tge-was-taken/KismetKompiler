using KismetKompiler.Syntax.Statements.Expressions.Literals;

namespace KismetKompiler.Syntax;

[Flags]
public enum VariableModifier
{
    Local = 1<<1,
    Const  =1<<2,
    Ref =1<<3,
}