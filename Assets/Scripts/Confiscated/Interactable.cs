using UnityEngine;

namespace Confiscated
{
    /// <summary>Something the player can use by looking at it and pressing Interact.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("Seconds the Interact button must be held. 0 = instant.")]
        public float holdSeconds = 0f;

        /// <summary>Prompt shown when the player looks at this. Return null/empty to hide.</summary>
        public abstract string GetPrompt(PlayerInteractor player);

        public virtual bool CanInteract(PlayerInteractor player) => true;

        /// <summary>Called once when the interaction completes (instant, or after the hold finishes).</summary>
        public abstract void Interact(PlayerInteractor player);
    }
}
