using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Syntax.Statements.Declarations;
using System.Collections.Generic;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library;

public abstract class AssetBuilder
{
    //public abstract FName AddName(string name);
    public abstract UnrealPackage Build();
}

public record ImportIndex(
    Import Import,
    FPackageIndex Index);

//public class ClassBuilder
//{
//    private readonly UAssetBuilder _assetBuilder;
//    private NormalExport _classDefaultObject;
//    private ClassExport _class;

//    internal ClassBuilder(UAssetBuilder assetBuilder, string name)
//    {
//        _assetBuilder = assetBuilder;

//        var scriptEngine = _assetBuilder.ImportPackage("/Script/Engine");
//        var blueprintGeneratedClass = _assetBuilder.Import("/Script/Engine", "Class", "BlueprintGeneratedClass", scriptEngine);
//        var blueprintGeneratedClassDefault = _assetBuilder.Import("/Script/Engine", "BlueprintGeneratedClass", "Default__BlueprintGeneratedClass", blueprintGeneratedClass);

//        _class = new ClassExport()
//        {
//            FuncMap = new(),
//            ClassFlags = EClassFlags.CLASS_Parsed | EClassFlags.CLASS_ReplicationDataIsSetUp | EClassFlags.CLASS_CompiledFromBlueprint | EClassFlags.CLASS_HasInstancedReference,
//            ClassWithin = _assetBuilder.ImportCoreUObject("Class", "Object"), // -11
//            ClassConfigName = _assetBuilder.AddName("Engine"),
//            Interfaces = Array.Empty<SerializedInterfaceReference>(),
//            ClassGeneratedBy = FPackageIndex.Null,
//            bDeprecatedForceScriptOrder = false,
//            bCooked = true,
//            ClassDefaultObject = FPackageIndex.Null,
//            SuperStruct = FPackageIndex.Null,
//            Children = Array.Empty<FPackageIndex>(),
//            LoadedProperties = Array.Empty<FProperty>(),
//            ScriptBytecode = Array.Empty<KismetExpression>(),
//            ScriptBytecodeSize = 0,
//            ScriptBytecodeRaw = null,
//            Field = new() { Next = null },
//            Data = new(),
//            ObjectName = _assetBuilder.AddName(name),
//            ObjectFlags = EObjectFlags.RF_Public | EObjectFlags.RF_Transactional,
//            SerialSize = 228,
//            SerialOffset = 0,
//            bForcedExport = false,
//            bNotForClient = false,
//            bNotForServer = false,
//            PackageGuid = Guid.Empty,
//            IsInheritedInstance = false,
//            PackageFlags = EPackageFlags.PKG_None,
//            bNotAlwaysLoadedForEditorGame = false,
//            bIsAsset = false,
//            GeneratePublicHash = false,
//            SerializationBeforeSerializationDependencies = new(),
//            CreateBeforeSerializationDependencies = new(),
//            SerializationBeforeCreateDependencies = new(),
//            CreateBeforeCreateDependencies = new(),
//            PublicExportHash = 0,
//            Padding = null,
//            Extras = Array.Empty<byte>(),
//            OuterIndex = FPackageIndex.Null,
//            ClassIndex = blueprintGeneratedClass, // -13
//            SuperIndex = FPackageIndex.Null, // -2
//            TemplateIndex = blueprintGeneratedClassDefault,
//        };
//    }

//    public ClassBuilder WithBaseClass(string name)
//    {

//    }

//    public ClassBuilder WithFlags(EClassFlags flags)
//    {
//        _class.ClassFlags = flags;
//        return this;
//    }

//    public ClassBuilder AddFunction()
//    {

//    }

//    public FPackageIndex Build()
//    {
//        return _class;
//    }
//}

//public class PackageImportBuilder
//{
//    private readonly UAssetBuilder _assetBuilder;
//    private ImportIndex _package;

//    private PackageImportBuilder(UAssetBuilder assetBuilder, string name)
//    {
//        _assetBuilder = assetBuilder;
//        _package = _assetBuilder.Import(name, "Package", name, FPackageIndex.Null);
//    }

//    public PackageImportBuilder Import(string className, string objectName)
//    {
//        _assetBuilder.Import(_package.Import.ObjectName.ToString(), className, objectName, _package.Index);
//        return this;
//    }


//}

public class UAssetBuilder : AssetBuilder
{
    private UAsset _asset;

    public UAssetBuilder()
    {
        _asset = new UAsset
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
            PackageFlags = EPackageFlags.PKG_FilterEditorOnly,
        };
    }

    public override UAsset Build()
    {
        return _asset;
    }


    //public override FName AddName(string name)
    //{
    //    return new(_asset, name);
    //}

    //public ImportIndex Import(string classPackage, string className, string objectName, FPackageIndex outerIndex, bool isOptional = false)
    //{
    //    var import = new Import()
    //    {
    //        ObjectName = new(_asset, objectName),
    //        OuterIndex = outerIndex,
    //        ClassPackage = new(_asset, classPackage),
    //        ClassName = new(_asset, className),
    //        bImportOptional = isOptional
    //    };
    //    return new(import, _asset.AddImport(import));
    //}

    ////public ImportIndex ImportPackageClass(string className, string objectName, ImportIndex package)
    ////{

    ////}

    //public ImportIndex ImportPackage(string name)
    //    => Import(name, "Package", name, FPackageIndex.Null, false);

    //public AssetBuilder FromAsset(UAsset asset)
    //{
    //    _asset = asset;
    //    return this;
    //}

    //public ClassBuilder AddClass()
    //{
    //    return new ClassBuilder(this);
    //}

    //public FPackageIndex Export()
    //{
    //    var export = new Export();
    //    _asset.Exports.Add(export);
    //    _asset.DependsMap.Add(Array.Empty<int>());
    //}
}
