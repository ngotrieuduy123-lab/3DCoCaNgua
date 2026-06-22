using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class SkinTransformFlightAnimator : MonoBehaviour
{
    AnimationClip transformClip;
    float playbackSpeed;
    PlayableGraph graph;
    AnimationClipPlayable playable;

    public bool IsConfigured => graph.IsValid() && playable.IsValid() && transformClip != null;

    public void Configure(Animator animator, AnimationClip clip, float speed)
    {
        transformClip = clip;
        playbackSpeed = Mathf.Max(0.1f, speed);

        if (animator == null || transformClip == null)
            return;

        graph = PlayableGraph.Create("SkinTransformFlightGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        playable = AnimationClipPlayable.Create(graph, transformClip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetDuration(transformClip.length);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "TransformFlight", animator);
        output.SetSourcePlayable(playable);

        ResetToRobot();
    }

    public float PlayForward()
    {
        if (!IsConfigured)
            return 0f;

        playable.SetTime(0d);
        playable.SetSpeed(playbackSpeed);
        graph.Evaluate(0f);
        graph.Play();
        return transformClip.length / playbackSpeed;
    }

    public float PlayReverse()
    {
        if (!IsConfigured)
            return 0f;

        playable.SetTime(transformClip.length);
        playable.SetSpeed(-playbackSpeed);
        graph.Evaluate(0f);
        graph.Play();
        return transformClip.length / playbackSpeed;
    }

    public void HoldFlightPose()
    {
        if (!IsConfigured)
            return;

        graph.Stop();
        playable.SetSpeed(0d);
        playable.SetTime(transformClip.length);
        graph.Evaluate(0f);
    }

    public void ResetToRobot()
    {
        if (!IsConfigured)
            return;

        graph.Stop();
        playable.SetSpeed(0d);
        playable.SetTime(0d);
        graph.Evaluate(0f);
    }

    void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
