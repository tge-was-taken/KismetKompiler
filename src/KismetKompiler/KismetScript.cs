using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler;

public class KismetScriptLabel
{
    public string Name { get; set; }
    public int CodeOffset { get; set; }

    public KismetScriptLabel()
    {
    }

    public KismetScriptLabel(string name, int codeOffset)
    {
        Name = name;
        CodeOffset = codeOffset;
    }
}

public class KismetScriptFunction
{
    public string Name { get; set; }
    public List<KismetExpression> Instructions { get; init; } = new();

    public KismetScriptFunction()
    {
        
    }

    public KismetScriptFunction(string name, List<KismetExpression> instructions)
    {
        Name = name;
        Instructions = instructions;
    }
}

public class KismetScript
{
    public List<KismetScriptFunction> Functions { get; init; } = new();
}
