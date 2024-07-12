using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Packaging;

public record PackageExport<T>(FPackageIndex Index, T Export) where T : Export;
