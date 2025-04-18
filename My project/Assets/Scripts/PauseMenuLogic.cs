using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuLogic : MonoBehaviour
{
    public GameObject pauseMenu;
    public static bool paused = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
      pauseMenu.SetActive(false);  
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!paused)
            {
                PauseGame();
            }else{
                ResumeGame();
            }
        }
    }


    public void PauseGame(){
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        paused = true;
    }

    public void ResumeGame(){
        pauseMenu.SetActive(false); 
        Time.timeScale = 1f;
        paused = false;
    }

    public void RetryLevel(){
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitToMenu(){
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
