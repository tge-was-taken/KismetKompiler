﻿using KismetKompiler.Compiler;
using KismetKompiler.Library.Compiler;
using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Compiler.Exceptions;
using KismetKompiler.Library.Compiler.Intermediate;
using KismetKompiler.Library.Syntax.Statements.Declarations;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Packaging;

public abstract class PackageLinker
{
    //public abstract FName AddName(string name);

    public abstract PackageLinker LinkCompiledScript(CompiledScriptContext scriptContext);
    public abstract UnrealPackage Build();
}

public record ImportIndex(
    Import Import,
    FPackageIndex Index);

public record PackageImport(FPackageIndex Index, Import Import);

public record PackageExport<T>(FPackageIndex Index, T Export) where T : Export;

public class UAssetLinker : PackageLinker
{
    private UAsset _asset;

    public UAssetLinker()
    {
        _asset = CreateDefaultAsset();
    }

    public UAssetLinker(UAsset asset)
    {
        _asset = asset;
    }

    private UAsset CreateDefaultAsset()
    {
        var asset = new UAsset()
        {
            LegacyFileVersion = -7,
            UsesEventDrivenLoader = true,
            Imports = new(),
            DependsMap = new(),
            SoftPackageReferenceList = new(),
            AssetRegistryData = new byte[] { 0, 0, 0, 0 },
            ValorantGarbageData = null,
            Generations = new(),
            PackageGuid = Guid.NewGuid(),
            RecordedEngineVersion = new()
            {
                Major = 0,
                Minor = 0,
                Patch = 0,
                Changelist = 0,
                Branch = null
            },
            RecordedCompatibleWithEngineVersion = new()
            {
                Major = 0,
                Minor = 0,
                Patch = 0,
                Changelist = 0,
                Branch = null
            },
            ChunkIDs = Array.Empty<int>(),
            PackageSource = 4048401688,
            FolderName = new("None"),
            GatherableTextDataCount = 0,
            GatherableTextDataOffset = 0,
            SearchableNamesOffset = 0,
            ThumbnailTableOffset = 0,
            CompressionFlags = 0,
            AdditionalPackagesToCook = new(),
            NamesReferencedFromExportDataCount = 0,
            PayloadTocOffset = 0,
            DataResourceOffset = 0,
            IsUnversioned = true,
            FileVersionLicenseeUE = 0,
            ObjectVersion = ObjectVersion.VER_UE4_FIX_WIDE_STRING_CRC,
            ObjectVersionUE5 = ObjectVersionUE5.UNKNOWN,
            CustomVersionContainer = new()
            {
                new(){ Key = Guid.Parse("{375EC13C-06E4-48FB-B500-84F0262A717E}"), FriendlyName = "FCoreObjectVersion", Version = 3, IsSerialized = false },
                new(){ Key = Guid.Parse("{E4B068ED-F494-42E9-A231-DA0B2E46BB41}"), FriendlyName = "FEditorObjectVersion", Version = 34, IsSerialized = false },
                new(){ Key = Guid.Parse("{CFFC743F-43B0-4480-9391-14DF171D2073}"), FriendlyName = "FFrameworkObjectVersion", Version = 35, IsSerialized = false },
                new(){ Key = Guid.Parse("{7B5AE74C-D270-4C10-A958-57980B212A5A}"), FriendlyName = "FSequencerObjectVersion", Version = 11, IsSerialized = false },
                new(){ Key = Guid.Parse("{29E575DD-E0A3-4627-9D10-D276232CDCEA}"), FriendlyName = "FAnimPhysObjectVersion", Version = 17, IsSerialized = false },
                new(){ Key = Guid.Parse("{601D1886-AC64-4F84-AA16-D3DE0DEAC7D6}"), FriendlyName = "FFortniteMainBranchObjectVersion", Version = 27, IsSerialized = false },
                new(){ Key = Guid.Parse("{9C54D522-A826-4FBE-9421-074661B482D0}"), FriendlyName = "FReleaseObjectVersion", Version = 23, IsSerialized = false },
            },
            Exports = new(),
            WorldTileInfo = null,
            doWeHaveWorldTileInfo = false,            
            PackageFlags = EPackageFlags.PKG_FilterEditorOnly,
        };
        asset.ClearNameIndexList();
        return asset;
    }

    private FPackageIndex EnsurePackageImported(string objectName, bool bImportOptional = false)
    {
        if (objectName == null)
            return FPackageIndex.Null;

        var import = _asset.FindImportByObjectName(objectName);
        if (import == null)
        {
            import = new UAssetAPI.Import()
            {
                ObjectName = new(_asset, objectName),
                OuterIndex =FPackageIndex.Null,
                ClassPackage = new(_asset, objectName),
                ClassName = new(_asset, "Package"),
                bImportOptional = bImportOptional
            };
            _asset.Imports.Add(import);
        }

        return FPackageIndex.FromImport(_asset.Imports.IndexOf(import));
    }

    private FPackageIndex EnsureObjectImported(FPackageIndex parent, string objectName, string className, bool bImportOptional = false)
    {
        var import = _asset.FindImportByObjectName(objectName);
        if (import == null)
        {
            var parentImport = parent.ToImport(_asset);
            import = new UAssetAPI.Import()
            {
                ObjectName = new(_asset, objectName),
                OuterIndex = parent,
                ClassPackage = parentImport.ObjectName,
                ClassName = new(_asset, className),
                bImportOptional = bImportOptional
            };
            _asset.Imports.Add(import);
        }

        return FPackageIndex.FromImport(_asset.Imports.IndexOf(import));
    }

    private PropertyExport CreatePropertyExport(UProperty property, string name, int serialSize, IEnumerable<FPackageIndex> serializationBeforeSerializationDependencies, IEnumerable<FPackageIndex> createBeforeSerializationDependencies, IEnumerable<FPackageIndex> createBeforeCreateDependencies, FPackageIndex outerIndex, FPackageIndex classIndex, FPackageIndex templateIndex)
    {
        var propertyExport = new PropertyExport()
        {
            Asset = _asset,
            Property = property,
            Data = new(),
            ObjectName = new FName(_asset, name),
            ObjectFlags = EObjectFlags.RF_Public,
            SerialSize = serialSize,
            SerialOffset = 0, // Filled be serializer
            bForcedExport = false,
            bNotForClient = false,
            bNotForServer = false,
            PackageGuid = Guid.Empty,
            IsInheritedInstance = false,
            PackageFlags = EPackageFlags.PKG_None,
            bNotAlwaysLoadedForEditorGame = false,
            bIsAsset = false,
            GeneratePublicHash = false,
            SerializationBeforeSerializationDependencies = new(serializationBeforeSerializationDependencies),
            CreateBeforeSerializationDependencies = new(createBeforeSerializationDependencies),
            SerializationBeforeCreateDependencies = new(),
            CreateBeforeCreateDependencies = new(createBeforeCreateDependencies),
            PublicExportHash = 0,
            Padding = null,
            Extras = new byte[0],
            OuterIndex = outerIndex,
            ClassIndex = classIndex,
            SuperIndex = new FPackageIndex(0),
            TemplateIndex = templateIndex,
        };
        return propertyExport;
    }

    private (FPackageIndex Index, PropertyExport Export) CreateVariable(VariableSymbol symbol)
    {
        string propertyType = null;
        int? serialSize = null;
        UProperty property = null;
        var serializationBeforeSerializationDependencies = new List<FPackageIndex>();
        var createBeforeSerializationDependencies = new List<FPackageIndex>();
        var createBeforeCreateDependencies = new List<FPackageIndex>();

        var type = symbol.Declaration?.Type.Text ?? symbol.Parameter.Type.Text;

        switch (type)
        {
            case "byte":
                propertyType = "ByteProperty";
                serialSize = 37;
                property = new UByteProperty()
                {
                    Enum = new FPackageIndex(0),
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;
            case "bool":
                propertyType = "BoolProperty";
                serialSize = 35;
                property = new UBoolProperty()
                {
                    NativeBool = true,
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 1,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;
            case "int":
                propertyType = "IntProperty";
                serialSize = 33;
                property = new UIntProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;

            case "string":
                propertyType = "StrProperty";
                serialSize = 33;
                property = new UStrProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;

            case "float":
                // TODO test
                propertyType = "FloatProperty";
                serialSize = 33;
                property = new UFloatProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;
            case "double":
                // TODO test
                propertyType = "DoubleProperty";
                serialSize = 33;
                property = new UDoubleProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;
            case "Interface":
                propertyType = "InterfaceProperty";
                serialSize = 37;
                var interfaceClassIndex = FindPackageIndexInAsset(symbol.InnerSymbol);
                property = new UInterfaceProperty()
                {
                    InterfaceClass = interfaceClassIndex,
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null
                };
                createBeforeSerializationDependencies.Add(interfaceClassIndex);
                break;
            case "Struct":
                propertyType = "StructProperty";
                serialSize = 37;
                var structClassIndex = FindPackageIndexInAsset(symbol.InnerSymbol);
                property = new UStructProperty()
                {
                    Struct = structClassIndex,
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null
                };
                serializationBeforeSerializationDependencies.Add(structClassIndex);
                break;
            case "Array":
                propertyType = "ArrayProperty";
                serialSize = 37;
                var arrayInnerIndex = FindPackageIndexInAsset(symbol.InnerSymbol);
                property = new UArrayProperty()
                {
                    Inner = arrayInnerIndex,
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                serializationBeforeSerializationDependencies.Add(arrayInnerIndex);
                break;
            case "Enum":
                // TODO: implement this properly
                propertyType = "IntProperty";
                serialSize = 33;
                property = new UIntProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;
            //case "Object":
            //    // TODO test
            //    propertyType = "ObjectProperty";
            //    serialSize = 37;
            //    var propertyClassIndex = FindPackageIndexInAsset(symbol.InnerSymbol);
            //    property = new UObjectProperty()
            //    {
            //        PropertyClass = propertyClassIndex,
            //        ArrayDim = EArrayDim.TArray,
            //        ElementSize = 0,
            //        PropertyFlags = EPropertyFlags.CPF_None,
            //        RepNotifyFunc = new FName(_asset, "None"),
            //        BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
            //        RawValue = null,
            //        Next = null
            //    };
            //    break;
            //case "Delegate":
            //    // TODO test
            //    propertyType = "DelegateProperty";
            //    serialSize = 37;
            //    var signatureFunctionIndex = FindPackageIndexInAsset(symbol.InnerSymbol);
            //    property = new UDelegateProperty()
            //    {
            //        SignatureFunction = signatureFunctionIndex,
            //        ArrayDim = EArrayDim.TArray,
            //        ElementSize = 0,
            //        PropertyFlags = EPropertyFlags.CPF_None,
            //        RepNotifyFunc = new FName(_asset, "None"),
            //        BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
            //        RawValue = null,
            //        Next = null
            //    };
            //    break;
            //case "Class":
            //    // TODO test
            //    propertyType = "ClassProperty";
            //    serialSize = 37;
            //    var metaClassIndex = FindPackageIndexInAsset(symbol.InnerSymbol);
            //    property = new UClassProperty()
            //    {
            //        MetaClass = metaClassIndex,
            //        ArrayDim = EArrayDim.TArray,
            //        ElementSize = 0,
            //        PropertyFlags = EPropertyFlags.CPF_None,
            //        RepNotifyFunc = new FName(_asset, "None"),
            //        BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
            //        RawValue = null,
            //        Next = null
            //    };
            //    break;
            default:
                throw new NotImplementedException($"Creating new property of type {type} is not implemented");
        }

        if (symbol.IsParameter)
        {
            property.PropertyFlags = EPropertyFlags.CPF_BlueprintVisible | EPropertyFlags.CPF_BlueprintReadOnly | EPropertyFlags.CPF_Parm;
            if (symbol.IsOutParameter)
            {
                property.PropertyFlags |= EPropertyFlags.CPF_OutParm;
            }
            if (symbol.IsReturnParameter)
            {
                property.PropertyFlags |= EPropertyFlags.CPF_OutParm;
                property.PropertyFlags |= EPropertyFlags.CPF_ReturnParm;
            }
        }

        var classExport = _asset.FindClassExportByName(symbol.DeclaringClass?.Name);
        var functionExport = _asset.FindFunctionExportByName(symbol?.DeclaringProcedure?.Name);
        var coreUObjectImport = _asset.FindImportIndexByObjectName("/Script/CoreUObject") ?? throw new NotImplementedException();
        var propertyClassImportIndex = EnsureObjectImported(coreUObjectImport, propertyType, "Class");
        var propertyTemplateImportIndex = EnsureObjectImported(coreUObjectImport, $"Default__{propertyType}", propertyType);

        var propertyOwnerIndex =
            symbol.DeclaringProcedure != null ?
                FPackageIndex.FromExport(_asset.Exports.IndexOf(functionExport)) :
            symbol.DeclaringClass != null ?
                FPackageIndex.FromExport(_asset.Exports.IndexOf(classExport)) :
                FPackageIndex.Null;

        if (propertyOwnerIndex != FPackageIndex.Null)
        {
            createBeforeCreateDependencies.Insert(0, propertyOwnerIndex);
        }

        var propertyExport = CreatePropertyExport(
            property: property,
            name: symbol.Name,
            serialSize: serialSize.Value,
            serializationBeforeSerializationDependencies: serializationBeforeSerializationDependencies,
            createBeforeSerializationDependencies: createBeforeSerializationDependencies,
            createBeforeCreateDependencies: createBeforeCreateDependencies,
            outerIndex: propertyOwnerIndex,
            classIndex: propertyClassImportIndex,
            templateIndex: propertyTemplateImportIndex);

        _asset.Exports.Add(propertyExport);
        var packageIndex = FPackageIndex.FromExport(_asset.Exports.Count - 1);
        return (packageIndex, propertyExport);
    }

    private FPackageIndex CreateProcedureImport(ProcedureSymbol symbol)
    {
        var import = new Import()
        {
            ObjectName = new(_asset, symbol.Name),
            OuterIndex = FindPackageIndexInAsset(symbol?.DeclaringClass),
            ClassPackage = new(_asset, symbol?.DeclaringPackage?.Name),
            ClassName = new(_asset, "Function"),
            bImportOptional = false
        };
        _asset.Imports.Add(import);
        return FPackageIndex.FromImport(_asset.Imports.IndexOf(import));
    }

    private FPackageIndex EnsurePackageIndexForSymbolCreated(Symbol symbol)
    {
        if (symbol == null)
            return FPackageIndex.Null;

        if (TryFindPackageIndexInAsset(symbol, out var packageIndex))
            return packageIndex;

        return CreatePackageIndexForSymbol(symbol);
    }

    private FPackageIndex CreatePackageIndexForSymbol(Symbol symbol)
    {
        if (symbol is VariableSymbol variableSymbol)
        {
            return CreateVariable(variableSymbol).Index;
        }
        else if (symbol is ProcedureSymbol procedureSymbol)
        {
            if (procedureSymbol.IsExternal)
            {
                return CreateProcedureImport(procedureSymbol);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private void FixPropertyPointer(ref KismetPropertyPointer pointer)
    {
        if (pointer is IntermediatePropertyPointer iProperty)
        {
            var packageIndex = EnsurePackageIndexForSymbolCreated(iProperty.Symbol);

            pointer = new KismetPropertyPointer()
            {
                Old = packageIndex,
                New = new()
                {
                    Path = new[] { new FName(_asset, iProperty.Symbol.Name) },
                    ResolvedOwner = packageIndex,
                },
            };
        }
    }

    private void FixPackageIndex(ref FPackageIndex packageIndex)
    {
        if (packageIndex is IntermediatePackageIndex iPackageIndex)
        {
            packageIndex = EnsurePackageIndexForSymbolCreated(iPackageIndex.Symbol);
        }
    }

    private FPackageIndex FixPackageIndex(FPackageIndex packageIndex)
    {
        FixPackageIndex(ref packageIndex);
        return packageIndex;
    }

    private void FixName(ref FName name)
    {
        if (name is IntermediateName iName)
        {
            name = new FName(_asset, iName.TextValue);
        }
    }

    private FName FixName(FName name)
    {
        FixName(ref name);
        return name;
    }

    private string? GetFullName(object obj)
    {
        if (obj is UAssetAPI.Import import)
        {
            if (import.OuterIndex.Index != 0)
            {
                string parent = GetFullName(import.OuterIndex);
                return parent + "." + import.ObjectName.ToString();
            }
            else
            {
                return import.ObjectName.ToString();
            }
        }
        else if (obj is Export export)
        {
            if (export.OuterIndex.Index != 0)
            {
                string parent = GetFullName(export.OuterIndex);
                return parent + "." + export.ObjectName.ToString();
            }
            else
            {
                return export.ObjectName.ToString();
            }
        }
        else if (obj is FField field)
            return field.Name.ToString();
        else if (obj is FName fname)
            return fname.ToString();
        else if (obj is FPackageIndex packageIndex)
        {
            if (packageIndex.IsImport())
                return GetFullName(packageIndex.ToImport(_asset));
            else if (packageIndex.IsExport())
                return GetFullName(packageIndex.ToExport(_asset));
            else if (packageIndex.IsNull())
                return null;
            else
                throw new NotImplementedException();
        }
        else
        {
            return null;
        }
    }

    private IEnumerable<(object ImportOrExport, FPackageIndex PackageIndex)> GetPackageIndexByLocalName(string name)
    {
        if (_asset is UAsset uasset)
        {
            foreach (var import in uasset.Imports)
            {
                if (import.ObjectName.ToString() == name)
                {
                    yield return (import, new FPackageIndex(-(uasset.Imports.IndexOf(import) + 1)));
                }
            }
        }
        else
        {
            throw new NotImplementedException("Zen import");
        }
        foreach (var export in _asset.Exports)
        {
            if (export.ObjectName.ToString() == name)
            {
                yield return ((export, new FPackageIndex(+(_asset.Exports.IndexOf(export) + 1))));
            }
        }
    }

    private IEnumerable<(object ImportOrExport, FPackageIndex PackageIndex)> GetPackageIndexByFullName(string name)
    {
        if (_asset is UAsset uasset)
        {
            foreach (var import in uasset.Imports)
            {
                var importFullName = GetFullName(import);
                if (importFullName == name)
                {
                    yield return (import, new FPackageIndex(-(uasset.Imports.IndexOf(import) + 1)));
                }
            }
        }
        else
        {
            throw new NotImplementedException("Zen import");
        }
        foreach (var export in _asset.Exports)
        {
            var exportFullName = GetFullName(export);
            if (exportFullName == name)
            {
                yield return ((export, new FPackageIndex(+(_asset.Exports.IndexOf(export) + 1))));
            }
        }
    }

    private FPackageIndex FindPackageIndexInAsset(Symbol symbol)
    {
        if (symbol == null)
            return FPackageIndex.Null;

        if (!TryFindPackageIndexInAsset(symbol, out var packageIndex))
        {
            packageIndex = EnsurePackageImported(symbol.DeclaringPackage?.Name);
            packageIndex = EnsureObjectImported(packageIndex, symbol.Name, "Class"); // TODO classname
        }

        return packageIndex;
    }

    private bool TryFindPackageIndexInAsset(Symbol symbol, out FPackageIndex? index)
    {
        index = null;


        var packageName = symbol.DeclaringPackage?.Name;
        var className = symbol.DeclaringClass?.Name;
        var functionName = symbol.DeclaringProcedure?.Name;
        var name = symbol.Name;

        // TODO fix
        if (name == "<null>")
            return true;

        var packageClassFunctionLocalName = string.Join(".", new[] { packageName, className, functionName, name }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var classFunctionLocalName = string.Join(".", new[] { className, functionName, name }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var classLocalName = string.Join(".", new[] { className, name }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var localName = name;

        var packageClassFunctionLocalCandidates = GetPackageIndexByFullName(packageClassFunctionLocalName).ToList();
        if (packageClassFunctionLocalCandidates.Count == 1)
        {
            index = packageClassFunctionLocalCandidates[0].PackageIndex;
            return true;
        }

        var classFunctionLocalCandidates = GetPackageIndexByFullName(classFunctionLocalName).ToList();
        if (classFunctionLocalCandidates.Count == 1)
        {
            index = classFunctionLocalCandidates[0].PackageIndex;
            return true;
        }

        var classLocalCandidates = GetPackageIndexByFullName(classLocalName).ToList();
        if (classLocalCandidates.Count == 1)
        {
            index = classLocalCandidates[0].PackageIndex;
            return true;
        }

        var localCandidates = GetPackageIndexByLocalName(localName).ToList();
        if (localCandidates.Count == 1)
        {
            index = localCandidates[0].PackageIndex;
            return true;
        }

        return false;
    }

    private KismetExpression[] GetFixedBytecode(IEnumerable<KismetExpression> expressions)
    {
        var bytecode = expressions.ToArray();
        foreach (var baseExpr in bytecode.Flatten())
        {
            switch (baseExpr)
            {
                case EX_BindDelegate expr:
                    FixName(ref expr.FunctionName);
                    break;

                case EX_InstanceDelegate expr:
                    FixName(ref expr.FunctionName);
                    break;

                case EX_InstrumentationEvent expr:
                    FixName(ref expr.EventName);
                    break;

                case EX_NameConst expr:
                    expr.Value = FixName(expr.Value);
                    break;

                case EX_VirtualFunction expr:
                    FixName(ref expr.VirtualFunctionName);
                    break;

                case EX_CrossInterfaceCast expr:
                    FixPackageIndex(ref expr.ClassPtr);
                    break;

                case EX_DynamicCast expr:
                    FixPackageIndex(ref expr.ClassPtr);
                    break;

                case EX_FinalFunction expr:
                    FixPackageIndex(ref expr.StackNode);
                    break;

                case EX_InterfaceToObjCast expr:
                    FixPackageIndex(ref expr.ClassPtr);
                    break;

                case EX_MetaCast expr:
                    FixPackageIndex(ref expr.ClassPtr);
                    break;

                case EX_ObjectConst expr:
                    expr.Value = FixPackageIndex(expr.Value);
                    break;

                case EX_ObjToInterfaceCast expr:
                    FixPackageIndex(ref expr.ClassPtr);
                    break;

                case EX_SetArray expr:
                    FixPackageIndex(ref expr.ArrayInnerProp);
                    break;

                case EX_StructConst expr:
                    FixPackageIndex(ref expr.Struct);
                    break;

                case EX_TextConst expr:
                    if (expr.Value?.StringTableAsset != null)
                    {
                        expr.Value.StringTableAsset = FixPackageIndex(expr.Value.StringTableAsset);
                    }
                    break;

                case EX_ArrayConst expr:
                    FixPropertyPointer(ref expr.InnerProperty);
                    break;

                case EX_ClassSparseDataVariable expr:
                    FixPropertyPointer(ref expr.Variable);
                    break;

                case EX_Context expr:
                    FixPropertyPointer(ref expr.RValuePointer);
                    break;

                case EX_DefaultVariable expr:
                    FixPropertyPointer(ref expr.Variable);
                    break;

                case EX_InstanceVariable expr:
                    FixPropertyPointer(ref expr.Variable);
                    break;

                case EX_Let expr:
                    FixPropertyPointer(ref expr.Value);
                    break;

                case EX_LetValueOnPersistentFrame expr:
                    FixPropertyPointer(ref expr.DestinationProperty);
                    break;

                case EX_LocalOutVariable expr:
                    FixPropertyPointer(ref expr.Variable);
                    break;

                case EX_LocalVariable expr:
                    FixPropertyPointer(ref expr.Variable);
                    break;

                case EX_MapConst expr:
                    FixPropertyPointer(ref expr.KeyProperty);
                    FixPropertyPointer(ref expr.ValueProperty);
                    break;

                case EX_PropertyConst expr:
                    FixPropertyPointer(ref expr.Property);
                    break;

                case EX_SetConst expr:
                    FixPropertyPointer(ref expr.InnerProperty);
                    break;

                case EX_StructMemberContext expr:
                    FixPropertyPointer(ref expr.StructMemberExpression);
                    break;

                default:
                    break;
            }
        }
        return bytecode;
    }

    private FunctionExport CreateFunctionExport(CompiledFunctionContext context)
    {
        var coreUObjectImport = EnsurePackageImported("/Script/CoreUObject");
        var functionClassImport = EnsureObjectImported(coreUObjectImport, "Function", "Class");
        var functionDefaultObjectImport = EnsureObjectImported(coreUObjectImport, "Default__Function", "Function");
        var classExport = _asset.FindClassExportByName(context.Symbol?.DeclaringClass?.Name);

        var ownerIndex = context.Symbol.DeclaringClass != null ?
            FPackageIndex.FromExport(_asset.Exports.IndexOf(classExport)) :
            FPackageIndex.Null;

        var createBeforeCreateDependencies = new List<FPackageIndex>();
        if (ownerIndex != FPackageIndex.Null)
            createBeforeCreateDependencies.Add(ownerIndex);

        // TODO: override
        var baseFunctionClassIndex = FPackageIndex.Null;
        var baseFunctionIndex = FPackageIndex.Null;
        var ubergraphFunction = _asset.GetUbergraphFunction();
        var ubergraphFunctionIndex = ubergraphFunction != null ?
            FPackageIndex.FromExport(_asset.Exports.IndexOf(ubergraphFunction)) :
            FPackageIndex.Null;

        var export = new FunctionExport()
        {
            FunctionFlags = context.Flags,
            SuperStruct = baseFunctionIndex,
            Children = new(),
            LoadedProperties = Array.Empty<FProperty>(),
            ScriptBytecode = null,
            ScriptBytecodeSize = 0,
            ScriptBytecodeRaw = null,
            Field = new() { Next = null },
            Data = new(),
            ObjectName = new(_asset, context.Symbol.Name),
            ObjectFlags = EObjectFlags.RF_Public,
            SerialSize = 0xDEADBEEF,
            SerialOffset = 0xDEADBEEF,
            bForcedExport = false,
            bNotForClient = false,
            bNotForServer = false,
            PackageGuid = Guid.Empty,
            IsInheritedInstance = false,
            PackageFlags = EPackageFlags.PKG_None,
            bNotAlwaysLoadedForEditorGame = false,
            bIsAsset = false,
            GeneratePublicHash = false,
            SerializationBeforeSerializationDependencies = new(),
            CreateBeforeSerializationDependencies = new() { /*ubergraphFunctionIndex*/ },
            SerializationBeforeCreateDependencies = new(),
            CreateBeforeCreateDependencies = createBeforeCreateDependencies,
            PublicExportHash = 0,
            Padding = null,
            Extras = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 },
            OuterIndex = ownerIndex,
            ClassIndex = functionClassImport,
            SuperIndex = baseFunctionIndex, 
            TemplateIndex = functionDefaultObjectImport,
        };
        _asset.Exports.Add(export);

        var children = context.Variables
            .Select(x => CreatePackageIndexForSymbol(x.Symbol))
            .ToList();
        export.Children.AddRange(children);
        export.SerializationBeforeSerializationDependencies.AddRange(children);

        if (!baseFunctionClassIndex.IsNull())
        {
            export.CreateBeforeSerializationDependencies.Insert(0, baseFunctionClassIndex);
        }
        if (!baseFunctionIndex.IsNull())
        {
            export.SerializationBeforeSerializationDependencies.Insert(0, baseFunctionIndex);
            export.CreateBeforeCreateDependencies.Add(baseFunctionIndex);
        }
        
        var exportIndex = FPackageIndex.FromExport(_asset.Exports.IndexOf(export));
        if (classExport != null)
        {
            classExport.FuncMap[export.ObjectName] = exportIndex;
            classExport.Children.Add(exportIndex);
            classExport.CreateBeforeSerializationDependencies.Add(exportIndex);
        }

        return export;
    }

    private ClassExport CreateClassExport(CompiledClassContext classContext)
    {
        var scriptEnginePackageIndex = EnsurePackageImported("/Script/Engine");
        var blueprintGeneratedClassObjectIndex = EnsureObjectImported(scriptEnginePackageIndex, "BlueprintGeneratedClass", "Class");
        var blueprintGeneratedClassDefaultObjectIndex = EnsureObjectImported(blueprintGeneratedClassObjectIndex, "Default__BlueprintGeneratedClass", "BlueprintGeneratedClass");

        var scriptCoreUObjectPackageIndex = EnsurePackageImported("/Script/CoreUObject");
        var objectObjectIndex = EnsureObjectImported(scriptCoreUObjectPackageIndex, "Object", "Class");
        var objectDefaultObjectIndex = EnsureObjectImported(objectObjectIndex, "Default__Object", "Object");

        var classDefaultObjectIndex = FPackageIndex.Null;
        var baseClassObjectIndex = objectObjectIndex;
        var baseClassDefaultObjectIndex = objectDefaultObjectIndex;

        var serializationBeforeSerializationDependencies = new List<FPackageIndex>();
        if (baseClassObjectIndex != FPackageIndex.Null)
        {
            serializationBeforeSerializationDependencies.Add(baseClassObjectIndex);
            serializationBeforeSerializationDependencies.Add(baseClassDefaultObjectIndex);
        }

        var createBeforeCreateDependencies = new List<FPackageIndex>();
        if (baseClassObjectIndex != FPackageIndex.Null)
            createBeforeCreateDependencies.Add(baseClassObjectIndex);

        var classExport = new ClassExport()
        {
            FuncMap = new(),
            ClassFlags = EClassFlags.CLASS_Parsed | EClassFlags.CLASS_ReplicationDataIsSetUp | EClassFlags.CLASS_CompiledFromBlueprint | EClassFlags.CLASS_HasInstancedReference,
            ClassWithin = objectObjectIndex, // -11
            ClassConfigName = new FName(_asset, "Engine"),
            Interfaces = Array.Empty<SerializedInterfaceReference>(),
            ClassGeneratedBy = FPackageIndex.Null,
            bDeprecatedForceScriptOrder = false,
            bCooked = true,
            ClassDefaultObject = classDefaultObjectIndex,
            SuperStruct = baseClassObjectIndex,
            Children = new(),
            LoadedProperties = Array.Empty<FProperty>(),
            ScriptBytecode = Array.Empty<KismetExpression>(),
            ScriptBytecodeSize = 0,
            ScriptBytecodeRaw = null,
            Field = new() { Next = null },
            /*
             * TODO
             *       "Data": [
                    {
                      "$type": "UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI",
                      "Name": "SimpleConstructionScript",
                      "DuplicationIndex": 0,
                      "Value": 382
                    },
                    {
                      "$type": "UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI",
                      "Name": "InheritableComponentHandler",
                      "DuplicationIndex": 0,
                      "Value": 184
                    },
                    {
                      "$type": "UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI",
                      "Name": "UberGraphFramePointerProperty",
                      "DuplicationIndex": 0,
                      "Value": 1
                    },
                    {
                      "$type": "UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI",
                      "Name": "UberGraphFunction",
                      "DuplicationIndex": 0,
                      "Value": 6
                    }
                  ],
             */
            Data = new(),
            ObjectName = new FName(_asset, classContext.Symbol.Name),
            ObjectFlags = EObjectFlags.RF_Public | EObjectFlags.RF_Transactional,
            SerialSize = 0xDEADBEEF,
            SerialOffset = 0xDEADBEEF,
            bForcedExport = false,
            bNotForClient = false,
            bNotForServer = false,
            PackageGuid = Guid.Empty,
            IsInheritedInstance = false,
            PackageFlags = EPackageFlags.PKG_None,
            bNotAlwaysLoadedForEditorGame = false,
            bIsAsset = false,
            GeneratePublicHash = false,
            /* 
             * TODO
             * - Base class
             * - Base class default object
             * - Class properties
             */
            SerializationBeforeSerializationDependencies = serializationBeforeSerializationDependencies, 
            /*
             * TODO
             * - Function exports
             */
            CreateBeforeSerializationDependencies = new(),
            SerializationBeforeCreateDependencies = new() { blueprintGeneratedClassObjectIndex, blueprintGeneratedClassDefaultObjectIndex },
            CreateBeforeCreateDependencies = createBeforeCreateDependencies,
            PublicExportHash = 0,
            Padding = null,
            Extras = Array.Empty<byte>(),
            OuterIndex = FPackageIndex.Null,
            ClassIndex = blueprintGeneratedClassObjectIndex, // -13
            SuperIndex = FPackageIndex.Null, // -2
            TemplateIndex = blueprintGeneratedClassDefaultObjectIndex,
        };
        _asset.Exports.Add(classExport);
        return classExport;
    }

    private T? FindChildExport<T>(StructExport? parent, string name) where T : Export
    {
        var selection = parent?.Children
            .Where(x => x.IsExport())
            .Select(x => x.ToExport(_asset)) ??
            _asset.Exports;
        return selection
            .Where(x => x is T && x.ObjectName.ToString() == name)
            .Cast<T>()
            .SingleOrDefault();
    }

    public override UAssetLinker LinkCompiledScript(CompiledScriptContext scriptContext)
    {
        foreach (var functionContext in scriptContext.Functions)
        {
            LinkCompiledFunction(functionContext);
        }

        foreach (var classContext in scriptContext.Classes)
        {
            var classExport = FindChildExport<ClassExport>(null, classContext.Symbol.Name);
            if (classExport == null)
                classExport = CreateClassExport(classContext);

            foreach (var variableContext in classContext.Variables)
            {
                var export = FindChildExport<PropertyExport>(classExport, variableContext.Symbol.Name);
                if (export == null)
                {
                    (var index, var propExport) = CreateVariable(variableContext.Symbol);
                    propExport.Property.PropertyFlags |= EPropertyFlags.CPF_Edit;
                    propExport.Property.PropertyFlags |= EPropertyFlags.CPF_BlueprintVisible;
                    propExport.Property.PropertyFlags |= EPropertyFlags.CPF_DisableEditOnInstance;
                    classExport!.Children.Add(index);
                }
            }

            foreach (var functionContext in classContext.Functions)
            {
                LinkCompiledFunction(functionContext);
            }
        }

        return this;
    }

    private void LinkCompiledFunction(CompiledFunctionContext functionContext)
    {
        var classExport = functionContext.Symbol.DeclaringClass != null ?
            FindChildExport<ClassExport>(null, functionContext.Symbol!.DeclaringClass!.Name) :
            null;

        var functionExport = FindChildExport<FunctionExport>(classExport, functionContext.Symbol.Name);
        if (functionExport == null)
            functionExport = CreateFunctionExport(functionContext);

        functionExport.ScriptBytecode = GetFixedBytecode(functionContext.Bytecode);
    }

    public override UAsset Build()
    {
        return _asset;
    }
}
