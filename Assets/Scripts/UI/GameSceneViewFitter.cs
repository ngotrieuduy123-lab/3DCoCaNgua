using UnityEngine;
using UnityEngine.UI;

public class GameSceneViewFitter : MonoBehaviour
{
    public Camera targetCamera;
    public Canvas canvas;
    public CanvasScaler canvasScaler;

    [Header("Camera")]
    public Vector3 cameraPosition = new Vector3(0.73f, 2.72f, -0.58f);
    public Vector3 cameraEulerAngles = new Vector3(78f, 0f, 0f);
    public float baseFieldOfView = 68f;
    public float narrowFieldOfView = 74f;
    public float wideFieldOfView = 66f;

    [Header("Canvas")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    void Awake()
    {
        Apply();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            Apply();
    }

    public void Apply()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvasScaler == null && canvas != null)
            canvasScaler = canvas.GetComponent<CanvasScaler>();

        ApplyCamera();
        ApplyCanvas();
    }

    void ApplyCamera()
    {
        if (targetCamera == null) return;

        targetCamera.transform.position = cameraPosition;
        targetCamera.transform.rotation = Quaternion.Euler(cameraEulerAngles);

        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;

        if (aspect < 1.5f)
            targetCamera.fieldOfView = narrowFieldOfView;
        else if (aspect > 1.9f)
            targetCamera.fieldOfView = wideFieldOfView;
        else
            targetCamera.fieldOfView = baseFieldOfView;
    }

    void ApplyCanvas()
    {
        if (canvas != null && targetCamera != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = targetCamera;
            canvas.planeDistance = 1f;
        }

        if (canvasScaler == null) return;

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
    }
}
