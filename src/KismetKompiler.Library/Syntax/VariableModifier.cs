namespace KismetKompiler.Library.Syntax;

[Flags]
public enum VariableModifier
{
    Local = 1 << 1,
    Const = 1 << 2,
    Ref = 1 << 3,
    Public = 1 << 4,
}