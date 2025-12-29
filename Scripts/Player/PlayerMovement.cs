using UnityEngine;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
    [Header("Player Health")]
    private int maxHealth = 100;
    public int currentHealth;

    [SerializeField]
    Canvas canvas;

    [SerializeField]
    Image fillImage;

    [Header("Player Movement & Gravity")]

    public float movementSpeed = 5f;
    private CharacterController controller;

    public float gravity = -9.81f;
    public Transform groundCheck;

    public LayerMask groundMask;
    public float groundDistance = 0.4f;
    private bool isGrounded;

    private Vector3 velocity;

    [Header("Foot Steps")]
    public AudioSource leftfootstepAudioSource;
    public AudioSource rightfootstepAudioSource;
    public AudioClip[] footstepSounds;

    public float footstepInterval = 0.5f; // Time between footsteps
    private float nextFootstepTime;
    private bool isLeftFoot = true; // To alternate between left and right foot

    private float lastGroundCheck;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateFillbar();
        UpdateVisibility();
        controller = GetComponent<CharacterController>();

    }

    void Update()
    {
        

    // Move this check to FixedUpdate or reduce frequency
    if (Time.fixedTime - lastGroundCheck > 0.1f) // Check every 0.1 seconds instead
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        lastGroundCheck = Time.fixedTime;
    }
    

        HandleMovement();
        HandleGravity();

        Vector3 totalMovement = velocity * Time.deltaTime;
        controller.Move(totalMovement);

        // Check input instead of velocity for more accurate detection
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isMovingInput = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        
        float playerSpeed = controller.velocity.magnitude;
        // Remove this line that runs every frame:
        // Debug.Log("Input Moving: " + isMovingInput + " | Speed: " + playerSpeed.ToString("F2"));

        // Only play footsteps if player is giving movement input AND actually moving
        if (isMovingInput && playerSpeed > 0.1f && Time.time >= nextFootstepTime)
        {
            // Keep this one since it only triggers occasionally:
            // Debug.Log("FOOTSTEP TRIGGERED!");
            PlayerFootstepSound();
            nextFootstepTime = Time.time + footstepInterval;
        }
    }

    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movement = transform.right * horizontal + transform.forward * vertical;
        velocity.x = movement.x * movementSpeed;  // Store in velocity instead of moving immediately
        velocity.z = movement.z * movementSpeed;
    }

    void HandleGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        // Don't move here - just modify velocity
    }

    void PlayerFootstepSound()
    {
        // Remove this line:
        // Debug.Log("Playing footstep sound!");

        AudioClip footstepClip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        if (isLeftFoot)
        {
            leftfootstepAudioSource.PlayOneShot(footstepClip);
        }
        else
        {
            rightfootstepAudioSource.PlayOneShot(footstepClip);
        }
        isLeftFoot = !isLeftFoot;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        UpdateFillbar();
        UpdateVisibility();
        
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void UpdateFillbar()
    {
        // Update the fill amount
        float value = Mathf.InverseLerp(0, maxHealth, currentHealth);

        fillImage.fillAmount = value;
    }

    void UpdateVisibility()
    {
        float value = fillImage.fillAmount;
    }
    private void Die()
    {
        // Implement death behavior (e.g., show death screen, respawn, etc.)
        Debug.Log("Player has died.");
    }
}
