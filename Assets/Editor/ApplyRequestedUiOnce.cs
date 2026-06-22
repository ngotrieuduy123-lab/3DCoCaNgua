using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ApplyRequestedUiOnce
{
    const string SessionKey = "3DCoCaNgua.ApplyRequestedUiOnce.20260621";

    static ApplyRequestedUiOnce()
    {
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        MinimalUiRedesign.ApplyAuth();
        MinimalUiRedesign.ApplyLobby();
        Debug.Log("Applied requested Auth and Lobby UI updates.");
    }
}
