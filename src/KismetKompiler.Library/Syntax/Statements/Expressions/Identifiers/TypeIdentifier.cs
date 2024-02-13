﻿using KismetKompiler.Library.Utilities;

namespace KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;

public class TypeIdentifier : Identifier
{
    public static TypeIdentifier Void { get; } = new TypeIdentifier(ValueKind.Void);

    public ValueKind ValueKind { get; set; }

    public TypeIdentifier? TypeParameter { get; set; }

    public bool IsConstructedType => TypeParameter != null;

    public TypeIdentifier() : base(ValueKind.Type)
    {
    }

    public TypeIdentifier(ValueKind valueKind) : base(ValueKind.Type, KeywordDictionary.ValueTypeToKeyword[valueKind])
    {
        ValueKind = valueKind;
    }

    public TypeIdentifier(string text) : base(ValueKind.Type, text)
    {
        if (!KeywordDictionary.KeywordToValueType.TryGetValue(text, out var valueType))
        {
            ValueKind = ValueKind.Int;
        }
        else
        {
            ValueKind = valueType;
        }
    }

    public TypeIdentifier(ValueKind valueKind, string text) : base(ValueKind.Type, text)
    {
        ValueKind = valueKind;
    }
}
