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
        SceneManager.LoadScene("SelectLevel");
        musicPlayer.StopAudio();
        SceneManager.LoadScene("TestLevel");
    }

    public void QuitGame(){
        Application.Quit();
    }

}
