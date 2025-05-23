using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuLogic : MonoBehaviour
{
    public GameObject pauseMenu;
    public static bool paused = false;
    
    // Access robot
    GameObject robot;

    // Sound effects
    OneSoundEffects oneSounds;

    LoopSoundEffects loopSounds;

    MusicPlayer musicPlayer;

    public BGMSceneManager bgmSceneManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
      pauseMenu.SetActive(false);  
      robot = GameObject.FindWithTag("Player");
      loopSounds = robot.GetComponent<LoopSoundEffects>();
      oneSounds = robot.GetComponent<OneSoundEffects>();
      musicPlayer = GetComponent<MusicPlayer>();
      musicPlayer.PlayAudio();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!paused)
            {
                PauseGame();
            } else{
                ResumeGame();
            }
        }
    }


    public void PauseGame(){
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        paused = true;
        
        loopSounds.PauseAudio();
        oneSounds.PauseAllAudio();
        musicPlayer.PauseAudio();
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Noisy")) {
            obj.GetComponent<ObjectSoundEffects>().PauseAudio();
        }
    }

    public void ResumeGame(){
        pauseMenu.SetActive(false); 
        Time.timeScale = 1f;
        paused = false;

        loopSounds.ResumeAudio();
        oneSounds.ResumeAllAudio();
        musicPlayer.ResumeAudio();
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Noisy")) {
            obj.GetComponent<ObjectSoundEffects>().ResumeAudio();
        }
    }

    public void RetryLevel(){
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        musicPlayer.PlayAudio();
        paused = false;
    }

    public void ExitToMenu(){
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
        paused = false;
    }
}
