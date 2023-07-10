using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library;

public static class AssetHelper
{
    public static string GetFullName(this UnrealPackage asset, object obj)
    {
        if (obj is Import import)
        {
            if (import.OuterIndex.Index != 0)
            {
                string parent = asset.GetFullName(import.OuterIndex);
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
                string parent = asset.GetFullName(export.OuterIndex);
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
        else
        {
            return "<null>";
        }
    }

    public static string GetFullName(this UnrealPackage asset, FPackageIndex index)
    {
        var obj = asset.GetImportOrExport(index);
        return asset.GetFullName(obj);
    }

    public static string GetName(this UnrealPackage asset, FPackageIndex index)
    {
        if (index.IsExport())
        {
            return index.ToExport(asset).ObjectName.ToString();
        }
        else if (index.IsNull())
        {
            return "<null>";
        }
        else
        {
            return index.ToImport(asset).ObjectName.ToString();
        }
    }

    public static FunctionExport GetFunctionExport(this UnrealPackage asset, FPackageIndex index)
    {
        return (FunctionExport)index.ToExport(asset);
    }

    public static object GetImportOrExport(this UnrealPackage asset, FPackageIndex index)
    {
        if (index != null)
        {
            if (index.IsExport())
                return index.ToExport(asset);
            else if (index.IsImport())
                return index.ToImport(asset);
            else if (index.IsNull())
                return null;
            else
                return null;
        }
        else
        {
            return null;
        }
    }

    public static bool FindProperty(this UnrealPackage asset, int index, FName propname, out FProperty property)
    {
        if (index < 0)
        {

            property = new FObjectProperty();
            return false;

        }
        Export export = asset.Exports[index - 1];
        if (export is StructExport)
        {
            foreach (FProperty prop in (export as StructExport).LoadedProperties)
            {
                if (prop.Name == propname)
                {
                    property = prop;
                    return true;
                }
            }
        }
        property = new FObjectProperty();
        return false;
    }

    public static object GetProperty(this UnrealPackage asset, KismetPropertyPointer pointer)
    {
        if (pointer.Old != null)
        {
            return asset.GetImportOrExport(pointer.Old);
        }
        else if (pointer.New != null)
        {
            if (pointer.New.ResolvedOwner.Index == 0)
                return null;

            if (asset.FindProperty(pointer.New.ResolvedOwner.Index, pointer.New.Path[0], out var prop))
                return prop;
            else
                return pointer.New.Path[0];
        }

        return null;
    }

    public static string GetPropertyName(this UnrealPackage asset, KismetPropertyPointer pointer, bool fullName)
    {
        var prop = asset.GetProperty(pointer);
        if (fullName)
        {
            return asset.GetFullName(prop);
        }
        else
        {
            if (prop is Export ex)
                return ex.ObjectName.ToString();
            else if (prop is Import im)
                return im.ObjectName.ToString();
            else if (prop is FField field)
                return field.Name.ToString();
            else if (prop is FName fname)
                return fname.ToString();
            else
                return "<null>";
        }
    }

    public static bool ImportInheritsType(this UnrealPackage asset, Import import, string type)
    {
        if (import.ClassName.ToString() == type)
            return true;

        if (import.OuterIndex.IsNull())
            return false;

        Import? parent = null;
        if (asset is UAsset uasset)
        {
            parent = uasset.Imports.Where(x => x.ObjectName == import.ClassName).FirstOrDefault();
        }
        else
        {
            throw new NotImplementedException("Zen import");
        }
        if (parent == null)
            return false;
        if (parent == import)
            return true;
        return asset.ImportInheritsType(parent, type);
    }

    public static ClassExport? FindClassExportByName(this UnrealPackage asset, string name)
    {
        return asset.Exports
            .Where(x => x is ClassExport)
            .Where(x => x.ObjectName.ToString() == name)
            .Cast<ClassExport>()
            .SingleOrDefault();
    }

    public static PropertyExport? FindPropertyExportByName(this UnrealPackage asset, string name)
    {
        return asset.Exports
            .Where(x => x is PropertyExport)
            .Where(x => x.ObjectName.ToString() == name)
            .Cast<PropertyExport>()
            .SingleOrDefault();
    }

    public static FunctionExport? FindFunctionExportByName(this UnrealPackage asset, string name)
    {
        return asset.Exports
            .Where(x => x is FunctionExport)
            .Where(x => x.ObjectName.ToString() == name)
            .Cast<FunctionExport>()
            .SingleOrDefault();
    }

    public static Import? FindImportByObjectName(this UnrealPackage asset, string name)
    {
        if (asset is UAsset uasset)
        {
            return uasset.Imports
                .Where(x => x.ObjectName.ToString() == name)
                .SingleOrDefault();
        }
        else
        {
            throw new NotImplementedException("Zen import");
        }
    }

    public static FPackageIndex? FindImportIndexByObjectName(this UnrealPackage asset, string name)
    {
        if (asset is UAsset uasset)
        {
            var import = FindImportByObjectName(asset, name);
            return FPackageIndex.FromImport(uasset.Imports.IndexOf(import));
        }
        else
        {
            throw new NotImplementedException("Zen import");
        }
    }

    public static FunctionExport? GetUbergraphFunction(this UnrealPackage asset)
    {
        return asset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .Where(x => x.IsUbergraphFunction())
            .SingleOrDefault();
    }
}

public static class FunctionExportExtensions
{
    public static bool IsUbergraphFunction(this FunctionExport functionExport)
    {
        return functionExport.FunctionFlags.HasFlag(EFunctionFlags.FUNC_UbergraphFunction) ||
               functionExport.ObjectName.ToString().StartsWith("ExecuteUbergraph_"); // DQXIS doesn't use the flag
    }
}
