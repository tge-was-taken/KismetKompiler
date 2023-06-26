using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler;

public class KismetScriptLabel
{
    public string Name { get; set; }
    public int CodeOffset { get; set; }
}

public class KismetScriptFunction
{
    public string Name { get; set; }
    public List<KismetPropertyPointer> LocalVariables { get; init; } = new();
    public List<KismetExpression> Expressions { get; init; } = new();
}

public class KismetScriptProperty
{
    public string Name { get; set; }
    public string Type { get; set; }
}

public class KismetScriptClass
{
    public EClassFlags Flags { get; set; }
    public string Name { get; set; }
    public string? BaseClass { get; set; }
    public List<KismetScriptProperty> Properties { get; init; } = new();
    public List<KismetScriptFunction> Functions { get; init; } = new();
}

public class KismetScript
{
    public List<KismetScriptProperty> Properties { get; init; } = new();
    public List<KismetScriptFunction> Functions { get; init; } = new();
    public List<KismetScriptClass> Classes { get; init; } = new();
}
