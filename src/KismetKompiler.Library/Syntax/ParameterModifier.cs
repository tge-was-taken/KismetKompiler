namespace KismetKompiler.Library.Syntax;

[Flags]
public enum ParameterModifier
{
    None = 1 << 0,
    Out = 1 << 1,
    Ref = 1 << 2,
    Const = 1 << 3
}