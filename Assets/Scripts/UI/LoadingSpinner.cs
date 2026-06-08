using UnityEngine;

public class LoadingSpinner : MonoBehaviour
{
    public float speed = 240f;

    void Update()
    {
        transform.Rotate(0f, 0f, -speed * Time.unscaledDeltaTime);
    }
}
