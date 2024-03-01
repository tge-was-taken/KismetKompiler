using KismetKompiler.Library.Compiler.Context;
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
    private Symbol? _class;
    private Symbol? _super;
    private Symbol? _template;
    private Symbol _superStruct;
    private Symbol _innerClass;
    private Symbol? _propertyType;

    public Symbol? Parent
    {
        get => _parent;
        set
        {
            CheckCircularReference(value);
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

    public Symbol? Class
    {
        get => _class;
        set
        {
            if (value != _class)
            {
                CheckCircularReferenceRecursively(value, x => x.Class);
                _class = value;
            }
        }
    }
    public Symbol? Super
    {
        get => _super;
        set
        {
            if (value != _super)
            {
                CheckCircularReferenceRecursively(value, x => x.Super);
                _super = value;
            }
        }
    }
    public Symbol? Template 
    { 
        get => _template; 
        set 
        { 
            if (value != _template)
            {
                CheckCircularReferenceRecursively(value, x => x.Template);
                _template = value;
            }
        } 
    }
    public Symbol? SuperStruct 
    {
        get => _superStruct; 
        set 
        { 
            if (value != _superStruct)
            {
                CheckCircularReferenceRecursively(value, x => x.SuperStruct);
                _superStruct = value;
            }
        } 
    }
    public Symbol? InnerClass 
    { 
        get => _innerClass; 
        set 
        { 
            if (value != _innerClass)
            {
                CheckCircularReferenceRecursively(value, x => x.InnerClass);
                _innerClass = value;
            }
        } 
    }
    public Symbol? PropertyType 
    { 
        get => _propertyType; 
        set 
        { 
            if (value != _propertyType)
            {
                CheckCircularReferenceRecursively(value, x => x.PropertyType);
                _propertyType = value;
            }
        } 
    }
    public Symbol? ClonedFrom { get; set; }

    public SymbolFunctionMetadata FunctionMetadata { get; set; } = new();

    public IEnumerable<Symbol> ClassHierarchy => GetAncestors(x => x.Class);
    public IEnumerable<Symbol> SuperHierarchy => GetAncestors(x => x.Super);
    public IEnumerable<Symbol> TemplateHierarchy => GetAncestors(x => x.Template);
    public IEnumerable<Symbol> InnerClassHierarchy => GetAncestors(x => x.InnerClass);
    public IEnumerable<Symbol> PropertyTypeHierarchy => GetAncestors(x => x.PropertyType);

    public Symbol()
    {

    }

    private IEnumerable<Symbol> GetAncestors(Func<Symbol, Symbol?> getter)
    {
        var ancestor = getter(this);
        if (ancestor != null)
        {
            yield return ancestor;
            foreach (var subAncestor in GetAncestors(getter))
                yield return subAncestor;
        }
    }

    private void CheckCircularReference(Symbol? symbol)
    {
        if (symbol == this)
            throw new InvalidOperationException($"Symbol {symbol} circular dependency detected");
    }

    private void CheckCircularReferenceRecursively(Symbol? symbol, Func<Symbol, Symbol?> getter)
    {
        if (symbol == this)
            throw new InvalidOperationException($"Symbol {symbol} circular dependency detected");

        var ancestor = getter(this);
        if (ancestor != null)
        {
            if (ancestor == symbol)
                throw new InvalidOperationException($"Symbol {ancestor} circular dependency detected");
            ancestor.CheckCircularReferenceRecursively(symbol, getter);
        }
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