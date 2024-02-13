using KismetKompiler.Library.Syntax;

namespace KismetKompiler.Library.Utilities;

public static class KeywordDictionary
{
    public static Dictionary<ValueKind, string> ValueTypeToKeyword { get; } = new Dictionary<ValueKind, string>
    {
        { ValueKind.Void, "void" },
        { ValueKind.Bool, "bool" },
        { ValueKind.Int, "int" },
        { ValueKind.Float, "float" },
        { ValueKind.String, "string" },
    };

    public static Dictionary<string, ValueKind> KeywordToValueType { get; } = ValueTypeToKeyword.Reverse();

    public static Dictionary<VariableModifier, string> ModifierTypeToKeyword { get; } = new Dictionary<VariableModifier, string>
    {
        { VariableModifier.Const, "const" },
    };

    public static Dictionary<string, VariableModifier> KeywordToModifierType { get; } = ModifierTypeToKeyword.Reverse();
}
