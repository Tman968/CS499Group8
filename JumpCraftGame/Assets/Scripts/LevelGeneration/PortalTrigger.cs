using UnityEngine;

public class PortalTrigger : MonoBehaviour
{
    public LevelManager levelManager;

    private bool player1Entered;
    private bool player2Entered;
    private bool transitionStarted;


    private void Update()
    {
        if (!transitionStarted)
        {
            CheckPlayers();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (transitionStarted) return;

        if (other.CompareTag("Player1"))
            player1Entered = true;

        if (other.CompareTag("Player2"))
            player2Entered = true;

        CheckPlayers();
    }

    private void CheckPlayers()
    {
        GameObject player1 = GameObject.FindWithTag("Player1");
        GameObject player2 = GameObject.FindWithTag("Player2");

        PlayerHealth health1 = player1 != null
            ? player1.GetComponent<PlayerHealth>()
            : null;

        PlayerHealth health2 = player2 != null
            ? player2.GetComponent<PlayerHealth>()
            : null;

        bool player1Ready = player1 == null ||
                            (health1 != null && health1.IsDead) ||
                            player1Entered;

        bool player2Ready = player2 == null ||
                            (health2 != null && health2.IsDead) ||
                            player2Entered;

        if (player1Ready && player2Ready)
        {
            transitionStarted = true;
            levelManager.AdvanceLevel();
        }
    }
}