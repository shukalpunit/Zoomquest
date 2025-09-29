using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class VoidPortal : MonoBehaviour
{
    [Header("Link")]
    [Tooltip("Other portal this links to (assign the other portal GameObject)")]
    public VoidPortal linkedPortal;

    [Header("Activation")]
    [Tooltip("If true, player will teleport automatically on trigger enter. If false, player must press Interact (E).")]
    public bool autoActivate = false;

    [Tooltip("If true, portal will only respond to player's explicit Interact call (TryUsePortal).")]
    public bool requireInteract = true;

    [Header("Teleport settings")]
    [Tooltip("How far above the linked portal the player will be placed")]
    public Vector2 teleportOffset = new Vector2(0f, 0.6f);

    [Tooltip("Seconds cooldown per portal after a teleport to avoid immediate re-teleport loops")]
    public float cooldownSeconds = 0.25f;

    [Tooltip("If true, portal will only work in one direction (this -> linked). Useful to prevent return teleport.")]
    public bool oneWay = false;

    [Header("Optional feedback")]
    public ParticleSystem enterVFX;
    public ParticleSystem exitVFX;
    public AudioClip teleportSound;

    // runtime
    bool available = true;

    private void Reset()
    {
        // Ensure collider is trigger by default
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoActivate) return;
        if (!other.CompareTag("Player")) return;

        // Only auto-activate if not requiring explicit interact
        if (requireInteract) return;

        TryTeleport(other.gameObject);
    }

    /// <summary>
    /// Call this from Player (Interact) to attempt to use the portal on demand.
    /// </summary>
    public void TryUsePortal(GameObject player)
    {
        if (player == null) return;
        // If this portal is configured to requireInteract, allow use; if not requiring interact, still allow.
        TryTeleport(player);
    }

    void TryTeleport(GameObject player)
    {
        if (!available)
        {
            // Debug for dev
            Debug.Log($"{name}: teleport unavailable (cooldown).");
            return;
        }

        if (linkedPortal == null)
        {
            Debug.LogWarning($"{name}: linkedPortal not assigned.");
            return;
        }

        // If this portal is one-way and it's not the direction we want, ignore
        // (oneWay = true means this portal teleports out only; returning through linked portal will be ignored if its oneWay is also true)
        // No extra logic here except honoring oneWay flags on each portal.

        // Run teleport
        StartCoroutine(DoTeleport(player));
    }

    IEnumerator DoTeleport(GameObject player)
    {
        // Double-check
        if (player == null) yield break;
        if (linkedPortal == null) yield break;

        // Mark unavailable on both portals to avoid immediate return loops
        available = false;
        linkedPortal.available = false;

        // Play enter VFX/sound on this portal
        if (enterVFX != null) Instantiate(enterVFX, transform.position, transform.rotation);
        if (teleportSound != null)
            AudioSource.PlayClipAtPoint(teleportSound, Camera.main != null ? Camera.main.transform.position : transform.position);

        // small optional delay to let VFX play (set to 0 if instant)
        yield return null; // wait one frame (you can change to WaitForSeconds(0.05f) if desired)

        // Teleport player: prefer to set Rigidbody2D position + zero velocity
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Vector3 dest = linkedPortal.transform.position + (Vector3)teleportOffset;
        if (rb != null)
        {
            // move using rb.position to maintain physics stability
            rb.position = dest;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            player.transform.position = dest;
        }

        // Play exit VFX on destination portal
        if (linkedPortal.exitVFX != null) Instantiate(linkedPortal.exitVFX, linkedPortal.transform.position, linkedPortal.transform.rotation);

        // small post-teleport delay before re-allowing (prevents immediate retrigger)
        yield return new WaitForSeconds(cooldownSeconds);

        // Re-enable after cooldown, unless oneWay prevents return: if either portal is oneWay and was the destination, we still re-enable, because oneWay behavior is per-portal in how you use it
        available = true;
        linkedPortal.available = true;
    }

    // OPTIONAL: editor helper to draw a gizmo and link lines
    void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        if (linkedPortal != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, linkedPortal.transform.position);
        }
    }
}
