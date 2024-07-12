using KismetKompiler.Library.Compiler;
using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Compiler.Intermediate;
using KismetKompiler.Library.Utilities;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UAssetAPI;
using UAssetAPI.CustomVersions;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Packaging;

public partial class UAssetLinker : PackageLinker<UAsset>
{
    public UAssetLinker()
    {
    }

    public UAssetLinker(UAsset asset) : base(asset)
    {
    }

    protected override UAsset CreateDefaultAsset()
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

    protected override FPackageIndex EnsurePackageImported(string objectName, bool bImportOptional = false)
    {
        if (objectName == null)
            return FPackageIndex.Null;

        var import = Package.FindImportByObjectName(objectName);
        if (import == null)
        {
            import = new Import()
            {
                ObjectName = new(Package, objectName),
                OuterIndex = FPackageIndex.Null,
                ClassPackage = new(Package, objectName),
                ClassName = new(Package, "Package"),
                bImportOptional = bImportOptional
            };
            Package.Imports.Add(import);
        }

        return FPackageIndex.FromImport(Package.Imports.IndexOf(import));
    }

    protected override FPackageIndex EnsureObjectImported(FPackageIndex parent, string objectName, string className, bool bImportOptional = false)
    {
        var import = Package.FindImportByObjectName(objectName);
        if (import == null)
        {
            var parentImport = parent.ToImport(Package);
            import = new Import()
            {
                ObjectName = new(Package, objectName),
                OuterIndex = parent,
                ClassPackage = parentImport.ObjectName,
                ClassName = new(Package, className),
                bImportOptional = bImportOptional
            };
            Package.Imports.Add(import);
        }

        return FPackageIndex.FromImport(Package.Imports.IndexOf(import));
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
                if (SerializeLoadedProperties)
                {
                    if (!classExport.LoadedProperties.Any(x => x.Name.ToString() == variableContext.Symbol.Name))
                        classExport.LoadedProperties.Add(CreateFProperty(variableContext.Symbol));
                }
                else
                {
                    var export = FindChildExport<PropertyExport>(classExport, variableContext.Symbol.Name);
                    (var index, var propExport) = CreatePropertyAsPropertyExport(variableContext.Symbol);
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

        foreach (var variableContext in functionContext.Variables)
        {
            if (SerializeLoadedProperties)
            {
                if (!functionExport.LoadedProperties.Any(x => x.Name.ToString() == variableContext.Symbol.Name))
                    functionExport.LoadedProperties.Add(CreateFProperty(variableContext.Symbol));
            }
            else
            {
                var export = FindChildExport<PropertyExport>(functionExport, variableContext.Symbol.Name);
                (var index, var propExport) = CreatePropertyAsPropertyExport(variableContext.Symbol);
                functionExport!.Children.Add(index);
            }
        }

        functionExport.ScriptBytecode = GetFixedBytecode(functionContext.Bytecode);
    }

    protected override FPackageIndex CreateProcedureImport(ProcedureSymbol symbol)
    {
        var import = new Import()
        {
            ObjectName = new(Package, symbol.Name),
            OuterIndex = FindPackageIndexInAsset(symbol?.DeclaringClass),
            ClassPackage = new(Package, symbol?.DeclaringPackage?.Name),
            ClassName = new(Package, "Function"),
            bImportOptional = false
        };
        Package.Imports.Add(import);
        return FPackageIndex.FromImport(Package.Imports.IndexOf(import));
    }

    protected override IEnumerable<(object ImportOrExport, FPackageIndex PackageIndex)> GetPackageIndexByLocalName(string name)
    {
        if (Package is UAsset uasset)
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
        foreach (var export in Package.Exports)
        {
            if (export.ObjectName.ToString() == name)
            {
                yield return (export, new FPackageIndex(+(Package.Exports.IndexOf(export) + 1)));
            }
        }
    }

    protected override IEnumerable<(object ImportOrExport, FPackageIndex PackageIndex)> GetPackageIndexByFullName(string name)
    {
        if (Package is UAsset uasset)
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
        foreach (var export in Package.Exports)
        {
            var exportFullName = GetFullName(export);
            if (exportFullName == name)
            {
                yield return (export, new FPackageIndex(+(Package.Exports.IndexOf(export) + 1)));
            }
        }
    }

    public override UAsset Build()
    {
        return Package;
    }

    [GeneratedRegex("_(\\d+)")]
    private static partial Regex NameIdSuffix();
}