using System.Runtime.InteropServices;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.IO.Objects
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FIoContainerHeaderLocalizedPackage
    {
        public readonly FPackageId SourcePackageId;
        public readonly FMappedName SourcePackageName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FIoContainerHeaderPackageRedirect
    {
        public readonly FPackageId SourcePackageId;
        public readonly FPackageId TargetPackageId;
        public readonly FMappedName SourcePackageName;
    }

    public readonly struct FIoContainerHeaderSoftPackageReferences
    {
        public readonly FPackageId[] PackageIds;
        public readonly byte[] PackageIndices;
        public readonly bool bContainsSoftPackageReferences;

        public FIoContainerHeaderSoftPackageReferences(FArchive Ar)
        {
            bContainsSoftPackageReferences = Ar.ReadBoolean();
            if (bContainsSoftPackageReferences)
            {
                PackageIds = Ar.ReadArray<FPackageId>();
                PackageIndices = Ar.ReadArray<byte>();
            }
            else
            {
                PackageIds = [];
                PackageIndices = [];
            }
        }
    }

    public enum EIoContainerHeaderVersion // : uint
    {
        BeforeVersionWasAdded = -1, // Custom constant to indicate pre-UE5 data
        Initial = 0,
        LocalizedPackages = 1,
        OptionalSegmentPackages = 2,
        NoExportInfo = 3,
        SoftPackageReferences = 4,
        SoftPackageReferencesOffset  = 5,

        LatestPlusOne,
        Latest = LatestPlusOne - 1
    }

    public class FIoContainerHeader
    {
        private const int Signature = 0x496f436e;
        public readonly EIoContainerHeaderVersion Version;
        public readonly FIoContainerId ContainerId;

        public FPackageId[] PackageIds;
        public FFilePackageStoreEntry[] StoreEntries;
        public FFilePackageStoreEntry[] OptionalSegmentStoreEntries;
        public FPackageId[] OptionalSegmentPackageIds;

        public FNameEntrySerialized[]? ContainerNameMap; // RedirectsNameMap
        public FIoContainerHeaderLocalizedPackage[]? LocalizedPackages;
        public FIoContainerHeaderPackageRedirect[] PackageRedirects;
        public FIoContainerHeaderSerialInfo SoftPackageReferencesSerialInfo;
        public FIoContainerHeaderSoftPackageReferences SoftPackageReferences;

        // Index of each id in PackageIds and OptionalSegmentPackageIds. Every package load looks its store entry up.
        private readonly Dictionary<FPackageId, int> _packageIndices;
        private readonly Dictionary<FPackageId, int> _optionalSegmentPackageIndices;

        public FIoContainerHeader(FArchive Ar)
        {
            Version = Ar.Game >= GAME_UE5_0 ? EIoContainerHeaderVersion.Initial : EIoContainerHeaderVersion.BeforeVersionWasAdded;
            if (Version == EIoContainerHeaderVersion.Initial)
            {
                var signature = Ar.Read<uint>();
                if (signature != Signature)
                {
                    throw new ParserException(Ar, $"Invalid container header signature: 0x{signature:X8} != 0x{Signature:X8}");
                }

                Version = Ar.Read<EIoContainerHeaderVersion>();
            }

            ContainerId = Ar.Read<FIoContainerId>();
            var packageCount = Version < EIoContainerHeaderVersion.OptionalSegmentPackages ? Ar.Read<uint>() : 0;
            if (Version == EIoContainerHeaderVersion.BeforeVersionWasAdded)
            {
                var namesSize = Ar.Read<int>();
                var namesPos = Ar.Position;
                var nameHashesSize = Ar.Read<int>();
                var continuePos = Ar.Position + nameHashesSize;
                if (namesSize > 0 && nameHashesSize > 0)
                {
                    Ar.Position = namesPos;
                    ContainerNameMap = FNameEntrySerialized.LoadNameBatch(Ar, nameHashesSize / sizeof(ulong) - 1);
                }
                Ar.Position = continuePos;
            }

            ReadPackageIdsAndEntries(Ar, out PackageIds, out StoreEntries);

            if (Version >= EIoContainerHeaderVersion.OptionalSegmentPackages)
            {
                ReadPackageIdsAndEntries(Ar, out OptionalSegmentPackageIds, out OptionalSegmentStoreEntries);
            }
            if (Version >= EIoContainerHeaderVersion.Initial)
            {
                ContainerNameMap = FNameEntrySerialized.LoadNameBatch(Ar);
            }
            if (Version >= EIoContainerHeaderVersion.LocalizedPackages)
            {
                LocalizedPackages = Ar.ReadArray<FIoContainerHeaderLocalizedPackage>();
            }
            PackageRedirects = Ar.ReadArray<FIoContainerHeaderPackageRedirect>();
            if (Version == EIoContainerHeaderVersion.SoftPackageReferences)
            {
                SoftPackageReferences = new FIoContainerHeaderSoftPackageReferences(Ar);
            }
            else if (Version >= EIoContainerHeaderVersion.SoftPackageReferencesOffset)
            {
                SoftPackageReferencesSerialInfo = new FIoContainerHeaderSerialInfo(Ar);
            }

            _packageIndices = IndicesOf(PackageIds);
            _optionalSegmentPackageIndices = IndicesOf(OptionalSegmentPackageIds ?? []);
        }

        /// <summary>The index of the id in PackageIds, or -1.</summary>
        public int IndexOfPackage(FPackageId id) => _packageIndices.GetValueOrDefault(id, -1);

        /// <summary>The index of the id in OptionalSegmentPackageIds, or -1.</summary>
        public int IndexOfOptionalSegmentPackage(FPackageId id) => _optionalSegmentPackageIndices.GetValueOrDefault(id, -1);

        // The first index of an id that occurs more than once, as Array.IndexOf gives.
        private static Dictionary<FPackageId, int> IndicesOf(FPackageId[] ids)
        {
            var indices = new Dictionary<FPackageId, int>(ids.Length);
            for (var i = 0; i < ids.Length; i++) indices.TryAdd(ids[i], i);
            return indices;
        }

        private void ReadPackageIdsAndEntries(FArchive Ar, out FPackageId[] packageIds, out FFilePackageStoreEntry[] storeEntries)
        {
            packageIds = Ar.ReadArray<FPackageId>();
            var storeEntriesSize = Ar.Read<int>();
            var storeEntriesEnd = Ar.Position + storeEntriesSize;
            storeEntries = Ar.ReadArray(packageIds.Length, () => new FFilePackageStoreEntry(Ar, Version));
            Ar.Position = storeEntriesEnd;
        }
    }
}
