using UnityEngine;
using UnityEngine.SceneManagement;
public class Spike : MonoBehaviour
{
    // Called when any Collider2D enters this trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        // If the thing we hit is tagged "Player" (the robot) or "Package"
        if (other.CompareTag("Package"))
        {
            
            // Destroy that GameObject
            Destroy(other.gameObject);
        }else if(other.CompareTag("Player")){
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);   
        }

    }
}
