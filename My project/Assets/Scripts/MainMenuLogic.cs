using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLogic : MonoBehaviour
{
    // Music


    public void StartGame(){
        SceneManager.LoadScene("SelectLevelMenu");
    }

    public void QuitGame(){
        Application.Quit();
    }

}
