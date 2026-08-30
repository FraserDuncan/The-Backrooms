using UnityEngine;

public class HandheldCamera : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;   // auto-grabs from parent if left empty

    [Header("Walk Bob")]
    public float bobFrequency = 8f;          // step rhythm
    public float bobVertical = 0.05f;        // up/down per step
    public float bobHorizontal = 0.04f;      // side-to-side sway
    public float bobRoll = 1.2f;             // degrees of tilt per step

    [Header("Handheld Drift (always on)")]
    public float swayAmount = 0.6f;          // degrees of idle wander
    public float swaySpeed = 0.4f;           // how fast it wanders

    [Header("Smoothing")]
    public float smooth = 8f;

    private Vector3 startPos;
    private Quaternion startRot;
    private float bobTimer;
    private float seed;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
        seed = Random.value * 100f;
        if (controller == null) controller = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        Vector3 v = controller.velocity; v.y = 0f;
        bool moving = v.magnitude > 0.1f;

        // walk bob
        Vector3 bob = Vector3.zero;
        float roll = 0f;
        if (moving)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            bob = new Vector3(Mathf.Cos(bobTimer * 0.5f) * bobHorizontal,
                              Mathf.Sin(bobTimer) * bobVertical, 0f);
            roll = Mathf.Cos(bobTimer * 0.5f) * bobRoll;
        }
        else bobTimer = 0f;

        // handheld drift (always on)
        float t = Time.time * swaySpeed;
        float p = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * swayAmount;
        float y = (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * swayAmount;
        float r = (Mathf.PerlinNoise(seed + t, t) - 0.5f) * 2f * swayAmount;

        Vector3 targetPos = startPos + bob;
        Quaternion targetRot = startRot * Quaternion.Euler(p, y, roll + r);

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smooth);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * smooth);
    }
}