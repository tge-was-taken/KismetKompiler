using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Decompiler.Context;

public class Node
{
    public required Node Parent { get; set; }
    public required KismetExpression Source { get; set; }
    public required int CodeStartOffset { get; set; }
    public required int CodeEndOffset { get; set; }
    public HashSet<Node> ReferencedBy { get; init; } = new();
    public List<Node> Children { get; init; } = new();

    public Node()
    {

    }

    public override string ToString()
    {
        return $"{CodeStartOffset}: {Source.Inst} {string.Join(' ', Children.Select(x => x.ToString()))}";
    }
}
