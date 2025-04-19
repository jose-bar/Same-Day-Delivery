using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLogic : MonoBehaviour
{
    // Music
    private MusicPlayer musicPlayer;

    void Start()
    {
        musicPlayer = GetComponent<MusicPlayer>();
        musicPlayer.PlayAudio();
    } 

    public void StartGame(){
        SceneManager.LoadScene("LevelSectionMenu");
        musicPlayer.StopAudio();
    }

    public void QuitGame(){
        Application.Quit();
    }

}
