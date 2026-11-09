using UnityEngine;
// Required if you ever use advanced Input System features, 
// though using Key.current works directly with Keyboard.current
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    private Rigidbody2D rb;
    private WizardAnimator wizard;
    private WandShooter shooter;
    private PlayerHealth health;
    private bool isGrounded = true;

    //Read by CameraFollow to track the height of the ground the player is standing on.
    public bool IsGrounded => isGrounded;

    [Header("Controls Setup")]
    [Tooltip("Check this box on Player 2's script component so they use Arrow Keys instead of WASD")]
    public bool useArrowKeys = false;

    // Static list to keep track of all active players in the scene
    private static List<PlayerMovement> activePlayers = new List<PlayerMovement>();

    private void OnEnable()
    {
        if (!activePlayers.Contains(this))
            activePlayers.Add(this);
    }

    private void OnDisable()
    {
        activePlayers.Remove(this);
    }
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        wizard = GetComponent<WizardAnimator>();
        shooter = GetComponent<WandShooter>();
        health = GetComponent<PlayerHealth>();

        // No friction, so running into the side of a platform after a missed jump slides you
        // down it instead of pinning you to the wall. Movement sets velocity directly, so this
        // doesn't make the ground slippery.
        var slippery = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        foreach (var playerCollider in GetComponents<Collider2D>())
        {
            playerCollider.sharedMaterial = slippery;
        }
    }

    void Update()
    {
        // Make sure a keyboard is connected
        if (Keyboard.current == null) return;

        // No control once dead
        if (health != null && health.IsDead)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 1. Left and Right Movement (A and D keys)
        float moveInput = 0f;

        if (useArrowKeys)
        {
            // Player 2: Left and Right Arrow Keys
            if (Keyboard.current.leftArrowKey.isPressed) moveInput = -1f;
            if (Keyboard.current.rightArrowKey.isPressed) moveInput = 1f;
        }
        else
        {
            // Player 1: A and D Keys
            if (Keyboard.current.aKey.isPressed) moveInput = -1f;
            if (Keyboard.current.dKey.isPressed) moveInput = 1f;
        }

        // Apply horizontal velocity while keeping current vertical velocity
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        // 2. Jumping Check (W key for Player 1, Up Arrow for Player 2)
        bool jumpPressed = useArrowKeys ? Keyboard.current.upArrowKey.wasPressedThisFrame
                                        : Keyboard.current.wKey.wasPressedThisFrame;

        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false; // Prevent mid-air jumping
        }

        // 3. Wand attack: left click (not on the HUD) fires an orb toward the mouse,
        //    Space fires straight ahead
        bool clickedWorld = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());

        if ((Keyboard.current.spaceKey.wasPressedThisFrame || clickedWorld) && wizard != null)
        if (shooter != null && Camera.main != null)
        {
            if (clickedWorld)
            {
                Vector3 mouse = Mouse.current.position.ReadValue();
                mouse.z = -Camera.main.transform.position.z;
                shooter.FireAt(Camera.main.ScreenToWorldPoint(mouse));
            }
            else if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                float facing = wizard != null && !wizard.FacingRight ? -1f : 1f;
                shooter.FireAt((Vector2)transform.position + new Vector2(facing * 10f, 1.5f));
            }
        }

        // 4. Death check (falling below y = -8)
        if (transform.position.y <= -8)
        {
            bool allPlayersDead = true;

            foreach (var player in activePlayers)
            {
                // If any player is still alive (above or at y > -8), then not everyone is dead yet
                if (player != null && player.transform.position.y > -8)
                {
                    allPlayersDead = false;
                    break;
                }
            }

            if (allPlayersDead)
            {
                SceneManager.LoadScene("GameMenu");
            }
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