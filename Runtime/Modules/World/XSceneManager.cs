using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XFramework;

public static class XWorldManager
{
    public static XWorldSetting _setting;
    public static XWorldSetting Setting
    {
        get
        {
            if (_setting == null)
            {
                _setting = Resources.Load<XWorldSetting>("XWorld/XWorldSetting");
                if (_setting == null)
                {
                    _setting = ScriptableObject.CreateInstance<XWorldSetting>();
                }
            }
            return _setting;
        }
    }
}

[CreateAssetMenu(fileName = "XWorldSetting", menuName = "XFramework/XWorld/XWorldScene")]
public class XWorldScene : ScriptableObject
{
    [AssetPath]
    public string scenePath;
    public XWorldMipScene[] mipScenes;
}

[Serializable]
public class XWorldMipScene
{
    public int mipLevel;
    public XWorldTrunk[] trunks;

    public XWorldMipScene(int mipLevel, XWorldTrunk[] trunks)
    {
        this.mipLevel = mipLevel;
        this.trunks = trunks;
    }
}

[Serializable]
public class XWorldTrunk
{
    public TileCoord coord;
    public GameObject[] prefabs;

    public XWorldTrunk(TileCoord coord, GameObject[] prefabs)
    {
        this.coord = coord;
        this.prefabs = prefabs;
    }
}

[Serializable]
public struct TileCoord : IEquatable<TileCoord>
{
    public int mip;
    public int x;
    public int z;

    public bool Equals(TileCoord other)
    {
        return mip == other.mip && x == other.x && z == other.z;
    }

    public override bool Equals(object obj)
    {
        return obj is TileCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(mip, x, z);
    }
}

[CreateAssetMenu(fileName = "XWorldSetting", menuName = "XFramework/XWorld/XWorldSetting")]
public class XWorldSetting : ScriptableObject
{
    public int baseCellSize = 16;
    public int maxMipLevel = 10;
}

public abstract class WorldSplitter : MonoBehaviour
{
    public abstract IReadOnlyList<GameObject> GetGameObjects();
}
