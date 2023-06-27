using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler;

public static class AssetHelper
{
    public static string GetFullName(this UAsset asset, object obj)
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

    public static string GetFullName(this UAsset asset, FPackageIndex index)
    {
        var obj = asset.GetImportOrExport(index);
        return asset.GetFullName(obj);
    }

    public static string GetName(this UAsset asset, FPackageIndex index)
    {
        if (index.IsExport())
        {
            return index.ToExport(asset).ObjectName.ToString();
        }
        else
        {
            return index.ToImport(asset).ObjectName.ToString();
        }
    }

    public static FunctionExport GetFunctionExport(this UAsset asset, FPackageIndex index)
    {
        return (FunctionExport)index.ToExport(asset);
    }

    public static object GetImportOrExport(this UAsset asset, FPackageIndex index)
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

    public static bool FindProperty(this UAsset asset, int index, FName propname, out FProperty property)
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

    public static object GetProperty(this UAsset asset, KismetPropertyPointer pointer)
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

    public static string GetPropertyName(this UAsset asset, KismetPropertyPointer pointer, bool fullName)
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

    public static bool ImportInheritsType(this UAsset asset, Import import, string type)
    {
        if (import.ClassName.ToString() == type)
            return true;

        if (import.OuterIndex.IsNull())
            return false;

        var parent = asset.Imports.Where(x => x.ObjectName == import.ClassName).FirstOrDefault();
        if (parent == null)
            return false;
        return asset.ImportInheritsType(parent, type);
    }
}
