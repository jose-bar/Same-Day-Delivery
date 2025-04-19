using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class BGMSceneManager : MonoBehaviour
{
    [Tooltip("List the exact names of your level scenes here")]
    public string[] levelSceneNames = {"Level1","Level2","Level3","Level4"};

    private AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;     // don’t auto‐play on startup
        DontDestroyOnLoad(gameObject);   // keep this around between loads

        // watch for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // is this one of our level scenes?
        bool isLevel = false;
        for (int i = 0; i < levelSceneNames.Length; i++)
        {
            if (scene.name == levelSceneNames[i])
            {
                isLevel = true;
                break;
            }
        }

        if (isLevel)
        {
            if (!_audio.isPlaying)
                _audio.Play();
        }
        else
        {
            if (_audio.isPlaying)
                _audio.Stop();
        }
    }
}