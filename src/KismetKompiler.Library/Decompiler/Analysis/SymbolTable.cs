using System.Collections;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Decompiler.Analysis;

public class SymbolTable : IEnumerable<Symbol>, IList<Symbol>
{
    private List<Symbol> _symbols = new();

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

    public SymbolTable Union(SymbolTable other)
        => new SymbolTable(_symbols.Union(other._symbols));

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
