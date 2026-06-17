using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Dice : MonoBehaviour
{
    public Rigidbody rb;
    public Transform[] faces;

    public Vector3 throwDirection = new Vector3(1f, -1f, 0f);
    public float throwForce = 2.5f;
    public float torqueForce = 5f;
    public Vector3[] valueEulerRotations = new Vector3[6];

    public Transform[] valueRotations;
    public Transform diceModel;

    public int value;
    public bool isRolling;

    private Vector3 startPosition;
    private Quaternion startRotation;

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetVisualValue(1);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetVisualValue(2);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SetVisualValue(3);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SetVisualValue(4);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) SetVisualValue(5);
        if (Keyboard.current.digit6Key.wasPressedThisFrame) SetVisualValue(6);
    }

    public void Roll()
    {
        if (!isRolling)
        {
            StartCoroutine(RollRoutine());

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayDice();
        }
    }

    IEnumerator RollRoutine()
    {
        isRolling = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = startPosition;
        transform.rotation = Random.rotation;

        yield return null;

        Vector3 dir = throwDirection.normalized;

        rb.AddForce(dir * throwForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

        yield return new WaitForSeconds(1.5f);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        value = GetTopFaceValue();

        Debug.Log(gameObject.name + " value: " + value);

        isRolling = false;
    }

    int GetTopFaceValue()
    {
        int topValue = 1;
        float highestY = faces[0].position.y;

        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].position.y > highestY)
            {
                highestY = faces[i].position.y;
                topValue = i + 1;
            }
        }

        return topValue;
    }

    public void RollVisualOnly()
    {
        if (!isRolling)
        {
            StartCoroutine(RollRoutine());
        }
    }
    public void SetVisualValue(int value)
    {
        this.value = value;

        if (value < 1 || value > 6) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Quaternion targetRotation = Quaternion.Euler(valueEulerRotations[value - 1]);

        rb.MoveRotation(targetRotation);
        transform.rotation = targetRotation;

        Debug.Log(gameObject.name + " set visual value: " + value);
    }
    Quaternion GetRotationForValue(int value)
    {
        if (value == 1) return Quaternion.Euler(0, 0, 0);
        if (value == 2) return Quaternion.Euler(90, 0, 0);
        if (value == 3) return Quaternion.Euler(0, 0, 90);
        if (value == 4) return Quaternion.Euler(0, 0, -90);
        if (value == 5) return Quaternion.Euler(-90, 0, 0);
        if (value == 6) return Quaternion.Euler(180, 0, 0);

        return Quaternion.identity;
    }
}
