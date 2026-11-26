using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// The stamina and flash bars under the battery bar. Stamina fades out while full; the
    /// flash bar fills back up during its cooldown and says how many seconds are left.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StatusBarsUI : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private FlashlightFlash flash;

        [Header("Stamina")]
        [SerializeField] private RectTransform staminaFill;
        [SerializeField] private Image staminaFillImage;
        [SerializeField] private Text staminaLabel;
        [SerializeField] private CanvasGroup staminaGroup;

        [Header("Flash")]
        [SerializeField] private RectTransform flashFill;
        [SerializeField] private Image flashFillImage;
        [SerializeField] private Text flashLabel;

        [Header("Colours")]
        [SerializeField] private Color readyColor = new Color(0.76f, 0.69f, 0.58f, 1f);
        [SerializeField] private Color waitColor = new Color(0.42f, 0.4f, 0.37f, 1f);
        [SerializeField] private Color dangerColor = new Color(0.62f, 0.12f, 0.1f, 1f);
        [SerializeField] private Color labelColor = new Color(0.56f, 0.54f, 0.5f, 1f);
        [SerializeField] private string accentHex = "C2B094";

        private PlayerFlashlight torch;
        private int lastSeconds = -1;
        private int lastState = -1;

        private void OnEnable()
        {
            if (motor == null)
            {
                motor = FindAnyObjectByType<PlayerMotor>();
            }

            if (flash == null)
            {
                flash = FindAnyObjectByType<FlashlightFlash>();
            }

            lastSeconds = -1;
            lastState = -1;
        }

        private void Update()
        {
            UpdateStamina();
            UpdateFlash();
        }

        private void UpdateStamina()
        {
            if (motor == null)
            {
                return;
            }

            float stamina = motor.Stamina;
            if (staminaFill != null)
            {
                staminaFill.anchorMax = new Vector2(stamina, 1f);
            }

            if (staminaFillImage != null)
            {
                staminaFillImage.color = motor.IsExhausted ? dangerColor : readyColor;
            }

            if (staminaLabel != null)
            {
                string text = motor.IsExhausted ? World.Loc.Pick("Нет сил", "Exhausted") : World.Loc.Pick("Выносливость", "Stamina");
                if (staminaLabel.text != text)
                {
                    staminaLabel.text = text;
                    staminaLabel.color = motor.IsExhausted ? dangerColor : labelColor;
                }
            }

            if (staminaGroup != null)
            {
                float target = stamina >= 0.999f ? 0f : 1f;
                staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, target, Time.unscaledDeltaTime * 3f);
            }
        }

        private void UpdateFlash()
        {
            if (flash == null)
            {
                return;
            }

            // The flash is the torch's: no bar until the torch is in the pocket.
            if (torch == null)
            {
                torch = FindAnyObjectByType<PlayerFlashlight>();
            }

            GameObject bar = flashFill != null && flashFill.parent != null ? flashFill.parent.gameObject : null;
            bool owned = torch == null || torch.IsOwned;
            if (bar != null && bar.activeSelf != owned)
            {
                bar.SetActive(owned);
                lastState = -1;
            }

            if (!owned)
            {
                return;
            }

            float remaining = flash.CooldownRemaining;
            float fraction = flash.CooldownSeconds <= 0f ? 1f : 1f - remaining / flash.CooldownSeconds;
            if (flashFill != null)
            {
                flashFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            }

            // 0 = ready, 1 = cooling, 2 = not enough battery
            int state = remaining > 0f ? 1 : flash.IsReady ? 0 : 2;
            int seconds = Mathf.CeilToInt(remaining);
            if (state == lastState && seconds == lastSeconds)
            {
                return;
            }

            lastState = state;
            lastSeconds = seconds;

            if (flashFillImage != null)
            {
                flashFillImage.color = state == 0 ? readyColor : waitColor;
            }

            if (flashLabel != null)
            {
                string cost = Mathf.RoundToInt(flash.BatteryCost * 100f) + "%";
                switch (state)
                {
                    case 0:
                        flashLabel.text = World.Loc.Pick($"Вспышка готова — <color=#{accentHex}>ПКМ</color>", $"Flash ready — <color=#{accentHex}>RMB</color>");
                        break;
                    case 1:
                        flashLabel.text = World.Loc.Pick($"Вспышка через {seconds} с", $"Flash in {seconds} s");
                        break;
                    default:
                        flashLabel.text = World.Loc.Pick($"Для вспышки нужно {cost} заряда", $"The flash needs {cost} charge");
                        break;
                }

                flashLabel.color = labelColor;
            }
        }
    }
}
