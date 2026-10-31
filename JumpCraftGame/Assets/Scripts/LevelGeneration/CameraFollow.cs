using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    public Transform player;

    public float xOffset = 0f;

    [Header("Vertical Follow")]
    //The camera height follows the height of the ground the player is standing on (not every jump), mapped from the lowest platform to the highest one.
    //Defaults match PlatformGenerator (minY -5, maxY 3): a platform's top is about 1.2 above its position.
    public float lowestGroundY = -3.8f;
    public float highestGroundY = 4.2f;

    //Camera height when standing on the lowest and the highest platform. At maxY the mountain peaks in the backdrop come into view.
    public float minY = 0f;
    public float maxY = 3.5f;

    //Space kept between the player's feet and the top of the screen, so a jump never goes off screen.
    public float headroom = 2.5f;
    public float verticalSmoothTime = 0.35f;

    private float fixedZ = 0f;
    private float groundY = 0f;
    private float yVelocity = 0f;
    private Camera cam;
    private PlayerMovement playerMovement;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        fixedZ = transform.position.z;
        cam = GetComponent<Camera>();
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            groundY = player.position.y;
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (player == null)
            return;

        //Only update the ground height when the player lands, so the camera doesn't bob up and down with every jump.
        if (playerMovement == null || playerMovement.IsGrounded)
            groundY = player.position.y;

        float t = Mathf.InverseLerp(lowestGroundY, highestGroundY, groundY);
        float targetY = Mathf.Lerp(minY, maxY, t);

        //Follow the player up if they jump higher than the screen shows, and down when they fall, but never below minY.
        targetY = Mathf.Max(targetY, player.position.y + headroom - cam.orthographicSize);
        targetY = Mathf.Min(targetY, player.position.y + cam.orthographicSize - 1f);
        targetY = Mathf.Max(targetY, minY);

        float y = Mathf.SmoothDamp(transform.position.y, targetY, ref yVelocity, verticalSmoothTime);

        //Sets position of the camera to the players X position -> Makes the player in the center of the screen (for x values only) at all times.
        transform.position = new Vector3(player.position.x, y, fixedZ);
    }
}
