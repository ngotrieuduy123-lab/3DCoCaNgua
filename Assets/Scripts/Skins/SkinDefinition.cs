using UnityEngine;

public enum SkinMovementStyle
{
    Walk,
    TransformFlight
}

[CreateAssetMenu(menuName = "Co Ca Ngua/Skins/Skin Definition", fileName = "SkinDefinition")]
public class SkinDefinition : ScriptableObject
{
    public string id = "default_horse";
    public string displayName = "Default Horse";
    [TextArea] public string description = "The classic horse piece.";
    [Min(0)] public int price;
    public bool includedByDefault;
    public Sprite previewImage;
    public GameObject modelPrefab;
    public SkinMovementStyle movementStyle = SkinMovementStyle.Walk;
    public AnimationClip walkAnimation;
    public AnimationClip turnLeftAnimation;
    public AnimationClip turnRightAnimation;
    [Min(0.1f)] public float turnPlaybackSpeed = 1.8f;
    [Min(0f)] public float turnThresholdDegrees = 10f;
    public AnimationClip transformAnimation;
    [Min(0.1f)] public float transformPlaybackSpeed = 4f;
    [Min(0f)] public float flightHeight = 0.008f;
    [Min(0f)] public float flightTurnDuration = 0.2f;
    public Vector3 flightLocalOffset = Vector3.zero;
    [Min(0.05f)] public float movementSpeed = 5f;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;
}
