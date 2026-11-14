using UnityEngine;
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

    public bool IsGrounded => isGrounded;

    [Header("Player Assignment")]
    [Tooltip("Check this if this script instance represents Player 2.")]
    public bool isPlayerTwo = false;

    [Header("Controls Setup")]
    [Tooltip("Check this box on Player 2's script component so they use Arrow Keys instead of WASD (if using keyboard)")]
    public bool useArrowKeys = false;

    [Header("Controller Setup (PS4 / Xbox)")]
    [Tooltip("Check this to use a connected Gamepad instead of keyboard keys for this player.")]
    public bool useGamepad = false;
    [Tooltip("0 for the first plugged-in controller, 1 for the second controller.")]
    public int gamepadIndex = 0;

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

        var slippery = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        foreach (var playerCollider in GetComponents<Collider2D>())
        {
            playerCollider.sharedMaterial = slippery;
        }

        // --- AUTOMATIC CONTROLLER & PLAYER ASSIGNMENT LOGIC ---
        int controllerCount = Gamepad.all.Count;

        if (controllerCount == 0)
        {
            // 0 Controllers attached: Player 1 uses keyboard, Player 2 uses keyboard (arrow keys)
            if (!isPlayerTwo)
            {
                useGamepad = false;
                useArrowKeys = false;
            }
            else
            {
                useGamepad = false;
                useArrowKeys = true;
            }
        }
        else if (controllerCount == 1)
        {
            // 1 Controller attached: Player 1 uses keyboard, Player 2 uses Gamepad 0
            if (!isPlayerTwo)
            {
                useGamepad = false;
                useArrowKeys = false;
            }
            else
            {
                useGamepad = true;
                gamepadIndex = 0;
            }
        }
        else
        {
            // 2 or more controllers attached: Player 1 uses Gamepad 0, Player 2 uses Gamepad 1
            if (!isPlayerTwo)
            {
                useGamepad = true;
                gamepadIndex = 0;
            }
            else
            {
                useGamepad = true;
                gamepadIndex = 1;
            }
        }
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float moveInput = 0f;
        bool jumpPressed = false;
        bool attackPressed = false;

        // --- GAMEPAD INPUT ---
        if (useGamepad)
        {
            Gamepad pad = null;
            if (Gamepad.all.Count > gamepadIndex)
            {
                pad = Gamepad.all[gamepadIndex];
            }
            else if (Gamepad.current != null)
            {
                pad = Gamepad.current;
            }

            if (pad != null)
            {
                moveInput = pad.leftStick.x.ReadValue();
                if (Mathf.Abs(moveInput) < 0.2f) moveInput = 0f;
                else moveInput = Mathf.Sign(moveInput);

                jumpPressed = pad.buttonSouth.wasPressedThisFrame;
                attackPressed = pad.rightTrigger.wasPressedThisFrame;
            }
        }
        // --- KEYBOARD INPUT ---
        else
        {
            if (Keyboard.current != null)
            {
                if (useArrowKeys)
                {
                    if (Keyboard.current.leftArrowKey.isPressed) moveInput = -1f;
                    if (Keyboard.current.rightArrowKey.isPressed) moveInput = 1f;
                    jumpPressed = Keyboard.current.upArrowKey.wasPressedThisFrame;
                }
                else
                {
                    if (Keyboard.current.aKey.isPressed) moveInput = -1f;
                    if (Keyboard.current.dKey.isPressed) moveInput = 1f;
                    jumpPressed = Keyboard.current.wKey.wasPressedThisFrame;
                }

                bool clickedWorld = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());

                attackPressed = Keyboard.current.spaceKey.wasPressedThisFrame || clickedWorld;
            }
        }

        // Apply movement velocity
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        // Jump execution
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }

        // Wand attack handling using PlayerCrosshair
        if (attackPressed && wizard != null && shooter != null && Camera.main != null)
        {
            PlayerCrosshair crosshair = GetComponent<PlayerCrosshair>();

            if (crosshair != null)
            {
                shooter.FireAt(crosshair.GetAimWorldPosition());
            }
            else
            {
                float facing = wizard != null && !wizard.FacingRight ? -1f : 1f;
                shooter.FireAt((Vector2)transform.position + new Vector2(facing * 10f, 1.5f));
            }
        }

        // Fall death check
        if (transform.position.y <= -8)
        {
            bool allPlayersDead = true;
            foreach (var player in activePlayers)
            {
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
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                break;
            }
        }
    }
}