using System.Collections.Generic;
using UnityEngine;

public class PlatformGenerator : MonoBehaviour
{
    public Transform player;
    public Transform startingPlatform;
    public GameObject platformPrefab;
    public Camera mainCamera;

    public float gapBetweenPlatforms = 10f;
    public float platformY = 0f;
    public float spawnAheadDistance = 28f;
    private float platformZ = 0f;

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

        GeneratePlatforms();
    }

    void Update()
    {
        GeneratePlatforms();
        CleanupPlatforms();
    }

    void GeneratePlatforms()
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
    }

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
                Destroy(startingPlatform.gameObject);
                startingPlatform = null;
            }
        }
    }
}
