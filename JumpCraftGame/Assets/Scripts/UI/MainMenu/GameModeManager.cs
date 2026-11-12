using UnityEngine;
using UnityEngine.SceneManagement;

public class GameModeManager : MonoBehaviour
{
    
    //public static GameModeManager Instance; //Not needed if Awake() is also inactive

    // True = 2-Player Co-op, False = Single Player
    public static bool IsMultiplayer = false;

    [Header("Scene Names")]
    public string gameplaySceneName = "GameScene"; // Change to your actual gameplay scene name

    //Removed method for now caused problems when game ended, new instances would not load. Saving incase it is essential to other functions
    /*private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }*/

    // Hook this up to your normal "Play" button in the Main Menu
    public void StartSinglePlayer()
    {
        IsMultiplayer = false;
        SceneManager.LoadScene(gameplaySceneName);
    }

    // Hook this up to your "Multiplayer" button in the Main Menu
    public void StartMultiplayer()
    {
        IsMultiplayer = true;
        SceneManager.LoadScene(gameplaySceneName);
    }
}