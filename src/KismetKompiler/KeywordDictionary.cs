using KismetKompiler.Syntax;

namespace KismetKompiler;

public static class KeywordDictionary
{
    public static Dictionary<ValueKind, string> ValueTypeToKeyword { get; } = new Dictionary<ValueKind, string>
    {
        { ValueKind.Void, "void" },
        { ValueKind.Bool, "bool" },
        { ValueKind.Int, "int" },
        { ValueKind.Float, "float" },
        { ValueKind.String, "string" }
    };

    public static Dictionary<string, ValueKind> KeywordToValueType { get; } = ValueTypeToKeyword.Reverse();

    public static Dictionary<VariableModifierKind, string> ModifierTypeToKeyword { get; } = new Dictionary<VariableModifierKind, string>
    {
        { VariableModifierKind.Global, "global" },
        { VariableModifierKind.Constant, "const" },
    };

    public static Dictionary<string, VariableModifierKind> KeywordToModifierType { get; } = ModifierTypeToKeyword.Reverse();
}
