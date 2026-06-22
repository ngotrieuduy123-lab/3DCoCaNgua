using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Co Ca Ngua/Skins/Skin Catalog", fileName = "SkinCatalog")]
public class SkinCatalog : ScriptableObject
{
    public const string DefaultSkinId = "default_horse";
    const string ResourcesPath = "Skins/SkinCatalog";

    public List<SkinDefinition> skins = new List<SkinDefinition>();

    public static SkinCatalog Load()
    {
        return Resources.Load<SkinCatalog>(ResourcesPath);
    }

    public SkinDefinition Get(string skinId)
    {
        string normalizedId = string.IsNullOrWhiteSpace(skinId) ? DefaultSkinId : skinId.Trim();

        foreach (SkinDefinition skin in skins)
            if (skin != null && skin.id == normalizedId)
                return skin;

        return null;
    }
}
