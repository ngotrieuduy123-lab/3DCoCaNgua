using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyCosmeticsUI : MonoBehaviour
{
    void Start()
    {
        SkinShopUI.EnsureCreated(false);
        RoyalMenuThemeStyler.EnsureCreated().ApplyForScene(SceneManager.GetActiveScene().name);
        BgmManager.EnsureCreated().UpdateBgmForScene(SceneManager.GetActiveScene().name);
    }

    public void OpenShop()
    {
        SkinShopUI.OpenShop();
    }

    public void OpenSkins()
    {
        SkinShopUI.OpenOwnedSkins();
    }

    public void QuitGame()
    {
        RoyalMenuThemeStyler.QuitApplication();
    }
}
