using System.Collections.Generic;
using UnityEngine;

public class PlatformGenerator : MonoBehaviour
{
    public Transform player;
    public Transform startingPlatform;
    public GameObject platformPrefab;
    public GameObject portalPrefab;
    public Camera mainCamera;

    public float gapBetweenPlatforms = 4f;
    public float platformY = 0f;
    public float spawnAheadDistance = 28f;
    private float platformZ = 0f;

    //Multi-Level Generation
    public int currentLevel = 1;
    public float portalMultiplier = 1.25f;
    public int baseMinPortalPlatforms = 4;
    public int baseMaxPortalPlatforms = 7;
    private int minPortalPlatforms;
    private int maxPortalPlatforms;
    public LevelManager levelManager;
    private int portalSpawnIndex;
    private int platformCount = 0;
    private bool portalSpawned = false;

    private Vector3 levelStartPosition;

    //Random Y-Values
    public float minY = -5f;
    public float maxY = 0f;
    public float maxJumpHeight = 2.5f; //Max difference in +y that a platform can spawn from the previous
    private float previousPlatformY = -4f;


    public float cleanupBehindDistance = 15f;

    private List<GameObject> spawnedPlatforms =
        new List<GameObject>();

    private float nextSpawnX;
    private float platformWidth;

    void Start()
    {
        platformZ = startingPlatform.position.z;
        levelStartPosition = startingPlatform.position;
        // Get the width of the regular platform
        SpriteRenderer platformRenderer =
            platformPrefab.GetComponentInChildren<SpriteRenderer>();

        platformWidth = platformRenderer.bounds.size.x;

        // Find the right edge of the starting platform
        SpriteRenderer startRenderer =
            startingPlatform.GetComponentInChildren<SpriteRenderer>();

        float startRightEdge = startRenderer.bounds.max.x;

        platformZ = startingPlatform.position.z;

        // Set the first platform's spawn position
        nextSpawnX = startRightEdge + gapBetweenPlatforms + platformWidth / 2f;

        //Generate the random platform count for the level (when the portal will spawn)
        CalculatePortalRange();
        portalSpawnIndex = Random.Range(minPortalPlatforms, maxPortalPlatforms + 1);
        Debug.Log("Level " + currentLevel +
          " portal will spawn at platform " + portalSpawnIndex);

        GeneratePlatforms();
    }

    void Update()
    {
        GeneratePlatforms();
        //CleanupPlatforms();
    }

    void GeneratePlatforms()
    {
        float spawnLimit = player.position.x + spawnAheadDistance;
        float randomHeight = 0f;
        float highestY = 0f;

        while (nextSpawnX < spawnLimit && !portalSpawned)
        {
            highestY = Mathf.Min(maxY, previousPlatformY + maxJumpHeight);
            randomHeight = Random.Range(minY, highestY);

            Vector3 spawnPosition = new Vector3(nextSpawnX, randomHeight, platformZ);

            GameObject prefabToSpawn;

            platformCount++;

            if (platformCount == portalSpawnIndex)
            {
                prefabToSpawn = portalPrefab;
                portalSpawned = true;

                Debug.Log("Spawning portal at platform " + platformCount);
            }
            else
            {
                prefabToSpawn = platformPrefab;
            }

            GameObject newPlatform = Instantiate(
                prefabToSpawn,
                spawnPosition,
                Quaternion.identity
            );

            // Connect the portal to the LevelManager
            if (prefabToSpawn == portalPrefab)
            {
                PortalTrigger portalTrigger =
                    newPlatform.GetComponentInChildren<PortalTrigger>();

                portalTrigger.levelManager = levelManager;
            }

            spawnedPlatforms.Add(newPlatform);

            nextSpawnX += platformWidth + gapBetweenPlatforms;

            previousPlatformY = newPlatform.transform.position.y;
        }
    }

    /*void GeneratePlatforms()               *******Old Method --- Saved for backup********
    {
        float spawnLimit = player.position.x + spawnAheadDistance;
        float randomHeight = 0f;
        float highestY = 0f;
        while (nextSpawnX < spawnLimit)
        {
            highestY = Mathf.Min(maxY, previousPlatformY + maxJumpHeight);
            randomHeight = Random.Range(minY, highestY);
            //Vector3 spawnPosition = new Vector3(nextSpawnX,platformY,platformZ);
            Vector3 spawnPosition = new Vector3(nextSpawnX, randomHeight, platformZ);

            GameObject newPlatform = Instantiate(platformPrefab,spawnPosition,Quaternion.identity);
            Debug.Log("Spawned platform at Y: " + randomHeight);

            spawnedPlatforms.Add(newPlatform);

            nextSpawnX += platformWidth + gapBetweenPlatforms;

            previousPlatformY = newPlatform.transform.position.y;
        }
    }*/

    void CleanupPlatforms()
    {

        //Deletion of Normal Platforms
        float cameraLeftEdge = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;

        float cleanupLimit = cameraLeftEdge - cleanupBehindDistance;

        for (int i = spawnedPlatforms.Count - 1; i >= 0; i--)
        {
            GameObject platform = spawnedPlatforms[i];

            SpriteRenderer renderer =
                platform.GetComponentInChildren<SpriteRenderer>();

            if (renderer.bounds.max.x < cleanupLimit)
            {
                Destroy(platform);
                spawnedPlatforms.RemoveAt(i);
            }
        }

        // Deletion of Starting Platform
        if (startingPlatform != null)
        {
            SpriteRenderer renderer =
                startingPlatform.GetComponentInChildren<SpriteRenderer>();

            if (renderer.bounds.max.x < cleanupLimit)
            {
                Debug.Log("STARTING PLATFORM DESTROYED BY CLEANUP!");
                Destroy(startingPlatform.gameObject);
                startingPlatform = null;
            }
        }
    }


    public Vector3 ResetForNewLevel(GameObject startingPlatformPrefab)
    {
        // Delete all remaining platforms from the previous level
        foreach (GameObject platform in spawnedPlatforms)
        {
            Destroy(platform);
        }

        spawnedPlatforms.Clear();

        // Delete the previous starting platform if it still exists
        if (startingPlatform != null)
        {
            Destroy(startingPlatform.gameObject);
        }

        // Create a new starting platform at the original level position
        GameObject newStartingPlatform = Instantiate(
            startingPlatformPrefab,
            levelStartPosition,
            Quaternion.identity
        );

        startingPlatform = newStartingPlatform.transform;

        Debug.Log("NEW STARTING PLATFORM CREATED AT: " + startingPlatform.position);

        // Reset platform generation variables
        platformCount = 0;
        portalSpawned = false;
        previousPlatformY = -4f;
        platformZ = startingPlatform.position.z;

        // Calculate the next platform spawn position
        SpriteRenderer startRenderer =
            startingPlatform.GetComponentInChildren<SpriteRenderer>();

        float startRightEdge = startRenderer.bounds.max.x;

        nextSpawnX = startRightEdge + gapBetweenPlatforms + platformWidth / 2f;

        CalculatePortalRange();

        // Select a new portal position
        portalSpawnIndex = Random.Range(
            minPortalPlatforms,
            maxPortalPlatforms + 1
        );

        Debug.Log("Starting Level. Portal will spawn at platform " + portalSpawnIndex);

        // Return the player's new spawn position above the starting platform
        return new Vector3(
            startingPlatform.position.x,
            startRenderer.bounds.max.y + 2f,
            startingPlatform.position.z
        );
    }

    void CalculatePortalRange()
    {
        minPortalPlatforms = Mathf.RoundToInt(
            baseMinPortalPlatforms * Mathf.Pow(portalMultiplier, currentLevel - 1)
        );

        maxPortalPlatforms = Mathf.RoundToInt(
            baseMaxPortalPlatforms * Mathf.Pow(portalMultiplier, currentLevel - 1)
        );
    }
}
