using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Packaging;

public record ImportIndex(
    Import Import,
    FPackageIndex Index);
