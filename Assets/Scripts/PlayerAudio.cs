using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Sources")]
    public AudioSource sfxSource;        // footsteps + one-shots
    public AudioSource breathSource;     // looping sprint breathing
    public AudioSource ambienceSource;   // looping room tone

    [Header("Clips")]
    public AudioClip[] footstepClips;
    public AudioClip outOfBreathClip;
    public AudioClip breathClip;
    public AudioClip ambienceClip;

    [Header("Footstep Timing")]
    public float walkStepInterval = 0.5f;
    public float sprintStepInterval = 0.33f;
    public float crouchStepInterval = 0.7f;
    public float footstepVolume = 0.6f;

    [Header("Breathing")]
    public float breathVolume = 0.3f;

    private CharacterController controller;
    private PlayerMovementController move;
    private float stepTimer;
    private bool wasExhausted;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        move = GetComponent<PlayerMovementController>();

        if (sfxSource) sfxSource.spatialBlend = 0f;

        if (ambienceSource && ambienceClip)
        {
            ambienceSource.clip = ambienceClip;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.Play();
        }
        if (breathSource && breathClip)
        {
            breathSource.clip = breathClip;
            breathSource.loop = true;
            breathSource.spatialBlend = 0f;
            breathSource.volume = 0f;
        }
    }

    void Update()
    {
        Vector3 v = controller.velocity; v.y = 0f;
        bool moving = controller.isGrounded && v.magnitude > 0.5f;

        // footsteps
        if (moving)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                PlayFootstep();
                stepTimer = move.IsCrouching ? crouchStepInterval
                          : move.IsSprinting ? sprintStepInterval
                          : walkStepInterval;
            }
        }
        else stepTimer = 0f;

        // sprint breathing (fades in/out)
        if (breathSource)
        {
            if (!breathSource.isPlaying && move.IsSprinting) breathSource.Play();
            float target = move.IsSprinting ? breathVolume : 0f;
            breathSource.volume = Mathf.MoveTowards(breathSource.volume, target, Time.deltaTime * 1.5f);
            if (breathSource.volume <= 0.001f && breathSource.isPlaying && !move.IsSprinting)
                breathSource.Stop();
        }

        // out of breath (fires once when stamina empties, re-arms after recovery)
        if (move.StaminaNormalized <= 0.01f && !wasExhausted)
        {
            wasExhausted = true;
            if (sfxSource && outOfBreathClip) sfxSource.PlayOneShot(outOfBreathClip);
        }
        if (move.StaminaNormalized > 0.4f) wasExhausted = false;
    }

    void PlayFootstep()
    {
        if (sfxSource == null || footstepClips == null || footstepClips.Length == 0) return;
        sfxSource.pitch = Random.Range(0.92f, 1.08f);
        float vol = footstepVolume * (move.IsCrouching ? 0.5f : move.IsSprinting ? 1f : 0.8f);
        sfxSource.PlayOneShot(footstepClips[Random.Range(0, footstepClips.Length)], vol);
    }
}