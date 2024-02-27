using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Decompiler.Analysis;

public class Symbol
{
    private List<Symbol> _children = new();
    private Symbol? _parent;

    public Symbol? Parent
    {
        get => _parent;
        set
        {
            _parent?._children.Remove(this);
            _parent = value;
            _parent?._children.Add(this);
        }
    }
    public IReadOnlyList<Symbol> Children => _children;
    public string Name { get; set; }

    public SymbolFlags Flags { get; set; }
    public SymbolType Type { get; set; }

    public Import? Import { get; set; }
    public FPackageIndex? ImportIndex { get; set; }

    public virtual Export? Export { get; set; }
    public FPackageIndex? ExportIndex { get; set; }

    public FProperty FProperty { get; set; }
    public UProperty UProperty { get; set; }

    public Symbol? Class { get; set; }
    public Symbol? Super { get; set; }
    public Symbol? Template { get; set; }
    public Symbol SuperStruct { get; set; }
    public Symbol InnerClass { get; set; }
    public Symbol? PropertyType { get; set; }
    public Symbol? ClonedFrom { get; set; }

    public SymbolFunctionMetadata FunctionMetadata { get; set; } = new();

    public Symbol()
    {
        
    }

    public bool HasMember(string name)
    {
        return GetMember(name) != null;
    }

    public bool HasMember(Symbol member)
    {
        return Children.Contains(member) ||
                (Super?.HasMember(member) ?? false) ||
                (PropertyType?.HasMember(member) ?? false) ||
                (Class?.HasMember(member) ?? false);
    }

    public Symbol? GetMember(KismetPropertyPointer pointer)
    {
        if (pointer.Old != null)
        {
            return GetMember(pointer.Old);
        }
        else
        {
            return GetMember(pointer.New.Path[0].ToString());
        }
    }

    public Symbol? GetMember(FPackageIndex index)
    {
        return Children.Where(x => x.ImportIndex?.Index == index.Index || x.ExportIndex?.Index == index.Index).SingleOrDefault()
            ?? Super?.GetMember(index)
            ?? PropertyType?.GetMember(index)
            ?? Class?.GetMember(index);
    }

    public Symbol? GetMember(string name)
    {
        return Children.Where(x => x.Name == name).SingleOrDefault() 
            ?? Super?.GetMember(name)
            ?? PropertyType?.GetMember(name) 
            ?? Class?.GetMember(name);
    }

    public void AddChild(Symbol child)
    {
        if (child.Parent != this)
        {
            child.Parent = this;
        }
    }

    public void RemoveChild(Symbol child)
    {
        if (child.Parent == this)
        {
            child.Parent = null;
        }
    }

    public void AddChildren(IEnumerable<Symbol> children)
    {
        foreach (var child in children)
        {
            AddChild(child);
        }
    }

    public void RemoveChildren(IEnumerable<Symbol> children)
    {
        foreach (var child in children)
        {
            RemoveChild(child);
        }
    }

    public override string ToString()
    {
        if (PropertyType != null)
            return $"[{Flags}] {Class?.Name}<{PropertyType?.Name}> {Name}";
        else
            return $"[{Flags}] {Class?.Name} {Name}";
    }
}