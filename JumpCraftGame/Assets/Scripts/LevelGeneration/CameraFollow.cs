using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    public Transform player;

    public float xOffset = 0f;

    private float fixedZ = 0f;
    private float fixedY = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        fixedZ = transform.position.z;
        fixedY = transform.position.y;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (player == null)
            return;
        //Sets position of the camera to the players X position -> Makes the player in the center of the screen (for x values only) at all times.
        transform.position = new Vector3(player.position.x, fixedY, fixedZ);
    }
}
