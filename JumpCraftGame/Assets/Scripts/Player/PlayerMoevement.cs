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
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Resets grounded status when landing on a collider
        isGrounded = true;
    }
}