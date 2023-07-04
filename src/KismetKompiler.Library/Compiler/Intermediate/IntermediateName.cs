using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Compiler.Intermediate;

public class IntermediateName : FName
{
    public IntermediateName(string value)
    {
        TextValue = value;
        DummyValue = new(nameof(IntermediateName));
    }

    public string TextValue { get; }
}
