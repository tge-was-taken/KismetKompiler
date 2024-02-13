using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Packaging;
using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Utilities;
using System.Linq.Expressions;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.IO;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace KismetKompiler.Library.Decompiler.Analysis;

public class KismetAnalysisResult
{
    public required IReadOnlyList<Symbol> AllSymbols { get; init; }
    public required IReadOnlyList<Symbol> RootSymbols { get; init; }
}

public class KismetAnalyser
{
    private UnrealPackage _asset;
    private List<Symbol> _symbols = new();

    public KismetAnalysisResult Analyse(UnrealPackage package)
    {
        _asset = package;
        AnalyseImports();
        AnalyseExports();
        AnalyseFunctions();
        var rootSymbols = _symbols.Where(x => x.Parent == null).ToList();
        return new KismetAnalysisResult()
        {
            AllSymbols = _symbols,
            RootSymbols = rootSymbols
        };
    }

    private void AnalyseImports()
    {
        List<Import> imports;
        if (_asset is UAsset)
            imports = ((UAsset)_asset).Imports;
        else
            imports = ((ZenAsset)_asset).Imports.Select(x => x.ToImport((ZenAsset)_asset)).ToList();

        var importSymbols = new List<Symbol>();
        var inferredSymbols = new List<Symbol>();

        void ResolveParent(Symbol symbol)
        {
            if (!symbol.Import!.OuterIndex.IsNull())
            {
                var parent = importSymbols
                    .Where(x => x.ImportIndex?.Index == symbol.Import!.OuterIndex.Index)
                    .SingleOrDefault();
                if (parent == null)
                    throw new InvalidOperationException("Reference to non existent import index");
                symbol.Parent = parent;
            }
        }

        void ResolveClass(Symbol symbol)
        {
            var classPackage = importSymbols
                .Union(inferredSymbols)
                .Where(x => x.Name == symbol.Import!.ClassPackage.ToString())
                .SingleOrDefault();
            if (classPackage == null)
            {
                classPackage = new Symbol()
                {
                    Name = symbol.Import!.ClassPackage.ToString(),
                    Class = _symbols.Where(x => x.Name == "Package").SingleOrDefault() 
                        ?? new Symbol() { Name = "Package", Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassPackage },
                    Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassPackage,
                };
                inferredSymbols.Add(classPackage);
            }

            var @class = importSymbols
                .Union(inferredSymbols)
                .Where(x => x.Name == symbol.Import?.ClassName.ToString()
                            && (x.Parent == classPackage))
                .SingleOrDefault();
            if (@class == null)
            {
                @class = new Symbol()
                {
                    Name = symbol.Import!.ClassName.ToString(),
                    Class = _symbols.Where(x => x.Name == "Class").SingleOrDefault() 
                        ?? new Symbol() { Name = "Class", Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassName },
                    Parent = classPackage,
                    Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassName,
                };
                inferredSymbols.Add(@class);
            }

            if (@class != symbol)
                symbol.Class = @class;
        }

        void AnalyseCompleteImportSymbol(Symbol symbol)
        {
            if (symbol.Name.EndsWith("_GEN_VARIABLE"))
            {
                var symbolClone = new Symbol()
                {
                    Name = symbol.Name.Replace("_GEN_VARIABLE", ""),
                    Parent = symbol.Parent,
                    Class = symbol.Class,
                    Flags = symbol.Flags | SymbolFlags.ClonedFromGenVariable,
                    FProperty = symbol.FProperty,
                    PropertyClass = symbol.PropertyClass,
                    ClonedFrom = symbol,
                };
                importSymbols.Add(symbolClone);
            }
        }

        for (int i = 0; i < imports.Count; i++)
        {
            var importIndex = FPackageIndex.FromImport(i);
            var importSymbol = new Symbol()
            {
                Name = imports[i].ObjectName.ToString(),
                Import = imports[i],
                ImportIndex = importIndex,
                Flags = SymbolFlags.Import,
            };
            importSymbols.Add(importSymbol);
        }

        for (int i = 0; i < importSymbols.Count; i++)
            ResolveParent(importSymbols[i]);

        for (int i = 0; i < importSymbols.Count; i++)
            ResolveClass(importSymbols[i]);

        for (int i = 0; i < importSymbols.Count; i++)
            AnalyseCompleteImportSymbol(importSymbols[i]);

        _symbols.AddRange(importSymbols);
        _symbols.AddRange(inferredSymbols);
    }

    private void AnalyseExports()
    {
        var exportSymbols = new List<Symbol>();
        var inferredClassSymbols = new List<Symbol>();
        var propertySymbols = new List<Symbol>();

        void CreateExportSymbols(Export export, FPackageIndex exportIndex)
        {
            var symbol = new Symbol()
            {
                Name = export.ObjectName.ToString(),
                Export = export,
                ExportIndex = exportIndex,
                Flags = SymbolFlags.Export,
            };
            exportSymbols.Add(symbol);

            if (export is StructExport structExport)
            {
                foreach (var property in structExport.LoadedProperties)
                {
                    var classSymbol = _symbols
                        .Union(inferredClassSymbols)
                        .Where(x => x.Name == property.SerializedType.ToString()).SingleOrDefault();
                    if (classSymbol == null)
                    {
                        classSymbol = new Symbol()
                        {
                            Name = property.SerializedType.ToString(),
                            Class = _symbols.Where(x => x.Name == "Class").SingleOrDefault(),
                            Flags = SymbolFlags.InferredFromFPropertySerializedType,
                            FProperty = property,
                        };
                        if (classSymbol.Name.EndsWith("Property"))
                            classSymbol.Parent = _symbols.Where(x => x.Name == "/Script/CoreUObject").SingleOrDefault();
                        inferredClassSymbols.Add(classSymbol);
                    }

                    var propertySymbol = new Symbol()
                    {
                        Name = property.Name.ToString(),
                        Parent = symbol,
                        Class = classSymbol,
                        Flags = SymbolFlags.FProperty,
                        FProperty = property,
                    };

                    if (property is FObjectProperty objectProperty)
                    {
                        propertySymbol.PropertyClass = _symbols
                            .Where(x =>
                                x.ExportIndex?.Index == objectProperty.PropertyClass.Index ||
                                x.ImportIndex?.Index == objectProperty.PropertyClass.Index)
                           .SingleOrDefault();
                    }

                    propertySymbols.Add(propertySymbol);

                    if (propertySymbol.Name.EndsWith("_GEN_VARIABLE"))
                    {
                        var propertySymbolClone = new Symbol()
                        {
                            Name = propertySymbol.Name.Replace("_GEN_VARIABLE", ""),
                            Parent = propertySymbol.Parent,
                            Class = propertySymbol.Class,
                            Flags = propertySymbol.Flags | SymbolFlags.ClonedFromGenVariable,
                            FProperty = propertySymbol.FProperty,
                            PropertyClass = propertySymbol.PropertyClass,
                            ClonedFrom = symbol,
                        };
                        propertySymbols.Add(propertySymbolClone);
                    }
                }
            }
        }

        void ResolveExportReferences(Symbol symbol)
        {
            if (!symbol.Export!.OuterIndex.IsNull())
            {
                var outer = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == symbol.Export!.OuterIndex.Index ||
                                x.ImportIndex?.Index == symbol.Export!.OuterIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                symbol.Parent = outer;
            }

            if (!symbol.Export!.ClassIndex.IsNull())
            {
                var @class = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == symbol.Export!.ClassIndex.Index ||
                                x.ImportIndex?.Index == symbol.Export!.ClassIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                symbol.Class = @class;
            }

            if (!symbol.Export!.SuperIndex.IsNull())
            {
                var super = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == symbol.Export!.SuperIndex.Index ||
                                x.ImportIndex?.Index == symbol.Export!.SuperIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                symbol.Super = super;
            }

            if (!symbol.Export!.TemplateIndex.IsNull())
            {
                var template = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == symbol.Export!.TemplateIndex.Index ||
                                x.ImportIndex?.Index == symbol.Export!.TemplateIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                symbol.Template = template;
            }

            if (symbol.Export is StructExport structExport)
            {
                if (!structExport.SuperStruct.IsNull())
                {
                    var superStruct = exportSymbols
                        .Union(_symbols)
                        .Where(x => x.ExportIndex?.Index == symbol.Export!.TemplateIndex.Index ||
                                    x.ImportIndex?.Index == symbol.Export!.TemplateIndex.Index)
                        .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                    symbol.SuperStruct = superStruct;
                }
            }

            if (symbol.Export is ClassExport classExport)
            {
                if (!classExport.ClassWithin.IsNull())
                {
                    var classWithin = exportSymbols
                        .Union(_symbols)
                        .Where(x => x.ExportIndex?.Index == symbol.Export!.TemplateIndex.Index ||
                                    x.ImportIndex?.Index == symbol.Export!.TemplateIndex.Index)
                        .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                    symbol.ClassWithin = classWithin;
                }
            }

            if (symbol.Export is EnumExport enumExport)
            {

            }

            if (symbol.Export is FunctionExport functionExport)
            {

            }

            if (symbol.Export is PropertyExport propertyExport)
            {
                symbol.UProperty = propertyExport.Property;
                if (propertyExport.Property is UObjectProperty objectProperty)
                {
                    symbol.PropertyClass = _symbols.Where(x => x.ExportIndex?.Index == objectProperty.PropertyClass.Index ||
                                                               x.ImportIndex?.Index == objectProperty.PropertyClass.Index)
                                                   .SingleOrDefault();
                }
                else
                {

                }
            }
        }

        for (int i = 0; i < _asset.Exports.Count; i++)
        {
            var export = _asset.Exports[i];
            var exportIndex = FPackageIndex.FromExport(i);
            CreateExportSymbols(export, exportIndex);
        }

        for (int i = 0; i < exportSymbols.Count; i++)
            ResolveExportReferences(exportSymbols[i]);

        _symbols.AddRange(exportSymbols);
        _symbols.AddRange(propertySymbols);
        _symbols.AddRange(inferredClassSymbols);
    }

    private class Visitor : KismetExpressionVisitor
    {
        private readonly FunctionAnalysisContext _context;
        private Symbol _instance;
        private Stack<(EX_Context Context, Symbol ContextSymbol)> _contextStack = new();

        public Visitor(FunctionAnalysisContext context, Symbol instance)
        {
            _context = context;
            _instance = instance;
        }

        private IEnumerable<Symbol> GetProperties(Symbol? context, KismetPropertyPointer pointer)
        {
            if (context != null)
            {
                if (pointer.Old != null)
                {
                    if (pointer.Old.IsImport())
                    {
                        var import = pointer.Old.ToImport(_context.Asset);
                        return _context.Symbols.Where(x => x.Import == import);
                    }
                    else if (pointer.Old.IsExport())
                    {
                        var export = pointer.Old.ToExport(_context.Asset);
                        return _context.Symbols.Where(x => x.Export == export);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    if (pointer.New.Path.Length != 1) throw new NotImplementedException();
                    return new[] { context.GetMember(pointer.New.Path[0].ToString()) };
                }
            }
            else
            {
                if (pointer.Old != null)
                {
                    if (pointer.Old.IsImport())
                    {
                        var import = pointer.Old.ToImport(_context.Asset);
                        return _context.Symbols.Where(x => x.Import == import);
                    }
                    else if (pointer.Old.IsExport())
                    {
                        var export = pointer.Old.ToExport(_context.Asset);
                        return _context.Symbols.Where(x => x.Export == export);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    if (pointer.New.Path.Length != 1) throw new NotImplementedException();
                    return _context.Symbols
                        .Where(x => x.Name == pointer.New.Path[0].ToString())
                        .Union(
                            _context.Symbols
                                .Select(x => x.GetMember(pointer.New.Path[0].ToString()))
                                .Where(x => x != null)
                        );
                }
            }
        }

        private Symbol GetProperty(Symbol? context, KismetPropertyPointer pointer)
        {
            if (context != null)
            {
                if (pointer.Old != null)
                {
                    if (pointer.Old.IsImport())
                    {
                        var import = pointer.Old.ToImport(_context.Asset);
                        var symbol = _context.Symbols.Where(x => x.Import == import).SingleOrDefault();
                        return symbol ?? throw new InvalidOperationException();
                    }
                    else if (pointer.Old.IsExport())
                    {
                        var export = pointer.Old.ToExport(_context.Asset);
                        var symbol = _context.Symbols.Where(x => x.Export == export).SingleOrDefault();
                        return symbol ?? throw new InvalidOperationException();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    if (pointer.New.Path.Length != 1) throw new NotImplementedException();
                    return context.GetMember(pointer.New.Path[0].ToString());
                }
            }
            else
            {
                if (pointer.Old != null)
                {
                    if (pointer.Old.IsImport())
                    {
                        var import = pointer.Old.ToImport(_context.Asset);
                        var symbol = _context.Symbols.Where(x => x.Import == import).SingleOrDefault();
                        return symbol ?? throw new InvalidOperationException();
                    }
                    else if (pointer.Old.IsExport())
                    {
                        var export = pointer.Old.ToExport(_context.Asset);
                        var symbol = _context.Symbols.Where(x => x.Export == export).SingleOrDefault();
                        return symbol ?? throw new InvalidOperationException();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    if (pointer.New.Path.Length != 1) throw new NotImplementedException();
                    return _context.Symbols
                        .Where(x => x.Name == pointer.New.Path[0].ToString())
                        .SingleOrDefault()
                        ?? throw new InvalidOperationException();
                }
            }
        }

        private Symbol GetContext(EX_Context ctx)
        {
            if (ctx.ObjectExpression is EX_InstanceVariable instanceVariable)
            {
                return GetProperty(_instance, instanceVariable.Variable);
            }
            else if (ctx.ObjectExpression is EX_ObjectConst objectConst)
            {
                if (objectConst.Value.IsExport())
                {
                    var context = _context.Symbols
                        .Where(x => x.ExportIndex?.Index == objectConst.Value.Index)
                        .SingleOrDefault()
                        ?? throw new NotImplementedException();
                    return context;
                }
                else if (objectConst.Value.IsImport())
                {
                    var context = _context.Symbols
                        .Where(x => x.ImportIndex?.Index == objectConst.Value.Index)
                        .SingleOrDefault()
                        ?? throw new NotImplementedException();
                    return context;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (ctx.ObjectExpression is EX_LocalVariable localVariable)
            {
                var context = GetProperty(null, localVariable.Variable);
                return context;
            }
            else if (ctx.ObjectExpression is EX_Context context)
            {
                var temp = GetContext(context);
                return temp;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private Symbol ActiveContext => (_contextStack.Count != 0 ? _contextStack.Peek().ContextSymbol : _instance);

        public override void Visit(KismetExpression expression, ref int codeOffset)
        {
            var enteredContext = false;
            if (expression is EX_Context context)
            {
                var contextSymbol = GetContext(context);
                _contextStack.Push((context, contextSymbol));
                enteredContext = true;
            }
            else if (expression is EX_StructMemberContext)
            {

            }

            if (expression is EX_VirtualFunction virtualFunction)
            {
                var isLocal = virtualFunction is EX_LocalVirtualFunction;
                var symbol = ActiveContext
                    .GetMember(virtualFunction.VirtualFunctionName.ToString());
                if (symbol == null)
                {
                    // TODO: can we determine the base class here?
                    symbol = new Symbol()
                    {
                        Name = virtualFunction.VirtualFunctionName.ToString(),
                        Class = _context.Symbols.Where(x => x.Name == "Function").SingleOrDefault(),
                        Flags = SymbolFlags.InferredFromCall,
                    };
                    _context.InferredSymbols.Add((ActiveContext, symbol));
                }

                symbol.CallingConvention |= isLocal ? CallingConvention.LocalVirtualFunction : CallingConvention.VirtualFunction;
            }
            else if (expression is EX_FinalFunction finalFunction)
            {
                var functionSymbol =
                    _context.Symbols.Where(x => 
                                        x.ImportIndex?.Index == finalFunction.StackNode.Index ||
                                        x.ExportIndex?.Index == finalFunction.StackNode.Index)
                    .SingleOrDefault();

                functionSymbol!.CallingConvention |= finalFunction switch
                {
                    EX_CallMath => CallingConvention.CallMath,
                    EX_CallMulticastDelegate => CallingConvention.CallMulticastDelegate,
                    EX_LocalFinalFunction => CallingConvention.LocalFinalFunction,
                    EX_FinalFunction => CallingConvention.FinalFunction,
                };

                if (finalFunction is not EX_CallMath)
                {
                    var symbol = ActiveContext
                        .GetMember(functionSymbol.Name);
                    if (symbol == null)
                    {
                        _context.InferredSymbols.Add((ActiveContext, functionSymbol));
                    }
                }
            }
            else if (expression is EX_InstanceVariable instanceVariable 
                && (_contextStack.Count == 0 || instanceVariable != _contextStack.Peek().Context.ObjectExpression))
            {
                var variableSymbol = GetProperty(ActiveContext, instanceVariable.Variable);
                if (variableSymbol == null)
                {
                    var candidates = GetProperties(null, instanceVariable.Variable).ToList();
                    if (candidates.Count == 1)
                    {
                        var symbol = ActiveContext
                            .GetMember(candidates.First().Name);
                        if (symbol == null)
                        {
                            _context.InferredSymbols.Add((ActiveContext, candidates.First()));
                        }
                    }
                    else
                    {
                        //
                    }
                }
                else
                {
                    var symbol = ActiveContext
                        .GetMember(variableSymbol.Name);
                    if (symbol == null)
                    {
                        _context.InferredSymbols.Add((ActiveContext, variableSymbol));
                    }
                }
            }

            base.Visit(expression, ref codeOffset);

            if (enteredContext)
                _contextStack.Pop();
        }
    }

    private class FunctionAnalysisContext
    {
        public required UnrealPackage Asset { get; init; }
        public required List<Symbol> Symbols { get; init; }
        public required HashSet<(Symbol Context, Symbol Member)> InferredSymbols { get; init; }
    }

    private void AnalyseFunctions()
    {
        var ctx = new FunctionAnalysisContext()
        {
            Asset = _asset,
            Symbols = _symbols,
            InferredSymbols = new()
        };

        foreach (var functionSymbol in _symbols.Where(x => x.Export is FunctionExport))
        {
            var functionExport = (FunctionExport)functionSymbol.Export!;
            foreach (var ex in functionExport.ScriptBytecode)
            {
                var visitor = new Visitor(ctx, functionSymbol.Parent!);
                visitor.Visit(ex);
            }
        }

        foreach (var item in ctx.InferredSymbols)
        {
            if (item.Member.Parent == item.Context)
                continue;
            else if (item.Member.Parent == null)
            {
                // Found parent of orphaned symbol
                item.Member.Parent = item.Context;
            }
            else
            {
                if (item.Context.Super != item.Member.Parent &&
                    item.Context.PropertyClass?.Super != item.Member.Parent)
                {
                    // If parent of member symbol is not the base class
                }
            }
        }
    }
}

[Flags]
public enum SymbolFlags
{
    Import = 1<<0,
    Export = 1<<1,
    FProperty = 1 << 4,
    InferredFromImportClassPackage = 1<<2,
    InferredFromImportClassName = 1<<3,
    InferredFromFPropertySerializedType = 1 << 5,
    InferredFromCall = 1 << 6,
    ClonedFromGenVariable = 1 << 7,
}

[Flags]
public enum CallingConvention
{
    CallMath = 1<<0,
    LocalVirtualFunction = 1<<1,
    LocalFinalFunction = 1<<2,
    VirtualFunction = 1<<3,
    FinalFunction = 1<<4,
    CallMulticastDelegate = 1 << 5
}

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
    
    public Import? Import { get; set; }
    public FPackageIndex? ImportIndex { get; set; }

    public virtual Export? Export { get; set; }
    public FPackageIndex? ExportIndex { get; set; }

    public FProperty FProperty { get; set; }
    public UProperty UProperty { get; set; }

    public Symbol? Class { get; set; }
    public SymbolFlags Flags { get; set; }
    public Symbol? Super { get; set; }
    public Symbol? Template { get; set; }
    public Symbol SuperStruct { get; set; }
    public Symbol ClassWithin { get; set; }
    public Symbol? PropertyClass { get; set; }
    public Symbol? ClonedFrom { get; set; }

    public CallingConvention CallingConvention { get; set; }

    public Symbol? GetMember(string name)
    {
        return Children.Where(x => x.Name == name).SingleOrDefault() 
            ?? PropertyClass?.GetMember(name) 
            ?? Class?.GetMember(name);
    }

    public void AddChild(Symbol child)
    {
        if (child.Parent == this)
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
            return $"{Class?.Name}<{PropertyClass?.Name}> {Name}";
        else
            return $"{Class?.Name} {Name}";
    }
}