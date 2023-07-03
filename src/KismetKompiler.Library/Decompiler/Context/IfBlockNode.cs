using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Library.Decompiler.Context;

public class IfBlockNode : BlockNode
{
    public KismetExpression Condition { get; set; }
}
