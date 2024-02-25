using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Utilities;
using System.Diagnostics;
using System.Xml.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.IO;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

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

    private IEnumerable<Symbol> RootSymbols => 
        _symbols.Where(x => x.Parent == null);

    private Symbol? ClassTypeSymbol
        => _symbols.Where(x => x.Name == "Class").FirstOrDefault();

    private Symbol? FunctionTypeSymbol
        => _symbols.Where(x => x.Name == "Function").FirstOrDefault();

    public KismetAnalysisResult Analyse(UnrealPackage package)
    {
        _asset = package;
        AnalyseImports();
        AnalyseExports();
        AnalyseFunctions();
        return new KismetAnalysisResult()
        {
            AllSymbols = _symbols,
            RootSymbols = RootSymbols.ToList()
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

        // On the initial pass, simply create the symbols one-to-one based on the asset imports
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

        // Resolve parent references between import symbols
        for (int i = 0; i < importSymbols.Count; i++)
        {
            var importSymbol = importSymbols[i];
            if (!importSymbol.Import!.OuterIndex.IsNull())
            {
                var parent = importSymbols
                    .Where(x => x.ImportIndex?.Index == importSymbol.Import!.OuterIndex.Index)
                    .SingleOrDefault();
                if (parent == null)
                    throw new InvalidOperationException("Reference to non existent import index");
                importSymbol.Parent = parent;
            }
        }

        // Create symbols based on the class and class package references of the asset imports
        for (int i = 0; i < importSymbols.Count; i++)
        {
            var importSymbol = importSymbols[i];

            // Try to find an existing symbol for the class package
            var classPackage = importSymbols
                 .Union(inferredSymbols)
                 .Where(x => x.Name == importSymbol.Import!.ClassPackage.ToString())
                 .SingleOrDefault();
            if (classPackage == null)
            {
                // The class package symbol has not been explicitly imported, so we create a fake symbol for it.
                classPackage = new Symbol()
                {
                    Name = importSymbol.Import!.ClassPackage.ToString(),
                    Class = _symbols.Where(x => x.Name == "Package").SingleOrDefault()
                        ?? new Symbol() { Name = "Package", Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassPackage },
                    Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassPackage,
                };
                inferredSymbols.Add(classPackage);
            }

            // Try to find an existing symbol for the class
            var @class = importSymbols
                .Union(inferredSymbols)
                .Where(x => x.Name == importSymbol.Import?.ClassName.ToString()
                            && (x.Parent == classPackage))
                .SingleOrDefault();
            if (@class == null)
            {
                // The class symbol has not been explicitly imported, so we create a fake symbol for it.
                @class = new Symbol()
                {
                    Name = importSymbol.Import!.ClassName.ToString(),
                    Class = _symbols.Where(x => x.Name == "Class").SingleOrDefault()
                        ?? new Symbol() { Name = "Class", Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassName },
                    Parent = classPackage,
                    Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassName,
                };
                inferredSymbols.Add(@class);
            }

            // Only set the class symbol reference if it is not a self reference (ie. Class symbol with type Class)
            if (@class == importSymbol)
                Trace.WriteLine($"Class of import {importSymbol} references self");
            else
                importSymbol.Class = @class;
        }

        for (int i = 0; i < importSymbols.Count; i++)
        {
            var importSymbol = importSymbols[i];

            // If we find a symbol with the _GEN_VARIABLE prefix, we need to account
            // for some blueprint runtime magic that causes it to also create a variable
            // without the prefix.
            if (importSymbol.Name.EndsWith("_GEN_VARIABLE"))
            {
                // Make a fake symbol for the generated variable.
                var symbolClone = new Symbol()
                {
                    Name = importSymbol.Name.Replace("_GEN_VARIABLE", ""),
                    Parent = importSymbol.Parent,
                    Class = importSymbol.Class,
                    Flags = importSymbol.Flags | SymbolFlags.ClonedFromGenVariable,
                    FProperty = importSymbol.FProperty,
                    PropertyType = importSymbol.PropertyType,
                    ClonedFrom = importSymbol,
                };
                inferredSymbols.Add(symbolClone);
            }
        }

        _symbols.AddRange(importSymbols);
        _symbols.AddRange(inferredSymbols);
    }

    private void AnalyseExports()
    {
        var exportSymbols = new List<Symbol>();
        var inferredClassSymbols = new List<Symbol>();
        var propertySymbols = new List<Symbol>();

        // On the first pass, simply create a symbol for every export
        for (int i = 0; i < _asset.Exports.Count; i++)
        {
            var export = _asset.Exports[i];
            var exportIndex = FPackageIndex.FromExport(i);
            var symbol = new Symbol()
            {
                Name = export.ObjectName.ToString(),
                Export = export,
                ExportIndex = exportIndex,
                Flags = SymbolFlags.Export,
            };
            exportSymbols.Add(symbol);
        }

        // Then, resolve references between export symbols
        // No fake symbols are created this time, so a reference to a non-existing import or export is considered an error.
        for (int i = 0; i < exportSymbols.Count; i++)
        {
            var exportSymbol = exportSymbols[i];
            if (!exportSymbol.Export!.OuterIndex.IsNull())
            {
                var outer = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == exportSymbol.Export!.OuterIndex.Index ||
                                x.ImportIndex?.Index == exportSymbol.Export!.OuterIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                exportSymbol.Parent = outer;
            }

            if (!exportSymbol.Export!.ClassIndex.IsNull())
            {
                var @class = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == exportSymbol.Export!.ClassIndex.Index ||
                                x.ImportIndex?.Index == exportSymbol.Export!.ClassIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                exportSymbol.Class = @class;
            }

            if (!exportSymbol.Export!.SuperIndex.IsNull())
            {
                var super = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == exportSymbol.Export!.SuperIndex.Index ||
                                x.ImportIndex?.Index == exportSymbol.Export!.SuperIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                exportSymbol.Super = super;
            }

            if (!exportSymbol.Export!.TemplateIndex.IsNull())
            {
                var template = exportSymbols
                    .Union(_symbols)
                    .Where(x => x.ExportIndex?.Index == exportSymbol.Export!.TemplateIndex.Index ||
                                x.ImportIndex?.Index == exportSymbol.Export!.TemplateIndex.Index)
                    .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                exportSymbol.Template = template;
            }

            if (exportSymbol.Export is StructExport structExport)
            {
                if (!structExport.SuperStruct.IsNull())
                {
                    var superStruct = exportSymbols
                        .Union(_symbols)
                        .Where(x => x.ExportIndex?.Index == exportSymbol.Export!.TemplateIndex.Index ||
                                    x.ImportIndex?.Index == exportSymbol.Export!.TemplateIndex.Index)
                        .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                    exportSymbol.SuperStruct = superStruct;
                }
            }

            if (exportSymbol.Export is ClassExport classExport)
            {
                if (!classExport.ClassWithin.IsNull())
                {
                    var classWithin = exportSymbols
                        .Union(_symbols)
                        .Where(x => x.ExportIndex?.Index == exportSymbol.Export!.TemplateIndex.Index ||
                                    x.ImportIndex?.Index == exportSymbol.Export!.TemplateIndex.Index)
                        .SingleOrDefault() ?? throw new InvalidOperationException("Reference to non existent index");
                    exportSymbol.InnerClass = classWithin;
                }
            }

            if (exportSymbol.Export is EnumExport enumExport)
            {

            }

            if (exportSymbol.Export is FunctionExport functionExport)
            {

            }

            if (exportSymbol.Export is PropertyExport propertyExport)
            {
                exportSymbol.UProperty = propertyExport.Property;
                if (propertyExport.Property is UObjectProperty objectProperty)
                {
                    exportSymbol.PropertyType = _symbols.Where(x => x.ExportIndex?.Index == objectProperty.PropertyClass.Index ||
                                                               x.ImportIndex?.Index == objectProperty.PropertyClass.Index)
                                                   .SingleOrDefault();
                }
                else
                {

                }
            }
        }

        // Inference pass
        for (int i = 0; i < exportSymbols.Count; i++)
        {
            var exportSymbol = exportSymbols[i];
            if (exportSymbol.Export is StructExport structExport)
            {
                // Struct exports contain child-exports in the form of properties
                foreach (var property in structExport.LoadedProperties)
                {
                    // Try to find a symbol for the property class type
                    var classSymbol = _symbols
                        .Union(inferredClassSymbols)
                        .Where(x => x.Name == property.SerializedType.ToString()).SingleOrDefault();
                    if (classSymbol == null)
                    {
                        // The symbol has not been explicitly imported or exported, so create a fake symbol
                        classSymbol = new Symbol()
                        {
                            Name = property.SerializedType.ToString(),
                            // FIXME: is this always just class?
                            Class = _symbols.Where(x => x.Name == "Class").SingleOrDefault(),
                            Flags = SymbolFlags.InferredFromFPropertySerializedType,
                            FProperty = property,
                        };

                        // Determine parent symbol based on known class types
                        // FIXME: do this properly
                        if (classSymbol.Name.EndsWith("Property"))
                            classSymbol.Parent = _symbols.Where(x => x.Name == "/Script/CoreUObject").SingleOrDefault();
                        inferredClassSymbols.Add(classSymbol);
                    }

                    var propertySymbol = new Symbol()
                    {
                        Name = property.Name.ToString(),
                        Parent = exportSymbol,
                        Class = classSymbol,
                        Flags = SymbolFlags.FProperty,
                        FProperty = property,
                    };

                    if (property is FObjectProperty objectProperty)
                    {
                        // Resolve property class symbol
                        // FIXME: do this in the pass where other references are solved
                        propertySymbol.PropertyType = _symbols
                            .Where(x =>
                                x.ExportIndex?.Index == objectProperty.PropertyClass.Index ||
                                x.ImportIndex?.Index == objectProperty.PropertyClass.Index)
                           .SingleOrDefault();
                    }

                    propertySymbols.Add(propertySymbol);

                    if (propertySymbol.Name.EndsWith("_GEN_VARIABLE"))
                    {
                        // If the name ends with _GEN_VARIABLE, we need to create a variable without the suffix to satisfy the compiler
                        // in the same way that this is also done for imports.
                        var propertySymbolClone = new Symbol()
                        {
                            Name = propertySymbol.Name.Replace("_GEN_VARIABLE", ""),
                            Parent = propertySymbol.Parent,
                            Class = propertySymbol.Class,
                            Flags = propertySymbol.Flags | SymbolFlags.ClonedFromGenVariable,
                            FProperty = propertySymbol.FProperty,
                            PropertyType = propertySymbol.PropertyType,
                            ClonedFrom = exportSymbol,
                        };
                        propertySymbols.Add(propertySymbolClone);
                    }
                }
            }
        }

        _symbols.AddRange(exportSymbols);
        _symbols.AddRange(propertySymbols);
        _symbols.AddRange(inferredClassSymbols);
    }

    private class ExpressionVisitorFirstPass : KismetExpressionVisitor
    {
        private readonly FunctionAnalysisContext _context;
        private Symbol _instance;
        private Stack<(EX_Context Context, Symbol ContextSymbol)> _contextStack = new();

        public ExpressionVisitorFirstPass(FunctionAnalysisContext context, Symbol instance)
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

        private Symbol? EnsurePropertySymbol(KismetPropertyPointer pointer)
        {
            if (pointer.Old != null)
            {
                if (pointer.Old.IsImport())
                {
                    var import = pointer.Old.ToImport(_context.Asset)
                        ?? throw new InvalidOperationException("Invalid import");
                    var symbol = _context.Symbols.Where(x => x.Import == import).SingleOrDefault();
                    return symbol ?? throw new InvalidOperationException();
                }
                else if (pointer.Old.IsExport())
                {
                    var export = pointer.Old.ToExport(_context.Asset)
                        ?? throw new InvalidOperationException("Invalid export");
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
                if (pointer.New.Path.Length == 0) return null;
                else if (pointer.New.Path.Length != 1) throw new InvalidOperationException();
                var propertyName = pointer.New.Path[0].ToString();
                Symbol ownerSymbol;

                if (pointer.New.ResolvedOwner.IsImport())
                {
                    var import = pointer.New.ResolvedOwner.ToImport(_context.Asset)
                        ?? throw new InvalidOperationException("Invalid import");
                    ownerSymbol = _context.Symbols.Where(x => x.Import == import).SingleOrDefault()
                        ?? throw new InvalidOperationException("Invalid import");
                }
                else if (pointer.New.ResolvedOwner.IsExport())
                {
                    var export = pointer.New.ResolvedOwner.ToExport(_context.Asset)
                        ?? throw new InvalidOperationException("Invalid export");
                    ownerSymbol = _context.Symbols.Where(x => x.Export == export).SingleOrDefault()
                        ?? throw new InvalidOperationException("Invalid import");
                }
                else
                {
                    throw new InvalidOperationException();
                }

                var symbol = ownerSymbol.GetMember(propertyName);
                if (symbol == null)
                {
                    // This property has no matching symbol, so create a fake one
                    symbol = new Symbol()
                    {
                        Name = propertyName,
                        Class = _context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault(),
                        Parent = ownerSymbol,
                        Flags = SymbolFlags.InferredFromKismetPropertyPointer | SymbolFlags.UnresolvedClass,
                    };
                    _context.InferredSymbols.Add(symbol);
                }
                return symbol;
            }
        }

        private Symbol GetProperty(Symbol? context, KismetPropertyPointer pointer)
        {
            if (context != null)
            {
                // FIXME: use context?
                // Limit access to within the symbol context
                return EnsurePropertySymbol(pointer);
            }
            else
            {
                // Global symbol lookup
                return EnsurePropertySymbol(pointer);
            }
        }

        private Symbol GetContext(EX_Context ctx)
        {
            if (ctx.ObjectExpression is EX_InstanceVariable instanceVariable)
            {
                var prop = GetProperty(_instance, instanceVariable.Variable);
                return prop;
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
                var context = GetProperty(null, localVariable.Variable)
                    ?? throw new NotImplementedException();
                return context.PropertyType ?? throw new NotImplementedException();
            }
            else if (ctx.ObjectExpression is EX_Context context)
            {  
                var temp = GetContext(context)
                    ?? throw new NotImplementedException();
                return temp;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private EX_Context ActiveContext => _contextStack.Count == 0 ? null : _contextStack.Peek().Context;

        private Symbol ActiveContextSymbol => _contextStack.Count == 0 ? _instance : _contextStack.Peek().ContextSymbol;

        public override void Visit(KismetExpression expression, ref int codeOffset)
        {
            switch (expression)
            {
                case EX_ArrayConst arrayConst:
                    EnsurePropertySymbol(arrayConst.InnerProperty);
                    break;
                case EX_ClassSparseDataVariable classSparseDataVariable:
                    EnsurePropertySymbol(classSparseDataVariable.Variable);
                    break;
                case EX_Context context:
                    {
                        EnsurePropertySymbol(context.RValuePointer);
                        var contextSymbol = GetContext(context);

                        _contextStack.Push((context, contextSymbol));
                        base.Visit(context.ContextExpression);
                        _contextStack.Pop();
                        return;
                    }
                case EX_DefaultVariable defaultVariable:
                    EnsurePropertySymbol(defaultVariable.Variable);
                    break;
                case EX_InstanceVariable instanceVariable:
                    {
                        var variableSymbol = EnsurePropertySymbol(instanceVariable.Variable);
                        if (!ActiveContextSymbol.HasMember(variableSymbol))
                        {
                            if (ActiveContextSymbol.Super == null)
                            {
                                ActiveContextSymbol.Super = variableSymbol.Parent;
                            }

                            //ActiveContextSymbol.AddChild(variableSymbol);
                        }
                    }
                    break;
                case EX_Let let:
                    EnsurePropertySymbol(let.Value);
                    break;
                case EX_LetValueOnPersistentFrame letValueOnPersistentFrame:
                    EnsurePropertySymbol(letValueOnPersistentFrame.DestinationProperty);
                    break;
                case EX_LocalOutVariable localOutVariable:
                    EnsurePropertySymbol(localOutVariable.Variable);
                    break;
                case EX_LocalVariable localVariable:
                    EnsurePropertySymbol(localVariable.Variable);
                    break;
                case EX_MapConst mapConst:
                    EnsurePropertySymbol(mapConst.KeyProperty);
                    EnsurePropertySymbol(mapConst.ValueProperty);
                    break;
                case EX_PropertyConst propertyConst:
                    EnsurePropertySymbol(propertyConst.Property);
                    break;
                case EX_SetConst setConst:
                    EnsurePropertySymbol(setConst.InnerProperty);
                    break;
                case EX_StructMemberContext structMemberContext:
                    EnsurePropertySymbol(structMemberContext.StructMemberExpression);
                    break;
                case EX_FinalFunction finalFunction:
                    {
                        // Analyse final (static) function call
                        var functionSymbol =
                            _context.Symbols.Where(x =>
                                                x.ImportIndex?.Index == finalFunction.StackNode.Index ||
                                                x.ExportIndex?.Index == finalFunction.StackNode.Index)
                            .SingleOrDefault();

                        // Set class to appropriate type (Function)
                        functionSymbol.Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault();

                        // Set function signature
                        functionSymbol.FunctionMetadata.CallingConvention |= finalFunction switch
                        {
                            EX_CallMath => CallingConvention.CallMath,
                            EX_CallMulticastDelegate => CallingConvention.CallMulticastDelegate,
                            EX_LocalFinalFunction => CallingConvention.LocalFinalFunction,
                            EX_FinalFunction => CallingConvention.FinalFunction,
                        };

                        functionSymbol.FunctionMetadata.Parameters = finalFunction.Parameters
                            .Select((x, i) => new Symbol()
                            {
                                // FIXME: determine better name if it all feasible
                                Name = $"param{i}",
                                // FIXME: determine actual type
                                Class = _context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault()
                            }).ToList();

                        if (ParentExpression != null)
                        {
                            // FIXME: determine actual type
                            functionSymbol.FunctionMetadata.ReturnType = _context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault();
                        }
                        break;
                    }
                case EX_VirtualFunction virtualFunction:
                    {
                    }
                    break;
                //case EX_InstanceVariable instanceVariable:
                //    {
                //        var variableSymbol = GetProperty(ActiveContext, instanceVariable.Variable);
                //        if (variableSymbol == null)
                //        {
                //            var candidates = GetProperties(null, instanceVariable.Variable).ToList();
                //            if (candidates.Count == 1)
                //            {
                //                var symbol = ActiveContext
                //                    .GetMember(candidates.First().Name);
                //                if (symbol == null)
                //                {
                //                    _context.InferredSymbols.Add((ActiveContext, candidates.First()));
                //                }
                //            }
                //            else
                //            {
                //                //
                //            }
                //        }
                //        else
                //        {
                //            var symbol = ActiveContext
                //                .GetMember(variableSymbol.Name);
                //            if (symbol == null)
                //            {
                //                _context.InferredSymbols.Add((ActiveContext, variableSymbol));
                //            }
                //        }
                //        break;
                //    }
            }

            base.Visit(expression, ref codeOffset);
        }
    }

    private class FunctionAnalysisContext
    {
        public required UnrealPackage Asset { get; init; }
        public required List<Symbol> Symbols { get; init; }
        public required HashSet<Symbol> InferredSymbols { get; init; }
    }

    private void AnalyseFunctions()
    {
        // Walk through all functions and their instructions to do usage-based inference
        var ctx = new FunctionAnalysisContext()
        {
            Asset = _asset,
            Symbols = _symbols,
            InferredSymbols = new()
        };

        foreach (var functionSymbol in _symbols
            .Where(x => x.Export is FunctionExport))
        {
            var functionExport = (FunctionExport)functionSymbol.Export!;
            foreach (var ex in functionExport.ScriptBytecode)
            {
                var visitor = new ExpressionVisitorFirstPass(ctx, functionSymbol.Parent!);
                visitor.Visit(ex);
            }
        }

        // Resolve types of inferred symbols
        foreach (var symbol in ctx.InferredSymbols)
        {
            

            //if (item.Member.Parent == item.Context)
            //    continue;
            //else if (item.Member.Parent == null)
            //{
            //    // Found parent of orphaned symbol
            //    item.Member.Parent = item.Context;
            //}
            //else
            //{
            //    if (item.Context.Super != item.Member.Parent &&
            //        item.Context.PropertyClass?.Super != item.Member.Parent)
            //    {
            //        // If parent of member symbol is not the base class
            //    }
            //}
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
    InferredFromKismetPropertyPointer = 1 << 8,
    UnresolvedClass = 1 << 9,
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

public class SymbolFunctionMetadata
{
    public CallingConvention CallingConvention { get; set; }
    public List<Symbol> Parameters { get; set; } = new();
    public Symbol? ReturnType { get; set; }
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
    public Symbol InnerClass { get; set; }
    public Symbol? PropertyType { get; set; }
    public Symbol? ClonedFrom { get; set; }

    public SymbolFunctionMetadata FunctionMetadata { get; set; } = new();

    public bool HasMember(Symbol member)
    {
        return Children.Contains(member) ||
                (Super?.HasMember(member) ?? false) ||
                (PropertyType?.HasMember(member) ?? false) ||
                (Class?.HasMember(member) ?? false);
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
        if (PropertyType != null)
            return $"[{Flags}] {Class?.Name}<{PropertyType?.Name}> {Name}";
        else
            return $"[{Flags}] {Class?.Name} {Name}";
    }
}