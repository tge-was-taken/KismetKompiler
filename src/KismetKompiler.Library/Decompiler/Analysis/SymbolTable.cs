using System.Collections;
using System.Xml.Linq;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Decompiler.Analysis;

public class AggregrateSymbolTable : ISymbolTable
{
    private readonly IEnumerable<ISymbolTable> _symbolTables;

    public AggregrateSymbolTable(IEnumerable<ISymbolTable> symbolTables)
    {
        _symbolTables = symbolTables;
    }

    public Symbol this[int index] { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public IEnumerable<Symbol> AllSymbols => _symbolTables.SelectMany(t => t.AllSymbols);

    public IEnumerable<Symbol> Classes => _symbolTables.SelectMany(t => t.Classes);

    public Symbol? DefaultClass => 
        _symbolTables.Where(x => x.DefaultClass != null).Select(x => x.DefaultClass).FirstOrDefault();

    public Symbol? FunctionClass =>
        _symbolTables.Where(x => x.FunctionClass != null).Select(x => x.FunctionClass).FirstOrDefault();

    public IEnumerable<Symbol> Functions => _symbolTables.SelectMany(x => x.Functions);

    public IEnumerable<Symbol> Packages => _symbolTables.SelectMany(x => x.Packages);

    public IEnumerable<Symbol> RootSymbols => _symbolTables.SelectMany(x => x.RootSymbols);

    public int Count => _symbolTables.Sum(x => x.Count);

    public bool IsReadOnly => true;

    public void Add(Symbol item)
    {
        throw new NotSupportedException();
    }

    public void Clear()
    {
        throw new NotSupportedException();
    }

    public bool Contains(Symbol item)
        => _symbolTables.Any(x => x.Contains(item));

    public void CopyTo(Symbol[] array, int arrayIndex) =>
        throw new NotSupportedException();

    public Symbol? GetClass(string name)
        => _symbolTables.Select(x => x.GetClass(name)).Where(x => x != null).FirstOrDefault();

    public IEnumerator<Symbol> GetEnumerator()
        => _symbolTables.SelectMany(x => x.AllSymbols).GetEnumerator();

    public Symbol? GetSymbol(string name)
        => _symbolTables.Select(x => x.GetSymbol(name)).Where(x => x != null).FirstOrDefault();

    public Symbol? GetSymbolByExport(FPackageIndex index)
        => _symbolTables.Select(x => x.GetSymbolByExport(index)).Where(x => x != null).FirstOrDefault();

    public Symbol? GetSymbolByImport(FPackageIndex index)
        => _symbolTables.Select(x => x.GetSymbolByImport(index)).Where(x => x != null).FirstOrDefault();

    public Symbol? GetSymbolByPackageIndex(FPackageIndex index)
        => _symbolTables.Select(x => x.GetSymbolByPackageIndex(index)).Where(x => x != null).FirstOrDefault();

    public int IndexOf(Symbol item)
    {
        throw new NotSupportedException();
    }

    public void Insert(int index, Symbol item)
    {
        throw new NotSupportedException();
    }

    public bool Remove(Symbol item)
    {
        throw new NotSupportedException();
    }

    public void RemoveAt(int index)
    {
        throw new NotSupportedException();
    }

    public ISymbolTable Union(ISymbolTable other)
    {
        return new AggregrateSymbolTable(new ISymbolTable[] { this, other });
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class SymbolTable : IEnumerable<Symbol>, IList<Symbol>, ISymbolTable
{
    private readonly List<Symbol> _symbols = new();

    public IEnumerable<Symbol> AllSymbols
        => _symbols;

    public IEnumerable<Symbol> RootSymbols
        => _symbols.Where(x => x.Parent == null);

    public IEnumerable<Symbol> Packages
        => _symbols.Where(x => x.Type == SymbolType.Package);

    public IEnumerable<Symbol> Classes
        => _symbols.Where(x => x.Type == SymbolType.Class);

    public IEnumerable<Symbol> Functions
        => _symbols.Where(x => x.Type == SymbolType.Function);

    public Symbol DefaultClass
        => _symbols.Where(x => x.Name == "Class").First();

    public Symbol FunctionClass
        => _symbols.Where(x => x.Name == "Function").First();

    public int Count => _symbols.Count;

    public bool IsReadOnly => ((ICollection<Symbol>)_symbols).IsReadOnly;

    public Symbol this[int index] { get => ((IList<Symbol>)_symbols)[index]; set => ((IList<Symbol>)_symbols)[index] = value; }

    public Symbol? GetClass(string name)
        => _symbols
            .Where(x => x.Type == SymbolType.Class)
            .Where(x => x.Name == name).FirstOrDefault();

    public Symbol? GetSymbol(string name)
        => _symbols.Where(x => x.Name == name).FirstOrDefault();

    public Symbol? GetSymbolByImport(FPackageIndex index)
        => _symbols.Where(x => x.ImportIndex?.Index == index.Index).FirstOrDefault();

    public Symbol? GetSymbolByExport(FPackageIndex index)
        => _symbols.Where(x => x.ExportIndex?.Index == index.Index).FirstOrDefault();

    public Symbol? GetSymbolByPackageIndex(FPackageIndex index)
        => _symbols.Where(x => x.ImportIndex?.Index == index.Index || x.ExportIndex?.Index == index.Index).FirstOrDefault();

    public SymbolTable()
    {

    }

    public SymbolTable(IEnumerable<Symbol> symbols)
    {
        _symbols = symbols.ToList();
    }

    public void Join(SymbolTable other)
    {
        foreach (var item in other)
            Add(item);
    }

    public ISymbolTable Union(ISymbolTable other)
        => new SymbolTable(this._symbols.Union(other));
        //=> new AggregrateSymbolTable(new[] { this, other });

    public IEnumerator<Symbol> GetEnumerator()
    {
        return ((IEnumerable<Symbol>)_symbols).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_symbols).GetEnumerator();
    }

    public int IndexOf(Symbol item)
    {
        return ((IList<Symbol>)_symbols).IndexOf(item);
    }

    public void Insert(int index, Symbol item)
    {
        ((IList<Symbol>)_symbols).Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        ((IList<Symbol>)_symbols).RemoveAt(index);
    }

    public void Add(Symbol item)
    {
        ((ICollection<Symbol>)_symbols).Add(item);
    }

    public void Clear()
    {
        ((ICollection<Symbol>)_symbols).Clear();
    }

    public bool Contains(Symbol item)
    {
        return ((ICollection<Symbol>)_symbols).Contains(item);
    }

    public void CopyTo(Symbol[] array, int arrayIndex)
    {
        ((ICollection<Symbol>)_symbols).CopyTo(array, arrayIndex);
    }

    public bool Remove(Symbol item)
    {
        return ((ICollection<Symbol>)_symbols).Remove(item);
    }
}
