using UnityEngine;
// Required if you ever use advanced Input System features, 
// though using Key.current works directly with Keyboard.current
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    private Rigidbody2D rb;
    private bool isGrounded = true;

    //Read by CameraFollow to track the height of the ground the player is standing on.
    public bool IsGrounded => isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Make sure a keyboard is connected
        if (Keyboard.current == null) return;

        // 1. Left and Right Movement (A and D keys)
        float moveInput = 0f;
        if (Keyboard.current.aKey.isPressed) moveInput = -1f; // Move left
        if (Keyboard.current.dKey.isPressed) moveInput = 1f;  // Move right

        // Apply horizontal velocity while keeping current vertical velocity
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        // 2. Jumping (W key)
        if (Keyboard.current.wKey.wasPressedThisFrame && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false; // Prevent mid-air jumping
        }

        // 3. Death check (falling below y = -8): Moves player back to main menu (death screen can be implemented later, or have a revive system with multiplayer?)
        if (transform.position.y <= -8)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameMenu");
        }
        
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        //Checks the contact between the player and the platform
        //Prevents jumping on side and bottom of platforms
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f) //Creates a normal vector based on the y direction of the contact (on top of a platform is positive)
            {
                isGrounded = true;
                break;
            }
        }
    }
}