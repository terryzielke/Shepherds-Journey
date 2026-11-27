using UnityEngine;

namespace ShepherdsJourney.Puzzles
{
    /// <summary>
    /// A door that slides between a closed and an open position.
    /// Its public methods can be hooked to any UnityEvent (levers now,
    /// heavy buttons and other puzzle triggers later).
    /// </summary>
    
    
    public class PuzzleDoor : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("How far the door moves when open, in the door's parent space (default: up 3 units).")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 3f, 0f);
        [SerializeField] private float openSpeed = 3f;

        [Header("State")]
        [SerializeField] private bool startsOpen = false;

        public bool IsOpen { get; private set; }

        private Vector3 closedPosition;
        private Vector3 openPosition;

        private void Awake()
        {
            // The position you place the door at in the scene is the CLOSED position.
            closedPosition = transform.localPosition;
            openPosition = closedPosition + openOffset;

            IsOpen = startsOpen;
            if (IsOpen)
                transform.localPosition = openPosition;
        }

        private void Update()
        {
            Vector3 target = IsOpen ? openPosition : closedPosition;
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, target, openSpeed * Time.deltaTime);
        }

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
        public void Toggle() => IsOpen = !IsOpen;
        public void SetOpen(bool open) => IsOpen = open;

        private void OnDrawGizmosSelected()
        {
            // Shows where the door will end up when open.
            Vector3 baseLocal = Application.isPlaying ? closedPosition : transform.localPosition;
            Vector3 openLocal = baseLocal + openOffset;
            Vector3 world = transform.parent != null
                ? transform.parent.TransformPoint(openLocal)
                : openLocal;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(world, 0.25f);
            Gizmos.DrawLine(transform.position, world);
        }
    }
}