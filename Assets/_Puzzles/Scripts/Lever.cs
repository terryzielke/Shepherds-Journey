using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ShepherdsJourney.Puzzles
{
    /// <summary>
    /// A lever the player can pull when standing inside its trigger collider.
    /// It does not know about doors: hook up whatever it should control
    /// through the onActivated / onDeactivated events in the Inspector.
    /// </summary>
    public class Lever : MonoBehaviour
    {
        [Header("Visuals")]
        [Tooltip("The part of the lever that swings. Leave empty to skip the animation.")]
        [SerializeField] private Transform handle;
        [SerializeField] private Vector3 offEulerAngles = new Vector3(-35f, 0f, 0f);
        [SerializeField] private Vector3 onEulerAngles = new Vector3(35f, 0f, 0f);
        [SerializeField] private float swingSpeed = 8f;

        [Header("Behaviour")]
        [SerializeField] private bool startsOn = false;
        [Tooltip("If true, the lever can only be pulled once (stays on).")]
        [SerializeField] private bool oneShot = false;
        [Tooltip("If above 0, the lever flips back off by itself after this many seconds, and can't be pulled while it is on. 0 = off.")]
        [SerializeField] private float autoResetSeconds = 0f;
        [SerializeField] private string playerTag = "Player";

        [Header("Events")]
        public UnityEvent onActivated;
        public UnityEvent onDeactivated;
        [Tooltip("Use these to show / hide a 'Press E' prompt.")]
        public UnityEvent onPlayerEnterRange;
        public UnityEvent onPlayerExitRange;

        [Header("Debug")]
        [Tooltip("Prints messages in the Console to help find setup problems. Turn off when it works.")]
        [SerializeField] private bool debugLogs = true;

        public bool IsOn { get; private set; }

        private bool playerInRange;
        private Quaternion targetRotation;
        private float resetTimer;

        private void Reset()
        {
            // Adds a trigger zone the first time the script is attached.
            if (GetComponent<Collider>() == null)
            {
                var zone = gameObject.AddComponent<SphereCollider>();
                zone.radius = 1.5f;
                zone.isTrigger = true;
            }
        }

        private void Start()
        {
            IsOn = startsOn;
            resetTimer = IsOn ? autoResetSeconds : 0f;
            targetRotation = Quaternion.Euler(IsOn ? onEulerAngles : offEulerAngles);

            if (handle != null)
                handle.localRotation = targetRotation;
            else
                Log("No Handle assigned, so the lever will not swing (events still work).");

            Collider zone = GetComponent<Collider>();
            if (zone == null || !zone.isTrigger)
                Log("The Lever object has no trigger collider. Add a Sphere Collider and tick Is Trigger.");

            // Fire the matching event once so connected objects start in sync.
            (IsOn ? onActivated : onDeactivated)?.Invoke();
        }

        private void Update()
        {
            if (InteractPressed())
            {
                if (playerInRange)
                    Interact();
                else
                    Log("E pressed, but the lever does not think the player is in range.");
            }

            if (IsOn && autoResetSeconds > 0f)
            {
                resetTimer -= Time.deltaTime;
                if (resetTimer <= 0f)
                    SetState(false);
            }

            if (handle != null)
            {
                handle.localRotation = Quaternion.Slerp(
                    handle.localRotation, targetRotation, swingSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Public so another system (for example the team's player interaction
        /// script) can call this instead of the built-in key check.
        /// </summary>
        public void Interact()
        {
            // Once pulled, a one-shot or timed lever ignores further pulls.
            if (IsOn && (oneShot || autoResetSeconds > 0f)) return;
            SetState(!IsOn);
        }

        public void SetState(bool on)
        {
            if (IsOn == on) return;

            IsOn = on;
            targetRotation = Quaternion.Euler(IsOn ? onEulerAngles : offEulerAngles);
            resetTimer = IsOn ? autoResetSeconds : 0f;

            UnityEvent evt = IsOn ? onActivated : onDeactivated;
            Log("Lever turned " + (IsOn ? "ON" : "OFF") + ". Listeners on "
                + (IsOn ? "On Activated" : "On Deactivated") + ": " + evt.GetPersistentEventCount());
            evt?.Invoke();
        }

        private static bool InteractPressed()
        {
            bool key = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            bool pad = Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
            return key || pad;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag))
            {
                Log("Something entered the zone but was ignored: '" + other.name
                    + "' has tag '" + other.tag + "' (needs '" + playerTag + "').");
                return;
            }

            playerInRange = true;
            Log("Player entered the zone.");
            onPlayerEnterRange?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            playerInRange = false;
            Log("Player left the zone.");
            onPlayerExitRange?.Invoke();
        }

        private void Log(string message)
        {
            if (debugLogs)
                Debug.Log("[Lever] " + message, this);
        }
    }
}