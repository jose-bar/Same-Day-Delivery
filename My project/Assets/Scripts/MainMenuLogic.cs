using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLogic : MonoBehaviour
{
    // Music
    private musicPlayer musicPlayer;

    void Start()
    {
        musicPlayer = GetComponent<musicPlayer>();
        musicPlayer.PlayAudio();
    } 

    public void StartGame(){
        musicPlayer.StopAudio();
        SceneManager.LoadScene("Spring");
    }

    public void QuitGame(){
        Application.Quit();
    }

}
