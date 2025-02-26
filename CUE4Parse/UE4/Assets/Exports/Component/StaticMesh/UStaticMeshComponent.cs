using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;

public class UStaticMeshComponent : UMeshComponent
{
    public FStaticMeshComponentLODInfo[] LODData;
    public bool bSerializeAsCookedData;
    public FPackageIndex? MeshPaintTextureCooked;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        if (Ar.Game == EGame.GAME_Borderlands3) Ar.ReadBoolean();
        LODData = Ar.ReadArray(() => new FStaticMeshComponentLODInfo(Ar));

        if (FFortniteMainBranchObjectVersion.Get(Ar) >= FFortniteMainBranchObjectVersion.Type.MeshPaintTextureUsesEditorOnly)
        {
            bSerializeAsCookedData = Ar.ReadBoolean();

            if (bSerializeAsCookedData)
                MeshPaintTextureCooked = new FPackageIndex(Ar);
        }
    }

    public FPackageIndex GetStaticMesh()
    {
        var mesh = new FPackageIndex();
        var current = this;
        while (true)
        {
            if (current is null) break;
            mesh = current.GetOrDefault("StaticMesh", new FPackageIndex());
            if (!mesh.IsNull || current.Template == null)
                break;
            current = current.Template.Load<UStaticMeshComponent>();
        }

        return mesh;
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        if (LODData is { Length: <= 0 }) return;
        writer.WritePropertyName("LODData");
        serializer.Serialize(writer, LODData);
    }
}

public class UBaseBuildingStaticMeshComponent : UStaticMeshComponent;
