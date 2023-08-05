using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Library.Decompiler.Context.Nodes;

public class IfBlockNode : BlockNode
{
    public KismetExpression Condition { get; set; }
}
