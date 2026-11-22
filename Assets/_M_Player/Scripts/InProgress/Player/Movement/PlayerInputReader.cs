using UnityEngine;
using UnityEngine.InputSystem;


[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput), typeof(PlayerMovement))]


public sealed class PlayerInputReader : MonoBehaviour
{
    private PlayerMovement movement;
    private InputAction moveAction;
    private InputAction jumpAction;



    /// <summary>
    /// Initializes player movement and retrieves the movement and jump input actions.
    /// Checks for missing components or inputs and disables the script if needed.
    /// </summary>
    private void Start()
    {
        movement = GetComponent<PlayerMovement>();

        // Read the players action asst once, affter PlayerInput has checked it
        InputActionAsset actions = GetComponent<PlayerInput>().actions;
        moveAction = actions != null ? actions.FindAction("Player/Move") : null;
        jumpAction = actions != null ? actions.FindAction("Player/Jump") : null;

        // If things are missing, console will promt you (me, lol) to fix it. 
        if (movement == null || jumpAction == null)
        {
            Debug.LogError("Assign an Input Actions asset with Player/Move and Player/Jump", this);
            enabled = false;
        }
    }



    /// <summary>
    /// Reads player movement and jump inputs each frame.
    /// Passes them to PlayerMovement to handle movement and physics.
    /// </summary>
    private void Update()
    {
        // An inactive action map sends neutral input. The movement script still handles gravity.
        Vector2 move = moveAction.enabled ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool jumpPressed = jumpAction.enabled && jumpAction.WasPressedThisFrame();

        // Pass intent and time. PlayerMovement decides what can actually happen.
        movement.Simulate(move, jumpPressed, Time.deltaTime);
    }
}
