using UnityEngine;

public class Door : MonoBehaviour
{
    private Collider2D doorCollider;
    private SpriteRenderer sr;

    [Header("Optional: open state visuals")]
    public Sprite openSprite;         // assign a different sprite for "open" state (optional)
    public AudioClip openSound;       // sound to play when door opens (optional)

    [Header("Optional: scene/finish")]
    [Tooltip("If set, this scene will be loaded when the player walks through this open door.")]
    public string targetSceneName;    // set to "LevelB" or empty if door should call EndGame()

    // internal state
    private bool isOpen = false;

    void Awake()
    {
        doorCollider = GetComponent<Collider2D>();
        if (doorCollider == null)
        {
            doorCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Opens the door (disables collider so player can pass).
    /// </summary>
    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;

        Debug.Log($"Door.OpenDoor() called on {gameObject.name}");

        // Prefer: make door non-blocking so player can pass
        if (doorCollider != null)
        {
            // either disable or make trigger depending on your design
            // here we set isTrigger = true so OnTriggerEnter2D fires when player passes through
            doorCollider.isTrigger = true;
        }

        if (openSprite != null && sr != null)
        {
            sr.sprite = openSprite;
        }

        if (openSound != null)
        {
            AudioSource.PlayClipAtPoint(openSound, Camera.main != null ? Camera.main.transform.position : transform.position);
        }
    }

    /// <summary>
    /// Interaction wrapper that PlayerMovement can call.
    /// If the door is closed it will tell user it's locked.
    /// If the door is open, it will either load a scene or call end game.
    /// </summary>
    public void TryOpen()
    {
        if (!isOpen)
        {
            Debug.Log("Door.TryOpen(): Door is locked.");
            // Optionally provide feedback (UI / sound) here
            return;
        }

        Debug.Log("Door.TryOpen(): Door is already open - attempting to activate passage.");

        // If the door is open and has a target scene assigned, load it.
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning("Door.TryOpen(): GameManager.Instance is null, cannot load scene.");
            }
        }
        else
        {
            // No scene assigned: treat this as an end/finish door
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndGame();
            }
            else
            {
                Debug.LogWarning("Door.TryOpen(): GameManager.Instance is null, cannot EndGame.");
            }
        }
    }

    // If the player walks through the door (when it's been opened -> collider is trigger), handle scene load there as well:
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag("Player")) return;

        Debug.Log($"Door.OnTriggerEnter2D: Player entered open door {gameObject.name}");

        if (!string.IsNullOrEmpty(targetSceneName))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoadScene(targetSceneName);
            else
                Debug.LogWarning("Door: GameManager missing, cannot load scene.");
        }
        else
        {
            if (GameManager.Instance != null)
                GameManager.Instance.EndGame();
        }
    }

    // Optional helper to close/reset the door
    public void CloseDoor()
    {
        if (!isOpen) return;
        isOpen = false;

        if (doorCollider != null)
        {
            doorCollider.isTrigger = false;
        }

        // You could restore sprite here if you saved the closed sprite
        Debug.Log("Door.CloseDoor() called.");
    }
}
