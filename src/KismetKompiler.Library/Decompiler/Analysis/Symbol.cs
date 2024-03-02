using System.Diagnostics;
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
    public Symbol? ClassWithin 
    { 
        get => _innerClass; 
        set 
        { 
            if (value != _innerClass)
            {
                CheckCircularReferenceRecursively(value, x => x.ClassWithin);
                _innerClass = value;
            }
        } 
    }
    public Symbol? PropertyClass 
    { 
        get => _propertyType; 
        set 
        { 
            if (value != _propertyType)
            {
                CheckCircularReferenceRecursively(value, x => x.PropertyClass);
                _propertyType = value;
            }
        } 
    }
    public Symbol? ClonedFrom { get; set; }

    public Symbol? Enum { get; set; }
    public Symbol? UnderlyingProp { get; set; }
    public Symbol? Inner { get; set; }
    public Symbol? ElementProp { get; set; }
    public Symbol? MetaClass { get; set; }
    public Symbol? SignatureFunction { get; set; }
    public Symbol? InterfaceClass { get; set; }
    public Symbol? KeyProp { get; set; }
    public Symbol? ValueProp { get; set; }
    public Symbol? Struct { get; set; }

    public SymbolFunctionMetadata FunctionMetadata { get; set; } = new();
    public SymbolClassMetadata ClassMetadata { get; set; } = new();

    public Symbol()
    {

    }

    /// <summary>
    /// Gets the super class at the root of the class hierarchy.
    /// </summary>
    public Symbol? RootSuperClass
    {
        get
        {
            var currentClass = Super;
            while (currentClass?.Super != null)
                currentClass = currentClass.Super;
            return currentClass;
        }
    }

    public bool CheckSuperClassCircularReference(Symbol? newSuperClass = default)
    {
        var currentClass = this;
        var seenClasses = new HashSet<Symbol>();
        while (currentClass.Super != null)
        {
            if (!seenClasses.Add(currentClass))
                throw new AnalysisException($"Referential cycle in superclass of {currentClass}");
            currentClass = currentClass.Super;
        }
        if (newSuperClass != null)
            if (!seenClasses.Add(newSuperClass))
                throw new AnalysisException($"Referential cycle in superclass of {currentClass}");
        return true;
    }

    /// <summary>
    /// Adds a superclass at the root of the class hierarchy.
    /// </summary>
    /// <param name="superClass"></param>
    /// <exception cref="AnalysisException"></exception>
    public void AddSuperClass(Symbol superClass)
    {
        CheckSuperClassCircularReference(superClass);
        var currentClass = RootSuperClass ?? this;
        currentClass.Super = superClass;
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

    public bool IsClass => 
        Class?.Name == "Class" ||
        Class?.Name == "BlueprintGeneratedClass";

    public bool IsInstance => !IsClass;

    public Symbol? ResolvedType
        => IsClass ? this : PropertyClass ?? InterfaceClass ?? Struct ?? Class;

    public bool IsImport => Import != null;
    public bool IsExport => Export != null;

    private Symbol? FindMember(Func<Symbol, bool> predicate)
    {
        var stack = new Stack<Symbol>();
        stack.Push(this);
        var iterations = 0;

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            Debug.Assert(current.ResolvedType?.CheckSuperClassCircularReference() ?? true);

            foreach (var child in current.Children)
            {
                if (predicate(child))
                    return child;
            }

            if (current.Super != null)
                stack.Push(current.Super);

            if (current.PropertyClass != null)
                stack.Push(current.PropertyClass);

            if (current.InterfaceClass != null)
                stack.Push(current.InterfaceClass);

            if (current.Struct != null)
                stack.Push(current.Struct);

            if (current.Class != null)
                stack.Push(current.Class);

            ++iterations;
            if (iterations > 1000)
                throw new AnalysisException($"Circular reference in {this}");
        }

        return null;
    }

    public bool HasMember(string name)
    {
        return FindMember(x => x.Name == name) != null;
    }

    public bool HasMember(Symbol member)
    {
        return FindMember(x => x == member) != null;
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
        Debug.Assert(Super != this && PropertyClass != this && InterfaceClass != this && Struct != this && Class != this);

        return Children.Where(x => x.ImportIndex?.Index == index.Index || x.ExportIndex?.Index == index.Index).SingleOrDefault()
            ?? Super?.GetMember(index)
            ?? PropertyClass?.GetMember(index)
            ?? InterfaceClass?.GetMember(index)
            ?? Struct?.GetMember(index)
            ?? Class?.GetMember(index);
    }

    public Symbol? GetMember(string name)
    {
        Debug.Assert(Super != this && PropertyClass != this && InterfaceClass != this && Struct != this && Class != this);

        return Children.Where(x => x.Name == name).SingleOrDefault()
            ?? Super?.GetMember(name)
            ?? PropertyClass?.GetMember(name)
            ?? InterfaceClass?.GetMember(name)
            ?? Struct?.GetMember(name)
            ?? Class?.GetMember(name);
    }

    public bool InheritsClass(Symbol classSymbol)
    {
        if (Super == classSymbol)
            return true;
        return Super?.InheritsClass(classSymbol) ?? false;
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
        if (PropertyClass != null)
            return $"[{Flags}] {Class?.Name}<{PropertyClass?.Name}> {Name}";
        else if (InterfaceClass != null)
            return $"[{Flags}] {Class?.Name}<{InterfaceClass?.Name}> {Name}";
        else if (Struct != null)
            return $"[{Flags}] {Class?.Name}<{Struct?.Name}> {Name}";
        else
            return $"[{Flags}] {Class?.Name} {Name}";
    }
}

public class SymbolClassMetadata
{
    public bool IsStaticClass { get; set; }
}