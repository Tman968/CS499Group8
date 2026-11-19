using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public Transform player;
    public Transform player2;

    public PlatformGenerator platformGenerator;
    public GameObject startingPlatformPrefab;

    public int currentLevel = 1;

    public void AdvanceLevel()
    {
        currentLevel++;
        platformGenerator.currentLevel = currentLevel;

        Debug.Log("Advancing to Level " + currentLevel);

        Vector3 newPlayerPosition =
            platformGenerator.ResetForNewLevel(startingPlatformPrefab);

        bool player1Alive = player != null &&
                            !player.GetComponent<PlayerHealth>().IsDead;

        bool player2Alive = player2 != null &&
                            !player2.GetComponent<PlayerHealth>().IsDead;

        if (player1Alive)
            player.position = newPlayerPosition;

        if (player2Alive)
        {
            player2.position = player1Alive
                ? newPlayerPosition + Vector3.right
                : newPlayerPosition;
        }

        Debug.Log("Level " + currentLevel + " started!");
    }
}