namespace KismetKompiler.Library.Decompiler.Context.Nodes;

public class ConditionalJumpNode : JumpNode
{
    public Node Condition { get; set; }
    public bool Inverted { get; set; }

    public override string ToString()
    {
        return $"{CodeStartOffset}: {Source.Inst} <{(Inverted ? "not " : "")}{Condition}> -> {Target}";
    }
}