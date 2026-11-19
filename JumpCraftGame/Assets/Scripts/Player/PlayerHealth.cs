using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("UI Reference")]
    public Slider healthBarSlider;

    [Header("Hit Settings")]
    [Tooltip("Seconds after a hit during which further hits do nothing")]
    public float invulnerableSeconds = 0.6f;


    private float invulnerableTimer;
    private bool isDead;
    private WizardAnimator wizard;

    public bool IsDead => isDead;

    void Start()
    {
        currentHealth = maxHealth;
        wizard = GetComponent<WizardAnimator>();

        if (healthBarSlider != null)
        {
            healthBarSlider.maxValue = maxHealth;
            healthBarSlider.value = currentHealth;
        }
    }

    void Update()
    {
        invulnerableTimer -= Time.deltaTime;
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead || invulnerableTimer > 0f) return;
        invulnerableTimer = invulnerableSeconds;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (healthBarSlider != null)
        {
            healthBarSlider.value = currentHealth;
        }

        if (wizard != null)
        {
            wizard.Flash();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void DieFromFalling()
    {
        if (!isDead)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        Debug.Log(gameObject.name + " has died!");

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }

        foreach (Collider2D playerCollider in GetComponents<Collider2D>())
        {
            playerCollider.enabled = false;
        }
    }
}
