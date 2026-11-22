using UnityEngine;

namespace ShepherdsJourney.Core
{
    // HOW TO USE
    //
    // 1. Inherit from Singleton and pass your own class as T:
    //        public class GameManager : Singleton<GameManager>
    // 2. Put that component on a root GameObject (no parent) in a scene.
    // 3. Other scripts use it like this:
    //        GameManager.Instance.DoSomething();
    // 4. In OnDisable or OnDestroy, use TryGetInstance instead of Instance (see TryGetInstance below).
    //
    // EXAMPLE
    //
    //     using ShepherdsJourney.Core;
    //
    //     public class GameManager : Singleton<GameManager>
    //     {
    //         // Optional: return false if this manager should be destroyed with its scene.
    //         protected override bool PersistAcrossScenes => true;
    //
    //         protected override void Awake()
    //         {
    //             base.Awake();                  // ALWAYS call this first. It does the singleton setup.
    //             if (Instance != this) return;  // This copy is a duplicate being destroyed, so skip setup.
    //
    //             // ...your own setup here...
    //         }
    //     }
    //
    // WHAT "where T : Singleton<T>" MEANS
    //
    // T must itself be a Singleton, so passing an unrelated type such as Singleton<Rigidbody>
    // fails to compile. (This pattern is called the "Curiously Recurring Template Pattern".)
    // It can't catch every mistake, though. See the check in Awake.
    //
    // WHEN NOT TO USE A SINGLETON
    //
    // Singletons are easy to reach, so it's tempting to make everything one. That hides which
    // scripts depend on what, and makes testing a scene on its own harder. Use one only for
    // true "one per game" managers. For data shared between scenes, a ScriptableObject is often
    // better. To tell other systems that something happened, use an event.

    /// <summary>
    /// Base class for a MonoBehaviour that should have exactly one copy in the game, reachable
    /// from any script through ClassName.Instance. Typical examples are a GameManager or an
    /// AudioManager. See the "HOW TO USE" notes above this class.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        // A static field inside a generic class is NOT shared by every singleton. C# makes a separate
        // copy of the class for each T, so Singleton<GameManager> and Singleton<AudioManager> each
        // have their own _instance. That's what lets one base class serve many managers.
        private static T _instance;

        /// <summary>
        /// True if a live instance is registered right now. Never searches the scene and never throws.
        /// </summary>
        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// The one instance. If it hasn't registered yet (for example, another script's Awake ran
        /// first), we search the scene for it.
        /// Throws an InvalidOperationException with a clear message if none exists. A loud error is
        /// easier to fix than a hidden auto-created object or a confusing NullReferenceException later.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (TryGetInstance(out T instance))
                {
                    return instance;
                }

                throw new System.InvalidOperationException(
                    $"No active {typeof(T).Name} found. Add one to the scene. " +
                    $"(In OnDisable/OnDestroy, use {typeof(T).Name}.TryGetInstance instead, " +
                    "because the singleton may already be destroyed while the game shuts down.)");
            }
        }

        /// <summary>
        /// Safe way to get the instance. Returns true and fills in "instance" if one exists.
        /// Returns false (and sets "instance" to null) instead of throwing when there isn't one.
        /// Use this in OnDisable and OnDestroy. When the game quits or a scene unloads, Unity
        /// destroys objects in no fixed order, so the singleton may already be gone.
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            // Note on "== null": Unity changes == for its objects. A destroyed object that C# still
            // references counts as "== null". So this check also catches an instance that was
            // destroyed but whose reference hasn't been cleared yet.
            if (_instance == null)
            {
                // Only finds ACTIVE objects. Inactive ones haven't run Awake, so they aren't set up yet.
                _instance = FindAnyObjectByType<T>();
            }

            instance = _instance;
            return instance != null;
        }

        /// <summary>
        /// Override and return false for a singleton that should be destroyed with its scene
        /// (for example, a manager for one level). The default, true, keeps it alive across scene loads.
        /// </summary>
        protected virtual bool PersistAcrossScenes => true;

        /// <summary>
        /// Registers this object as the instance, or destroys it if one already exists.
        /// Subclasses that override Awake must call base.Awake() first.
        /// </summary>
        protected virtual void Awake()
        {
            // A duplicate: usually a second copy in a scene, or the scene that holds the persistent
            // singleton was loaded again. Keep the original and remove this one.
            // We destroy the whole GameObject, so don't put other important components on a
            // singleton's GameObject.
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[Singleton] Another {typeof(T).Name} already exists. " +
                                 $"Destroying the duplicate on '{name}'.", this);
                Destroy(gameObject);
                return;
            }

            // The T rule can't catch every mistake. "class AudioManager : Singleton<GameManager>"
            // still compiles, because GameManager is a valid Singleton. In that case this object is
            // not a T, so we stop with a clear error instead of registering the wrong object.
            if (!(this is T self))
            {
                Debug.LogError($"[Singleton] {GetType().Name} inherits Singleton<{typeof(T).Name}>. " +
                               $"It should inherit Singleton<{GetType().Name}>.", this);
                return;
            }

            _instance = self;

            if (PersistAcrossScenes)
            {
                // DontDestroyOnLoad only works on root GameObjects. On a child, Unity just logs a
                // warning and the object is still destroyed when the scene changes. So we detach it first.
                if (transform.parent != null)
                {
                    Debug.LogWarning($"[Singleton] {typeof(T).Name} on '{name}' was a child object. Moving it " +
                                     "to the root so it can persist across scenes. Put it on a root object instead.", this);
                    transform.SetParent(null);
                }

                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// Clears the static reference when the instance is destroyed, so nothing keeps a dead object.
        /// Subclasses that override OnDestroy must call base.OnDestroy().
        /// </summary>
        protected virtual void OnDestroy()
        {
            // Only clear it if WE are the instance. A duplicate being destroyed must not wipe out
            // the real one.
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
