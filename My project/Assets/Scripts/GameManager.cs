using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MusicPlayer))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Tooltip("Drag in your Player prefab here")]
    public GameObject playerPrefab;

    private MusicPlayer musicPlayer;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Cache the MusicPlayer component
        musicPlayer = GetComponent<MusicPlayer>();
        if (musicPlayer == null)
            Debug.LogError("[GameManager] Missing MusicPlayer component on GameManager!");

        // Subscribe to scene‐loaded events
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Begin playback immediately
        if (musicPlayer != null)
            musicPlayer.PlayAudio();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (musicPlayer != null)
        {
            // If we're in a menu (scene names starting with "M"), resume BGM
            if (scene.name.StartsWith("M"))
            {
                musicPlayer.ResumeAudio();
            }
            // If we're in a level (scene names starting with "L"), stop BGM
            else if (scene.name.StartsWith("L"))
            {
                musicPlayer.StopAudio();
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe when this instance is destroyed
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
