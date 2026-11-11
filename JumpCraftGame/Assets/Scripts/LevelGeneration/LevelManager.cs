using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public Transform player;
    public PlatformGenerator platformGenerator;
    public GameObject startingPlatformPrefab;

    public int currentLevel = 1;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void AdvanceLevel()
    {
        currentLevel++;

        platformGenerator.currentLevel = currentLevel;

        Debug.Log("Advancing to Level " + currentLevel);

        // Reset platform generation and create a new starting platform
        Vector3 newPlayerPosition = platformGenerator.ResetForNewLevel(
            startingPlatformPrefab
        );

        // Move the player to the new starting position
        player.position = newPlayerPosition;

        Debug.Log("Level " + currentLevel + " started!");
    }
}
