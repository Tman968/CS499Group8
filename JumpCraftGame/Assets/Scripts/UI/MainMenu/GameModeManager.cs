using UnityEngine;
using UnityEngine.SceneManagement;

public class GameModeManager : MonoBehaviour
{
    
    //public static GameModeManager Instance; //Not needed if Awake() is also inactive

    // True = 2-Player Co-op, False = Single Player
    public static bool IsMultiplayer = false;

    [Header("Scene Names")]
    public string gameplaySceneName = "GameScene"; // Change to your actual gameplay scene name

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