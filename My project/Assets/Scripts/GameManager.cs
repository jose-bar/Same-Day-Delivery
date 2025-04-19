using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [Tooltip("Drag in your Player prefab here")]
    public GameObject playerPrefab;

    private void Awake()
    {
        // singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else Destroy(gameObject);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // only spawn in your actual level scenes
        if (!scene.name.StartsWith("0")) return;

        // find the spawn point
        // var spawn = GameObject.FindWithTag("PlayerSpawn");
        // if (spawn == null)
        // {
        //     Debug.LogError("No PlayerSpawn found in scene " + scene.name);
        //     return;
        // }

        // instantiate the player prefab at that position
        //Instantiate(playerPrefab, spawn.transform.position, Quaternion.identity);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
