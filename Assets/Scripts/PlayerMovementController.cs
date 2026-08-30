using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : MonoBehaviour
{
    [Header("Look")]
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;        // the CameraHolder

    [Header("Movement Speeds")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float crouchSpeed = 2f;
    public float gravity = -9.81f;

    [Header("Crouch")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransition = 10f;

    [Header("Stamina")]
    public float maxStamina = 5f;            // seconds of sprint
    public float drainRate = 1f;             // per second while sprinting
    public float regenRate = 0.6f;           // per second
    public float regenDelay = 1f;    
    public float exhaustDuration = 5f;
    private bool isExhausted;
    private float exhaustTimer;        // pause before regen starts

    [Header("Stamina UI")]
    public CanvasGroup staminaGroup;
    public Image staminaFill;
    public float uiFadeSpeed = 4f;

    private CharacterController controller;
    private float cameraPitch, verticalVelocity;
    private float stamina, regenTimer, startCamY;
    private bool isCrouching, sprinting;

    public bool IsSprinting => sprinting;
    public bool IsCrouching => isCrouching;
    public float StaminaNormalized => maxStamina > 0 ? stamina / maxStamina : 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        stamina = maxStamina;
        if (cameraTransform != null) startCamY = cameraTransform.localPosition.y;
    }

    void Update()
    {
        Look();
        Move();
        Stamina();
        UpdateUI();
    }

    void Look()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(Vector3.up * mx);
        cameraPitch = Mathf.Clamp(cameraPitch - my, -90f, 90f);
        cameraTransform.localEulerAngles = Vector3.right * cameraPitch;
    }

    void Move()
    {
        // crouch (hold Left Ctrl)
        isCrouching = Input.GetKey(KeyCode.LeftControl);
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransition);
        controller.center = new Vector3(0f, (controller.height - standHeight) / 2f, 0f);
        if (cameraTransform != null)
        {
            Vector3 p = cameraTransform.localPosition;
            p.y = startCamY - (standHeight - controller.height);
            cameraTransform.localPosition = p;
        }

        // input + speed
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 input = (transform.right * x + transform.forward * z);
        bool moving = input.sqrMagnitude > 0.01f;
        sprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && stamina > 0f && moving && !isExhausted;

        float speed = isCrouching ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);

        if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = input.normalized * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void Stamina()
    {
        if (sprinting)
        {
            stamina -= drainRate * Time.deltaTime;
            regenTimer = 0f;
        }
        else
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= regenDelay) stamina += regenRate * Time.deltaTime;
        }
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);

        // hit empty -> locked out of sprinting for exhaustDuration seconds
        if (stamina <= 0f && !isExhausted)
        {
            isExhausted = true;
            exhaustTimer = exhaustDuration;
        }
        if (isExhausted)
        {
            exhaustTimer -= Time.deltaTime;
            if (exhaustTimer <= 0f) isExhausted = false;
        }
    }

    void UpdateUI()
    {
        float t = stamina / maxStamina;
        if (staminaFill != null) staminaFill.fillAmount = t;
        if (staminaGroup != null)
        {
            float target = (t < 0.999f) ? 1f : 0f;   // visible unless full
            staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, target, Time.deltaTime * uiFadeSpeed);
        }
    }
}