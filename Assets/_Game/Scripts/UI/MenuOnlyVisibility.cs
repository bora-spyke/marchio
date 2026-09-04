using UnityEngine;

namespace Marchio
{
    // Hides this GameObject the moment Play is pressed (or on any other mode), shows it again back on the menu.
    public sealed class MenuOnlyVisibility : MonoBehaviour
    {
        GameManager Gm => GameManager.I;

        void Start()
        {
            Gm.ModeChanged += OnMode;
            OnMode(Gm.Mode);
        }

        void OnDestroy()
        {
            if (GameManager.I != null) GameManager.I.ModeChanged -= OnMode;
        }

        void OnMode(GameMode mode) => gameObject.SetActive(mode == GameMode.Menu);
    }
}
