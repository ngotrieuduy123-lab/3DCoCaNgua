using UnityEngine;

public class LobbyCosmeticsUI : MonoBehaviour
{
    void Start()
    {
        SkinShopUI.EnsureCreated(false);
    }

    public void OpenShop()
    {
        SkinShopUI.OpenShop();
    }

    public void OpenSkins()
    {
        SkinShopUI.OpenOwnedSkins();
    }
}
