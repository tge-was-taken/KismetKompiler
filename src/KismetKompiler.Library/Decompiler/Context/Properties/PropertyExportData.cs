using KismetKompiler.Library.Utilities;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Decompiler.Context.Properties;

public class PropertyExportData : IPropertyData
{
    public UnrealPackage Asset => Source.Asset;
    public PropertyExport Source { get; }

    public PropertyExportData(PropertyExport source)
    {
        Source = source;
    }

    public string Name
        => Source.ObjectName.ToString();

    public EPropertyFlags PropertyFlags
        => Source.Property.PropertyFlags;

    public string TypeName
        => Source.GetExportClassType().ToString();

    public string? PropertyClassName
        => Asset.GetName(((UObjectProperty)Source.Property).PropertyClass);

    public string? InterfaceClassName
        => Asset.GetName(((UInterfaceProperty)Source.Property).InterfaceClass);

    public string? StructName
        => Asset.GetName(((UStructProperty)Source.Property).Struct);

    public IPropertyData? ArrayInnerProperty
    {
        get
        {
            var inner = ((UArrayProperty)Source.Property).Inner;
            if (inner.IsExport())
            {
                var export = inner.ToExport(Asset);
                if (export is PropertyExport propertyExport)
                {
                    var innerProp = (PropertyExport)export;
                    return new PropertyExportData(innerProp);
                }
            }
            return null;
        }
    }

    object IPropertyData.Source => Source;
}