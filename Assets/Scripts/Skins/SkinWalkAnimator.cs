using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class SkinWalkAnimator : MonoBehaviour
{
    Animator animator;
    AnimationClip walkClip;
    AnimationClip leftTurnClip;
    AnimationClip rightTurnClip;
    float turnPlaybackSpeed;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable walkPlayable;
    AnimationClipPlayable leftTurnPlayable;
    AnimationClipPlayable rightTurnPlayable;
    Transform hipsTransform;
    Vector3 idleHipsLocalPosition;
    Quaternion idleHipsLocalRotation;
    bool hasIdleHipsPose;
    bool walking;
    bool turning;

    public void Configure(
        Animator targetAnimator,
        AnimationClip targetWalkClip,
        AnimationClip targetLeftTurnClip,
        AnimationClip targetRightTurnClip,
        float targetTurnPlaybackSpeed)
    {
        animator = targetAnimator;
        walkClip = targetWalkClip;
        leftTurnClip = targetLeftTurnClip;
        rightTurnClip = targetRightTurnClip;
        turnPlaybackSpeed = Mathf.Max(0.1f, targetTurnPlaybackSpeed);

        if (animator == null || walkClip == null)
            return;

        hipsTransform = FindHipsTransform(animator.transform);

        graph = PlayableGraph.Create("SkinWalkGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
        walkPlayable.SetApplyFootIK(false);
        walkPlayable.SetApplyPlayableIK(false);

        mixer = AnimationMixerPlayable.Create(graph, 3);
        graph.Connect(walkPlayable, 0, mixer, 0);

        if (leftTurnClip != null)
        {
            leftTurnPlayable = AnimationClipPlayable.Create(graph, leftTurnClip);
            leftTurnPlayable.SetApplyFootIK(false);
            leftTurnPlayable.SetApplyPlayableIK(false);
            graph.Connect(leftTurnPlayable, 0, mixer, 1);
        }

        if (rightTurnClip != null)
        {
            rightTurnPlayable = AnimationClipPlayable.Create(graph, rightTurnClip);
            rightTurnPlayable.SetApplyFootIK(false);
            rightTurnPlayable.SetApplyPlayableIK(false);
            graph.Connect(rightTurnPlayable, 0, mixer, 2);
        }

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Walk", animator);
        output.SetSourcePlayable(mixer);

        graph.Play();
        SetMixerInput(0);
        walkPlayable.SetSpeed(0d);
        walkPlayable.SetTime(0d);
        graph.Evaluate(0f);

        if (hipsTransform != null)
        {
            idleHipsLocalPosition = hipsTransform.localPosition;
            idleHipsLocalRotation = hipsTransform.localRotation;
            hasIdleHipsPose = true;
        }

        SetWalking(false);
    }

    public void SetWalking(bool active)
    {
        if (!walkPlayable.IsValid())
            return;

        if (walking && active && !turning)
            return;

        walking = active;
        turning = false;
        StopTurnPlayables();

        if (!walking)
        {
            SetMixerInput(0);
            walkPlayable.SetSpeed(0d);
            walkPlayable.SetTime(0d);
            graph.Evaluate(0f);
            graph.Stop();
            RestoreIdleHipsPose();
            return;
        }

        SetMixerInput(0);
        walkPlayable.SetSpeed(1d);
        graph.Play();
        LockHipsHorizontalDrift();
    }

    public float PlayTurn(bool turnLeft)
    {
        AnimationClip clip = turnLeft ? leftTurnClip : rightTurnClip;
        AnimationClipPlayable playable = turnLeft ? leftTurnPlayable : rightTurnPlayable;

        if (clip == null || !playable.IsValid())
            return 0f;

        walking = false;
        turning = true;
        RestoreIdleHipsPose();
        SetMixerInput(turnLeft ? 1 : 2);
        playable.SetTime(0d);
        playable.SetSpeed(turnPlaybackSpeed);
        graph.Evaluate(0f);
        graph.Play();
        RestoreIdleHipsPose();
        return clip.length / turnPlaybackSpeed;
    }

    static Transform FindHipsTransform(Transform root)
    {
        if (root == null)
            return null;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && (t.name.EndsWith("Hips") || t.name.Contains("Hips")))
                return t;
        }
        return null;
    }

    void RestoreIdleHipsPose()
    {
        if (!hasIdleHipsPose || hipsTransform == null)
            return;

        hipsTransform.localPosition = idleHipsLocalPosition;
        hipsTransform.localRotation = idleHipsLocalRotation;
    }

    void LockHipsHorizontalDrift()
    {
        if (!hasIdleHipsPose || hipsTransform == null)
            return;

        // Keep vertical bob from walkClip but lock horizontal X/Y drift so GLB walk clips stay centered on tile
        Vector3 pos = hipsTransform.localPosition;
        pos.x = idleHipsLocalPosition.x;
        pos.y = idleHipsLocalPosition.y;
        hipsTransform.localPosition = pos;
    }

    void SetMixerInput(int activeInput)
    {
        if (!mixer.IsValid())
            return;

        for (int i = 0; i < mixer.GetInputCount(); i++)
            mixer.SetInputWeight(i, i == activeInput ? 1f : 0f);
    }

    void StopTurnPlayables()
    {
        if (leftTurnPlayable.IsValid())
            leftTurnPlayable.SetSpeed(0d);

        if (rightTurnPlayable.IsValid())
            rightTurnPlayable.SetSpeed(0d);
    }

    void LateUpdate()
    {
        if (turning)
        {
            RestoreIdleHipsPose();
            return;
        }

        if (!walking || !walkPlayable.IsValid() || walkClip == null || walkClip.length <= 0f)
        {
            RestoreIdleHipsPose();
            return;
        }

        if (walkPlayable.GetTime() >= walkClip.length)
        {
            walkPlayable.SetTime(walkPlayable.GetTime() % walkClip.length);
            graph.Evaluate(0f);
        }

        LockHipsHorizontalDrift();
    }

    void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
