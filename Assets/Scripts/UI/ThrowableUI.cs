using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// One line above the battery bar: what is in the player's hand and how to throw it, with
    /// a small charge bar while winding up. Hidden while the hand is empty.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowableUI : MonoBehaviour
    {
        [SerializeField] private PlayerThrower thrower;
        [SerializeField] private GameObject root;
        [SerializeField] private Text label;
        [SerializeField] private RectTransform chargeFill;

        private void OnEnable()
        {
            if (thrower == null)
            {
                thrower = FindAnyObjectByType<PlayerThrower>();
            }

            if (thrower != null)
            {
                thrower.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (thrower != null)
            {
                thrower.Changed -= Refresh;
            }
        }

        private void Refresh()
        {
            bool holding = thrower != null && thrower.HeldItem != null;

            if (root != null)
            {
                root.SetActive(holding);
            }

            if (!holding)
            {
                return;
            }

            if (label != null)
            {
                label.text = thrower.IsCharging
                    ? World.Loc.Pick($"Отпусти, чтобы бросить — {Mathf.RoundToInt(thrower.Charge * 100f)}%", $"Release to throw — {Mathf.RoundToInt(thrower.Charge * 100f)}%")
                    : World.Loc.Pick($"{UiTheme.Capitalize(World.Loc.T(thrower.HeldItem.DisplayName))} — зажми <color=#{UiTheme.AccentHex}>ЛКМ</color>, чтобы бросить", $"{UiTheme.Capitalize(World.Loc.T(thrower.HeldItem.DisplayName))} — hold <color=#{UiTheme.AccentHex}>LMB</color> to throw");
            }

            if (chargeFill != null)
            {
                chargeFill.anchorMax = new Vector2(thrower.IsCharging ? thrower.Charge : 0f, 1f);
            }
        }
    }
}
