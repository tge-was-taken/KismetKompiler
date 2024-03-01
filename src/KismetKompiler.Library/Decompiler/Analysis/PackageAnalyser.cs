using KismetKompiler.Library.Decompiler.Analysis.Visitors;
using KismetKompiler.Library.Utilities;
using System.Diagnostics;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.IO;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Decompiler.Analysis;

public partial class PackageAnalyser
{
    private UnrealPackage _asset;
    private SymbolTable _symbols;

    public PackageAnalysisResult Analyse(UnrealPackage package)
    {
        _asset = package;
        _symbols = new();
        AnalyseImports();
        AnalyseExports();
        AnalyseFunctions();
        return new PackageAnalysisResult()
        {
            AllSymbols = _symbols.AllSymbols.ToList(),
            RootSymbols = _symbols.RootSymbols.ToList()
        };
    }

    private SymbolType GetSymbolType(Symbol symbol)
    {
        // Handle special names first
        if (symbol.Name.StartsWith("Default__"))
            return SymbolType.ClassInstance;
        if (symbol.Name.EndsWith("_GEN_VARIABLE"))
            return SymbolType.Property;

        switch (symbol.Class?.Name)
        {
            case "Package":
                return SymbolType.Package;
            case "Function":
                return SymbolType.Function;
            default:
                if (symbol.Parent?.Type == SymbolType.Class)
                {
                    // Classes can't be nested, so this must be a class property of a class type
                    return SymbolType.Property;
                }
                else
                {
                    return SymbolType.Class;
                }
        }
    }

    private void AnalyseImports()
    {
        List<Import> imports;
        if (_asset is UAsset)
            imports = ((UAsset)_asset).Imports;
        else
            imports = ((ZenAsset)_asset).Imports.Select(x => x.ToImport((ZenAsset)_asset)).ToList();

        var importSymbols = new SymbolTable();
        var inferredSymbols = new SymbolTable();

        // On the initial pass, simply create the symbols one-to-one based on the asset imports
        for (int i = 0; i < imports.Count; i++)
        {
            var importIndex = FPackageIndex.FromImport(i);
            var importSymbol = new Symbol()
            {
                Name = imports[i].ObjectName.ToString(),
                Import = imports[i],
                ImportIndex = importIndex,
                Type = SymbolType.Unknown,
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
                 .GetSymbol(importSymbol.Import!.ClassPackage.ToString());
            if (classPackage == null)
            {
                // The class package symbol has not been explicitly imported, so we create a fake symbol for it.
                classPackage = new Symbol()
                {
                    Name = importSymbol.Import!.ClassPackage.ToString(),
                    Class = importSymbols.GetClass("Package")
                        ?? new Symbol() { Name = "Package", Type = SymbolType.Class, Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassPackage },
                    Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassPackage,
                    Type = SymbolType.Package,
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
                    Class = importSymbol.Import!.ClassName.ToString() == "Class" ? null :
                        importSymbols.GetClass("Class")
                        ?? new Symbol() { Name = "Class", Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassName },
                    Parent = classPackage,
                    Flags = SymbolFlags.Import | SymbolFlags.InferredFromImportClassName,
                    Type = SymbolType.Class
                };
                inferredSymbols.Add(@class);
            }

            // Only set the class symbol reference if it is not a self reference (ie. Class symbol with type Class)
            if (@class == importSymbol)
                Trace.WriteLine($"Class of import {importSymbol} references self");
            else
                importSymbol.Class = @class;
        }

        // Determine symbol types using BFS
        var queue = new Queue<Symbol>();
        foreach (var symbol in importSymbols.Where(x => x.Parent == null))
            queue.Enqueue(symbol);
        while (queue.Count > 0)
        {
            var symbol = queue.Dequeue();
            symbol.Type = GetSymbolType(symbol);
            foreach (var child in symbol.Children)
                queue.Enqueue(child);
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
                    Type = importSymbol.Type,
                    FProperty = importSymbol.FProperty,
                    PropertyClass = importSymbol.PropertyClass,
                    ClonedFrom = importSymbol,
                };
                inferredSymbols.Add(symbolClone);
            }
        }

        _symbols.Join(importSymbols);
        _symbols.Join(inferredSymbols);
    }

    private void AnalyseExports()
    {
        var exportSymbols = new SymbolTable();
        var inferredClassSymbols = new SymbolTable();
        var propertySymbols = new SymbolTable();

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
                Type = SymbolType.Unknown,
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
                    exportSymbol.ClassWithin = classWithin;
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
                if (propertyExport.Property is UEnumProperty enumProperty)
                {
                    exportSymbol.Enum = _symbols.GetSymbolByPackageIndex(enumProperty.Enum); 
                    exportSymbol.UnderlyingProp = _symbols.GetSymbolByPackageIndex(enumProperty.UnderlyingProp); 
                }
                if (propertyExport.Property is UArrayProperty arrayProperty)
                {
                    exportSymbol.Inner = _symbols.GetSymbolByPackageIndex(arrayProperty.Inner);
                }
                if (propertyExport.Property is USetProperty setProperty)
                {
                    exportSymbol.ElementProp = _symbols.GetSymbolByPackageIndex(setProperty.ElementProp);
                }
                if (propertyExport.Property is UObjectProperty objectProperty)
                {
                    exportSymbol.PropertyClass = _symbols.GetSymbolByPackageIndex(objectProperty.PropertyClass);
                }
                if (propertyExport.Property is USoftClassProperty softClassProperty)
                {
                    exportSymbol.MetaClass = _symbols.GetSymbolByPackageIndex(softClassProperty.MetaClass);
                }
                if (propertyExport.Property is UDelegateProperty delegateProperty)
                {
                    exportSymbol.SignatureFunction = _symbols.GetSymbolByPackageIndex(delegateProperty.SignatureFunction);
                }
                if (propertyExport.Property is UInterfaceProperty interfaceProperty)
                {
                    exportSymbol.InterfaceClass = _symbols.GetSymbolByPackageIndex(interfaceProperty.InterfaceClass);
                }
                if (propertyExport.Property is UMapProperty mapProperty)
                {
                    exportSymbol.KeyProp = _symbols.GetSymbolByPackageIndex(mapProperty.KeyProp);
                    exportSymbol.ValueProp = _symbols.GetSymbolByPackageIndex(mapProperty.ValueProp);
                }
                if (propertyExport.Property is UByteProperty byteProperty)
                {
                    exportSymbol.Enum = _symbols.GetSymbolByPackageIndex(byteProperty.Enum);
                }
                if (propertyExport.Property is UStructProperty structProperty)
                {
                    exportSymbol.Struct = _symbols.GetSymbolByPackageIndex(structProperty.Struct);
                }
            }
        }

        // Determine symbol types using BFS
        var queue = new Queue<Symbol>();
        foreach (var symbol in exportSymbols.Where(x => x.Parent == null))
            queue.Enqueue(symbol);
        while (queue.Count > 0)
        {
            var symbol = queue.Dequeue();
            symbol.Type = GetSymbolType(symbol);
            foreach (var child in symbol.Children)
                queue.Enqueue(child);
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
                        .Union(exportSymbols)
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
                            Type = SymbolType.Class,
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
                        Type = SymbolType.Property,
                        FProperty = property,
                    };

                    if (property is FObjectProperty objectProperty)
                    {
                        if (objectProperty.PropertyClass.IsNull())
                            throw new AnalysisException($"Property class is null for property {property.Name}");

                        // Resolve property class symbol
                        // FIXME: do this in the pass where other references are solved
                        var propertyClassSymbol = _symbols
                            .Union(exportSymbols)
                            .Union(inferredClassSymbols)
                            .GetSymbolByPackageIndex(objectProperty.PropertyClass);
                        if (propertyClassSymbol == null)
                            throw new AnalysisException($"No symbol found for property class {_asset.GetName(objectProperty.PropertyClass)}");
                        propertySymbol.PropertyClass = propertyClassSymbol;
                    }
                    if (property is FInterfaceProperty interfaceProperty)
                    {
                        if (interfaceProperty.InterfaceClass.IsNull())
                            throw new AnalysisException($"Interface class is null for property {property.Name}");

                        // Resolve property class symbol
                        // FIXME: do this in the pass where other references are solved
                        var interfaceClassSymbol = _symbols
                            .Union(exportSymbols)
                            .Union(inferredClassSymbols)
                            .GetSymbolByPackageIndex(interfaceProperty.InterfaceClass);
                        if (interfaceProperty == null)
                            throw new AnalysisException($"No symbol found for property class {_asset.GetName(interfaceProperty.InterfaceClass)}");
                        propertySymbol.InterfaceClass = interfaceClassSymbol;
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
                            Type = propertySymbol.Type,
                            FProperty = propertySymbol.FProperty,
                            PropertyClass = propertySymbol.PropertyClass,
                            ClonedFrom = exportSymbol,
                        };
                        propertySymbols.Add(propertySymbolClone);
                    }
                }
            }
        }

        _symbols.Join(exportSymbols);
        _symbols.Join(propertySymbols);
        _symbols.Join(inferredClassSymbols);
    }

    private void AnalyseFunctions()
    {
        // Walk through all functions and their instructions to do usage-based inference
        var ctx = new FunctionAnalysisContext()
        {
            Asset = _asset,
            Symbols = _symbols,
            InferredSymbols = new(),
            UnexpectedMemberAccesses = new(),
        };

        void ExecuteVisitorPass(Func<Symbol, KismetExpressionVisitor> visitorFactory)
        {
            foreach (var functionSymbol in _symbols
                .Where(x => x.Export is FunctionExport))
            {
                var functionExport = (FunctionExport)functionSymbol.Export!;
                var visitor = visitorFactory(functionSymbol);
                foreach (var ex in functionExport.ScriptBytecode)
                    visitor.Visit(ex);
            }
        }

        ExecuteVisitorPass((function) => new CreateKismetPropertyPointerSymbolsVisitor(ctx));
        ExecuteVisitorPass((function) => new MemberAccessTrackingVisitor(ctx, function.Parent!));


        // Resolve types of inferred symbols through their usage
        foreach (var group in ctx.UnexpectedMemberAccesses
            .GroupBy(x => x.ContextSymbol))
        {
            var contextSymbol = group.Key;
            foreach (var memberAccess in group)
            {
                if (contextSymbol.HasMember(memberAccess.VariableSymbol))
                    continue;

                if (contextSymbol.Flags.HasFlag(SymbolFlags.UnresolvedClass))
                {
                    // The context's class is unresolved, likely because it was inferred from
                    // a KismetPropertyPointer
                    // Try to guess variable type based on what members have been accessed
                    // FIXME: pick best candidate instead of first
                    var classSymbol = _symbols.Where(x =>
                        x.HasMember(memberAccess.VariableSymbol.Name)).ToList();
                    if (classSymbol.Any())
                    {
                        contextSymbol.Class = classSymbol.First();
                        contextSymbol.Flags &= ~SymbolFlags.UnresolvedClass;
                    }
                }
                else
                {
                    // The context a known symbol. Might be a virtual call, or a missing base class.
                    if (memberAccess.VariableExpression is EX_VirtualFunction virtualFunction)
                    {
                        // Virtual calls are done by name, and are not necessarily imported
                        // We assume the virtual function belongs to the base class
                        if (!contextSymbol.HasMember(memberAccess.VariableSymbol.Name))
                        {
                            if (contextSymbol.PropertyClass != null)
                            {
                                contextSymbol.PropertyClass.AddChild(memberAccess.VariableSymbol);
                            }
                            else if (contextSymbol.InterfaceClass != null)
                            {
                                contextSymbol.InterfaceClass.AddChild(memberAccess.VariableSymbol);
                            }
                            else if (contextSymbol.Super != null)
                            {
                                contextSymbol.Super.AddChild(memberAccess.VariableSymbol);
                            }
                            else if (contextSymbol.Class?.Name != "Class")
                            {
                                contextSymbol.Class!.AddChild(memberAccess.VariableSymbol);
                            }
                            else
                            {
                                contextSymbol.AddChild(memberAccess.VariableSymbol);
                            }
                        }
                    }
                    else if (memberAccess.VariableExpression is EX_FinalFunction finalFunction)
                    {
                        // When an unexpected call to a final function happens, it's very likely
                        // that the function is defined in the base class, but the base class itself
                        // has not been properly assigned to the context class
                        // TODO: properly assigned base class instead of adding the members
                        if (!contextSymbol.HasMember(memberAccess.VariableSymbol.Name))
                        {
                            if (contextSymbol.PropertyClass != null)
                            {
                                contextSymbol.PropertyClass.AddChild(memberAccess.VariableSymbol);
                            }
                            else if (contextSymbol.InterfaceClass != null)
                            {
                                contextSymbol.InterfaceClass.AddChild(memberAccess.VariableSymbol);
                            }
                            else if (contextSymbol.Super != null)
                            {
                                contextSymbol.Super.AddChild(memberAccess.VariableSymbol);
                            }
                            else if (contextSymbol.Class?.Name != "Class")
                            {
                                contextSymbol.Class!.AddChild(memberAccess.VariableSymbol);
                            }
                            else
                            {
                                contextSymbol.AddChild(memberAccess.VariableSymbol);
                            }
                        }
                    }
                    else if (memberAccess.VariableExpression is EX_InstanceVariable instanceVariable)
                    {
                        // FIXME: prevent wrong base classes from being assigned here
                        // If an instance member on the context has been accessed, but it doesn't exist in the class
                        // it must be part of a base class that is not assigned properly
                        //var rootBaseClass = contextSymbol.Super ?? contextSymbol;
                        //while (rootBaseClass?.Super != null)
                        //    rootBaseClass = rootBaseClass.Super;

                        //var owningClassSymbol = _symbols.Where(x =>
                        //    x != rootBaseClass &&
                        //    x.HasMember(memberAccess.VariableSymbol.Name)).ToList();
                        //if (owningClassSymbol.Any())
                        //{
                        //    rootBaseClass.Super = owningClassSymbol.First();
                        //}
                    }
                }
            }
        }


        // For the remainder of the unresolved symbols, we generate
        // fake classes which contain the accessed members to satisfy the compiler
        foreach (var sym in ctx.Symbols
            .Union(ctx.InferredSymbols)
            .Where(x => x.Flags.HasFlag(SymbolFlags.UnresolvedClass))
            .ToList())
        {
            var members = ctx.UnexpectedMemberAccesses.Where(x => x.ContextSymbol == sym)
                .Select(x => x.VariableSymbol)
                .DistinctBy(x => x.Name)
                .ToList();

            if (members.Any())
            {
                var fakeClass = new Symbol()
                {
                    Name = $"{sym.Name}FakeClass",
                    Class = _symbols.Where(x => x.Name == "Class").FirstOrDefault(),
                    Flags = SymbolFlags.FakeClass | SymbolFlags.Import,
                    Type = SymbolType.Class,
                };
                foreach (var member in members)
                {
                    fakeClass.AddChild(member);
                }
                fakeClass.Parent = sym.Parent.Parent;

                sym.Class = fakeClass;
                sym.Flags &= ~SymbolFlags.UnresolvedClass;
                ctx.InferredSymbols.Add(fakeClass);
            }
        }

        // With all the classes hopefully mapped out

        _symbols.Join(ctx.InferredSymbols);
    }
}
