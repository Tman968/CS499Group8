using UnityEngine;

public class PortalTrigger : MonoBehaviour
{
    public LevelManager levelManager;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            levelManager.AdvanceLevel();
        }
    }

}
