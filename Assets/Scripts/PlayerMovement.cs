using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    public Rigidbody2D rb;
    [Header("Movement")]
    public float moveSpeed = 5f;
    float horizontalMovement;

    [Header("Jumping")]
    public float jumpPower = 10f;
    public int maxJumps = 2;
    private int jumpsRemaining;

    [Header("GroundCheck")]
    public Transform groundCheckPos;
    public Vector2 groundCheckSize = new Vector2(0.49f, 0.03f);
    public LayerMask groundLayer;

    [Header("Gravity")]
    public float baseGravity = 2f;
    public float maxFallSpeed = 18f;
    public float fallGravityMult = 2f;

    // --- INTERACT ---
    [Header("Interact")]
    public float interactDistance = 1.2f;
    public LayerMask interactableLayer = ~0; // default: everything
    private bool facingRight = true; // tracked from last horizontal input

    // Input subscription
    PlayerInput playerInput;
    [Tooltip("Name of the Interact action inside your Input Actions asset (case sensitive)")]
    public string interactActionName = "Interact";

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    void OnEnable()
    {
        // Subscribe to the Interact action performed callback if available
        if (playerInput != null && playerInput.actions != null)
        {
            var action = playerInput.actions.FindAction(interactActionName);
            if (action != null)
            {
                action.performed += OnInteract;
                Debug.Log($"PlayerMovement: Subscribed to action '{interactActionName}'.");
            }
            else
            {
                Debug.LogWarning($"PlayerMovement: action '{interactActionName}' not found in PlayerInput.actions. Check spelling and InputActionAsset.");
            }
        }
        else
        {
            Debug.LogWarning("PlayerMovement: PlayerInput or PlayerInput.actions is null.");
        }
    }

    void OnDisable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            var action = playerInput.actions.FindAction(interactActionName);
            if (action != null) action.performed -= OnInteract;
        }
    }

    void Update()
    {
        // move (keeps your original linearVelocity usage)
        rb.linearVelocity = new Vector2(horizontalMovement * moveSpeed, rb.linearVelocity.y);

        // update facing based on input (only when there's non-zero horizontal input)
        if (horizontalMovement > 0.01f) facingRight = true;
        else if (horizontalMovement < -0.01f) facingRight = false;

        // falling gravity
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = baseGravity * fallGravityMult; //fall faster and faster
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallSpeed)); //max fall speed
        }
        else
        {
            rb.gravityScale = baseGravity;
        }

        GroundCheck();

        // QUICK FALLBACK: if Input System subscription somehow fails, allow keyboard 'E' for quick debug
        // (Remove this in final build if you don't want direct keyboard checks)
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("PlayerMovement: E pressed (fallback keyboard). Running DoInteract()");
            DoInteract();
        }
    }

    public void Move(InputAction.CallbackContext context)
    {
        horizontalMovement = context.ReadValue<Vector2>().x;
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (jumpsRemaining > 0)
        {
            if (context.performed)
            {
                //Hold down jump button = full height
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
                jumpsRemaining--;
            }
            else if (context.canceled && rb.linearVelocity.y > 0)
            {
                //Light tap of jump button = half the height
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
                jumpsRemaining--;
            }
        }
    }

    // This is the callback subscribed to the InputAction (via code above).
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("PlayerMovement: Interact action performed (subscription).");
            DoInteract();
        }
    }

    // Parameterless method kept for backward compatibility if you still want to call it from UnityEvents
    public void DoInteract()
    {
        Vector2 origin = GetFacingOrigin(out Vector2 dir);

        // visual debug in Scene view
        Debug.DrawRay(origin, dir * interactDistance, Color.cyan, 1.0f);

        // Try RaycastAll first
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, interactDistance, interactableLayer);
        if (hits != null && hits.Length > 0)
        {
            Debug.Log($"DoInteract: RaycastAll found {hits.Length} hits.");
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                Debug.Log($"  Ray hit: {h.collider.name} layer={LayerMask.LayerToName(h.collider.gameObject.layer)} isTrigger={h.collider.isTrigger}");
                TryHandleHit(h.collider);
            }
            return;
        }

        // If ray hit nothing, try small overlap circle (good for triggers or slightly offset colliders)
        Vector2 circleCenter = origin + dir * (interactDistance * 0.5f);
        float circleRadius = 0.4f;
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(circleCenter, circleRadius, interactableLayer);
        if (overlaps != null && overlaps.Length > 0)
        {
            Debug.Log($"DoInteract: OverlapCircleAll found {overlaps.Length} hits (center={circleCenter}, r={circleRadius}).");
            foreach (var c in overlaps)
            {
                Debug.Log($"  Overlap hit: {c.name} layer={LayerMask.LayerToName(c.gameObject.layer)} isTrigger={c.isTrigger}");
                TryHandleHit(c);
            }
            return;
        }

        Debug.Log("DoInteract: nothing hit (ray & overlap empty).");
    }

    // Helper that returns facing origin and direction
    Vector2 GetFacingOrigin(out Vector2 dir)
    {
        Vector2 origin = transform.position;
        dir = facingRight ? Vector2.right : Vector2.left;
        return origin;
    }

    // Handles a collider hit: checks parent chain too
    void TryHandleHit(Collider2D col)
    {
        if (col == null) return;

        // Terminal (check on collider or parent)
        var terminal = col.GetComponent<Terminal>() ?? col.GetComponentInParent<Terminal>();
        if (terminal != null)
        {
            Debug.Log($"DoInteract -> calling Terminal.TryActivate() on {terminal.gameObject.name}");
            terminal.TryActivate();
            return;
        }

        // Door
        var door = col.GetComponent<Door>() ?? col.GetComponentInParent<Door>();
        if (door != null)
        {
            Debug.Log($"DoInteract -> calling Door.TryOpen() on {door.gameObject.name}");
            door.TryOpen();
            return;
        }

        // VoidPortal
        //var portal = col.GetComponent<VoidPortal>() ?? col.GetComponentInParent<VoidPortal>();
        //if (portal != null)
        //{
        //    Debug.Log($"DoInteract -> calling VoidPortal.TryUsePortal() on {portal.gameObject.name}");
        //    portal.TryUsePortal(this.gameObject);
        //    return;
        //}

        Debug.Log($"DoInteract: hit {col.name} but no Terminal/Door/VoidPortal component found in parents.");
    }

    private void GroundCheck()
    {
        if (Physics2D.OverlapBox(groundCheckPos.position, groundCheckSize, 0, groundLayer)) //checks if set box overlaps with ground
        {
            jumpsRemaining = maxJumps;
        }
    }

    private void OnDrawGizmosSelected()
    {
        //Ground check visual
        Gizmos.color = Color.white;
        if (groundCheckPos != null) Gizmos.DrawWireCube(groundCheckPos.position, groundCheckSize);

        // Interact ray visual (editor only)
        Gizmos.color = Color.cyan;
        Vector3 origin = transform ? transform.position : Vector3.zero;
        Vector3 dir = facingRight ? Vector3.right : Vector3.left;
        Gizmos.DrawLine(origin, origin + dir * interactDistance);
    }
}
