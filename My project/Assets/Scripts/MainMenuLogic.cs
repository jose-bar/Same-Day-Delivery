using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLogic : MonoBehaviour
{
    public void StartGame(){
        SceneManager.LoadScene("TestLevel");
    }

    public void QuitGame(){
        Application.Quit();
    }

}
