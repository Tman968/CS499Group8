using UnityEngine;

public class PlatformScore : MonoBehaviour
{
    private bool hasBeenScored = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasBeenScored)
            return;

        if (collision.gameObject.CompareTag("Player1") || collision.gameObject.CompareTag("Player2"))
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                
                if (contact.normal.y < -0.5f)
                {
                    ScoreManager.Instance.AddPoints(1);
                    hasBeenScored = true;
                    break;
                }
            }
        }
    }
}