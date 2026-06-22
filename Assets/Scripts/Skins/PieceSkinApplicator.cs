using System.Collections;
using UnityEngine;

public class PieceSkinApplicator : MonoBehaviour
{
    PieceController piece;
    Renderer[] originalRenderers;
    GameObject visualPivot;
    GameObject activeModel;
    SkinWalkAnimator walkAnimator;
    SkinTransformFlightAnimator flightAnimator;
    SkinMovementStyle movementStyle;
    float movementSpeed = 5f;
    float turnThresholdDegrees = 10f;
    float flightHeight;
    float flightTurnDuration = 0.2f;
    Vector3 modelRestLocalPosition;
    Vector3 flightLocalOffset;
    Quaternion facingBaseWorldRotation;
    Vector3 facingDirection = Vector3.forward;

    public float MovementSpeed => movementSpeed;

    public void Initialize(PieceController targetPiece)
    {
        piece = targetPiece;
        originalRenderers = GetComponentsInChildren<Renderer>(true);
        Apply(PlayerPrefs.GetString("PlayerSkin_" + piece.playerIndex, SkinCatalog.DefaultSkinId));
    }

    public void Apply(string skinId)
    {
        SkinCatalog catalog = SkinCatalog.Load();
        SkinDefinition skin = catalog != null ? catalog.Get(skinId) : null;

        ApplyDefinition(skin);
    }

    public void ApplyDefinition(SkinDefinition skin)
    {
        RestoreDefault();

        if (skin == null || skin.id == SkinCatalog.DefaultSkinId || skin.modelPrefab == null)
            return;

        foreach (Renderer renderer in originalRenderers)
            if (renderer != null)
                renderer.enabled = false;

        visualPivot = new GameObject("SkinFacingPivot");
        visualPivot.transform.SetParent(transform, false);
        facingBaseWorldRotation = visualPivot.transform.rotation;
        facingDirection = Vector3.forward;

        activeModel = Instantiate(skin.modelPrefab, visualPivot.transform);
        activeModel.name = "SkinModel_" + skin.id;
        activeModel.transform.localPosition = skin.localPosition;
        activeModel.transform.localRotation = Quaternion.Euler(skin.localEulerAngles);
        activeModel.transform.localScale = skin.localScale;
        modelRestLocalPosition = skin.localPosition;
        movementStyle = skin.movementStyle;
        movementSpeed = Mathf.Max(0.05f, skin.movementSpeed);
        SetLayerRecursively(activeModel, gameObject.layer);

        Animator animator = activeModel.GetComponent<Animator>();
        if (animator != null && movementStyle == SkinMovementStyle.Walk && skin.walkAnimation != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            walkAnimator = activeModel.AddComponent<SkinWalkAnimator>();
            walkAnimator.Configure(
                animator,
                skin.walkAnimation,
                skin.turnLeftAnimation,
                skin.turnRightAnimation,
                skin.turnPlaybackSpeed);
        }
        else if (animator != null &&
                 movementStyle == SkinMovementStyle.TransformFlight &&
                 skin.transformAnimation != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            flightAnimator = activeModel.AddComponent<SkinTransformFlightAnimator>();
            flightAnimator.Configure(animator, skin.transformAnimation, skin.transformPlaybackSpeed);
        }

        turnThresholdDegrees = Mathf.Max(0f, skin.turnThresholdDegrees);
        flightHeight = Mathf.Max(0f, skin.flightHeight);
        flightTurnDuration = Mathf.Max(0f, skin.flightTurnDuration);
        flightLocalOffset = skin.flightLocalOffset;
    }

    public void SetWalking(bool walking)
    {
        if (walkAnimator != null)
            walkAnimator.SetWalking(walking);
    }

    public IEnumerator TurnTowards(Vector3 worldDirection)
    {
        if (visualPivot == null)
            yield break;

        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f)
            yield break;

        worldDirection.Normalize();
        Quaternion startRotation = visualPivot.transform.rotation;
        float targetYaw = Vector3.SignedAngle(Vector3.forward, worldDirection, Vector3.up);
        Quaternion targetRotation = Quaternion.AngleAxis(targetYaw, Vector3.up) * facingBaseWorldRotation;
        float signedAngle = Vector3.SignedAngle(facingDirection, worldDirection, Vector3.up);

        if (Mathf.Abs(signedAngle) <= turnThresholdDegrees)
        {
            visualPivot.transform.rotation = targetRotation;
            facingDirection = worldDirection;
            yield break;
        }

        if (movementStyle == SkinMovementStyle.TransformFlight)
        {
            yield return RotateVisual(startRotation, targetRotation, flightTurnDuration);
            facingDirection = worldDirection;
            yield break;
        }

        float duration = walkAnimator != null ? walkAnimator.PlayTurn(signedAngle < 0f) : 0f;
        if (duration <= 0f)
        {
            visualPivot.transform.rotation = targetRotation;
            facingDirection = worldDirection;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            visualPivot.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        visualPivot.transform.rotation = targetRotation;
        facingDirection = worldDirection;
    }

    public IEnumerator PrepareMovement()
    {
        if (flightAnimator == null || visualPivot == null || activeModel == null)
            yield break;

        float duration = flightAnimator.PlayForward();
        yield return AnimateFlightPose(duration, true);
        flightAnimator.HoldFlightPose();
    }

    public IEnumerator FinishMovement()
    {
        if (flightAnimator == null || visualPivot == null || activeModel == null)
            yield break;

        float duration = flightAnimator.PlayReverse();
        yield return AnimateFlightPose(duration, false);
        flightAnimator.ResetToRobot();
    }

    public void ResetMotion()
    {
        SetWalking(false);

        if (visualPivot != null)
            visualPivot.transform.localPosition = Vector3.zero;

        if (activeModel != null)
            activeModel.transform.localPosition = modelRestLocalPosition;

        if (flightAnimator != null)
            flightAnimator.ResetToRobot();
    }

    IEnumerator AnimateFlightPose(float duration, bool flying)
    {
        Vector3 pivotStart = visualPivot.transform.localPosition;
        float worldFlightHeight = flightHeight * Mathf.Abs(transform.lossyScale.y);
        Vector3 pivotTarget = flying
            ? transform.InverseTransformVector(Vector3.up * worldFlightHeight)
            : Vector3.zero;
        Vector3 modelStart = activeModel.transform.localPosition;
        Vector3 modelTarget = modelRestLocalPosition + (flying ? flightLocalOffset : Vector3.zero);

        if (duration <= 0f)
        {
            visualPivot.transform.localPosition = pivotTarget;
            activeModel.transform.localPosition = modelTarget;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            visualPivot.transform.localPosition = Vector3.Lerp(pivotStart, pivotTarget, t);
            activeModel.transform.localPosition = Vector3.Lerp(modelStart, modelTarget, t);
            yield return null;
        }

        visualPivot.transform.localPosition = pivotTarget;
        activeModel.transform.localPosition = modelTarget;
    }

    IEnumerator RotateVisual(Quaternion startRotation, Quaternion targetRotation, float duration)
    {
        if (duration <= 0f)
        {
            visualPivot.transform.rotation = targetRotation;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            visualPivot.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        visualPivot.transform.rotation = targetRotation;
    }

    void RestoreDefault()
    {
        if (visualPivot != null)
        {
            if (Application.isPlaying)
                Destroy(visualPivot);
            else
                DestroyImmediate(visualPivot);
        }

        visualPivot = null;
        activeModel = null;
        walkAnimator = null;
        flightAnimator = null;
        movementStyle = SkinMovementStyle.Walk;
        movementSpeed = 5f;
        turnThresholdDegrees = 10f;
        flightHeight = 0f;
        flightTurnDuration = 0.2f;
        modelRestLocalPosition = Vector3.zero;
        flightLocalOffset = Vector3.zero;
        facingBaseWorldRotation = Quaternion.identity;
        facingDirection = Vector3.forward;

        if (originalRenderers == null)
            return;

        foreach (Renderer renderer in originalRenderers)
            if (renderer != null)
                renderer.enabled = true;
    }

    static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;

        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
