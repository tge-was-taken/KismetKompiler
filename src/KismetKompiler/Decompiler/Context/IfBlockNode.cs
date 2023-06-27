using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Decompiler.Context;

public class IfBlockNode : BlockNode
{
    public KismetExpression Condition { get; set; }
}
