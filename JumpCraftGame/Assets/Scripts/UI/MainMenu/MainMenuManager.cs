using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void StartSinglePlayer()
    {
        // Loads your side-scroller gameplay scene
        Sfx.PlayFlat("menu_start");
        SceneManager.LoadScene("SpriteTest");
    }

    public void StartMultiplayer()
    {
        Debug.Log("Multiplayer menu selected.");
    }

    public void OpenPreferences()
    {
        Debug.Log("Preferences menu selected.");
    }

    public void OpenPlayerStats()
    {
        Debug.Log("Player Stats menu selected.");
    }
}