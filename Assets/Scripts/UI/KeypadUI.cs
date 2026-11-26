using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using AshenWoods.Interaction;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// The keypad screen: a small panel with an amber display of the digits typed so far. Keyboard: 0-9 (top
    /// row or numpad) type, Backspace erases, Enter submits, Esc or E steps away. Gamepad: d-pad up/down picks
    /// the digit under the cursor, A or d-pad right keeps it, X or d-pad left erases, B steps away. The game
    /// keeps running while it is open - the enemy does not wait - only the player's movement is held. A catch
    /// closes it. Builds itself on first use.
    /// </summary>
    public sealed class KeypadUI : MonoBehaviour
    {
        private static readonly Key[] TopRowDigits =
        {
            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private static readonly Key[] NumpadDigits =
        {
            Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
            Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
        };

        private static KeypadUI instance;
        private static int closedFrame = -10;

        /// <summary>True while the keypad is up, and on the frame it closed (Esc must not also pause).</summary>
        public static bool BlocksMenu => (instance != null && instance.keypad != null) || Time.frameCount - closedFrame <= 1;

        private Canvas canvas;
        private Text display;
        private Text status;
        private Text hint;
        private Keypad keypad;
        private string typed = string.Empty;
        private int padDigit;
        private int openedFrame;
        private float closeAt = -1f;
        private PlayerInputReader input;
        private PlayerInteractor interactor;
        private PlayerEnemyTarget target;
        private bool inputWasEnabled;

        public static void Open(Keypad pad)
        {
            if (pad == null || Time.frameCount - closedFrame <= 1)
            {
                return;
            }

            if (instance == null)
            {
                var go = new GameObject("KeypadUI");
                instance = go.AddComponent<KeypadUI>();
                instance.Build();
            }

            instance.Show(pad);
        }

        private void Show(Keypad pad)
        {
            keypad = pad;
            typed = string.Empty;
            padDigit = 0;
            closeAt = -1f;
            openedFrame = Time.frameCount;
            SetStatus(World.Loc.Pick("Введи код.", "Enter the code."), UiTheme.Muted);
            hint.text = Gamepad.current != null
                ? World.Loc.Pick("Крестовина  выбрать цифру     A  ввести     X  стереть     B  отойти", "D-pad  pick a digit     A  enter     X  erase     B  step away")
                : World.Loc.Pick("0-9  ввод     Backspace  стереть     Esc  отойти", "0-9  type     Backspace  erase     Esc  step away");
            Redraw();
            canvas.enabled = true;
            UiSound.Confirm();

            input = FindAnyObjectByType<PlayerInputReader>();
            interactor = FindAnyObjectByType<PlayerInteractor>();
            target = FindAnyObjectByType<PlayerEnemyTarget>();
            if (input != null)
            {
                inputWasEnabled = input.InputEnabled;
                input.SetInputEnabled(false);
            }
        }

        private void Close()
        {
            keypad = null;
            closedFrame = Time.frameCount;
            canvas.enabled = false;

            // Never hand control back after a catch or an escape.
            bool outOfPlay = target != null && target.IsOutOfPlay;
            if (input != null && inputWasEnabled && !outOfPlay)
            {
                input.SetInputEnabled(true);
                PlayerInputReader.ApplyCursorLock(true);
            }
        }

        private void Update()
        {
            if (keypad == null)
            {
                return;
            }

            if (target != null && target.IsOutOfPlay)
            {
                Close();
                return;
            }

            if (closeAt > 0f)
            {
                if (Time.unscaledTime >= closeAt)
                {
                    Close();
                }

                return;
            }

            if (Time.frameCount <= openedFrame)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if ((keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
                || (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                UiSound.Back();
                Close();
                return;
            }

            if (keyboard != null)
            {
                for (int d = 0; d <= 9; d++)
                {
                    // Listed by hand: in the Key enum Digit0 comes AFTER Digit9 and is followed by Shift/Alt/Ctrl/Win,
                    // so "Digit0 + d" typed digits on modifier keys (0.1.2/0.1.3 bug).
                    if (keyboard[TopRowDigits[d]].wasPressedThisFrame || keyboard[NumpadDigits[d]].wasPressedThisFrame)
                    {
                        Type(d);
                    }
                }

                if (keyboard.backspaceKey.wasPressedThisFrame)
                {
                    Erase();
                }
            }

            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame)
                {
                    padDigit = (padDigit + (pad.dpad.up.wasPressedThisFrame ? 1 : 9)) % 10;
                    UiSound.Step(pad.dpad.up.wasPressedThisFrame ? 1 : -1);
                    Redraw();
                }

                if (pad.buttonSouth.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame)
                {
                    Type(padDigit);
                }

                if (pad.buttonWest.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame)
                {
                    Erase();
                }
            }

            bool submit = (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                          || typed.Length == keypad.Digits;
            if (submit && typed.Length == keypad.Digits)
            {
                Submit();
            }
        }

        private void Type(int digit)
        {
            if (typed.Length >= keypad.Digits)
            {
                return;
            }

            typed += (char)('0' + digit);
            padDigit = 0;
            UiSound.Step(1);
            SetStatus(World.Loc.Pick("Введи код.", "Enter the code."), UiTheme.Muted);
            Redraw();
        }

        private void Erase()
        {
            if (typed.Length == 0)
            {
                return;
            }

            typed = typed.Substring(0, typed.Length - 1);
            UiSound.Step(-1);
            Redraw();
        }

        private void Submit()
        {
            if (keypad.IsLockedOut)
            {
                SetStatus(World.Loc.Pick($"Панель заблокирована — ещё {Mathf.CeilToInt(keypad.LockoutRemaining)} с", $"Panel locked — {Mathf.CeilToInt(keypad.LockoutRemaining)} s left"), UiTheme.Danger);
                UiSound.Error();
                return;
            }

            string said = keypad.Submit(typed);
            if (keypad.IsOpen)
            {
                SetStatus(World.Loc.Pick("Принято. Замок открыт.", "Accepted. The lock is open."), UiTheme.Calm);
                display.color = UiTheme.Calm;
                UiSound.Confirm();
                closeAt = Time.unscaledTime + 0.8f;
            }
            else
            {
                SetStatus(World.Loc.Pick("Неверный код — сработала тревога! Уходи, оно слышало.", "Wrong code — the alarm went off! Get away, it heard."), UiTheme.Danger);
                UiSound.Error();
                typed = string.Empty;
                Redraw();
            }

            if (interactor != null && !string.IsNullOrEmpty(said))
            {
                interactor.ShowFeedback(said);
            }
        }

        private void SetStatus(string text, Color color)
        {
            status.text = text;
            status.color = color;
        }

        private void Redraw()
        {
            var text = new System.Text.StringBuilder();
            bool padCursor = Gamepad.current != null;
            for (int i = 0; i < keypad.Digits; i++)
            {
                if (i > 0)
                {
                    text.Append(' ');
                }

                if (i < typed.Length)
                {
                    text.Append(typed[i]);
                }
                else if (i == typed.Length && padCursor)
                {
                    text.Append("<color=#").Append(UiTheme.AccentHex).Append("88>").Append(padDigit).Append("</color>");
                }
                else
                {
                    text.Append("<color=#D9A65744>-</color>");
                }
            }

            display.text = text.ToString();
            display.color = UiTheme.Accent;
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            canvas.enabled = false;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // A soft shadow under the panel instead of a hard edge.
            var glow = new GameObject("Shadow", typeof(RectTransform), typeof(RawImage));
            var glowRect = (RectTransform)glow.transform;
            glowRect.SetParent(transform, false);
            glowRect.sizeDelta = new Vector2(640f + 80f, 330f + 80f);
            glowRect.anchoredPosition = new Vector2(0f, -66f);
            glow.GetComponent<RawImage>().texture = UiTheme.SoftRect(128, 24);
            glow.GetComponent<RawImage>().color = new Color(0f, 0f, 0f, 0.7f);
            glow.GetComponent<RawImage>().raycastTarget = false;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.sizeDelta = new Vector2(640f, 330f);
            rect.anchoredPosition = new Vector2(0f, -60f);
            panel.GetComponent<Image>().color = new Color(0.055f, 0.053f, 0.058f, 0.96f);
            panel.GetComponent<Image>().raycastTarget = false;

            Text title = UiTheme.MakeText("Title", rect, UiTheme.Medium, 18, UiTheme.Muted, TextAnchor.MiddleCenter);
            Place(title, new Vector2(0f, 122f), new Vector2(580f, 30f));
            title.text = World.Loc.Pick("Кодовый замок", "Keypad lock");

            var screen = new GameObject("Screen", typeof(RectTransform), typeof(Image));
            var screenRect = (RectTransform)screen.transform;
            screenRect.SetParent(rect, false);
            screenRect.sizeDelta = new Vector2(320f, 104f);
            screenRect.anchoredPosition = new Vector2(0f, 40f);
            screen.GetComponent<Image>().color = new Color(0.035f, 0.026f, 0.014f, 1f);
            screen.GetComponent<Image>().raycastTarget = false;

            display = UiTheme.MakeText("Digits", screenRect, UiTheme.Mono, 60, UiTheme.Accent, TextAnchor.MiddleCenter);
            display.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(display, new Vector2(0f, -2f), new Vector2(320f, 104f));

            status = UiTheme.MakeText("Status", rect, UiTheme.Regular, 19, UiTheme.Muted, TextAnchor.MiddleCenter);
            Place(status, new Vector2(0f, -52f), new Vector2(580f, 30f));

            hint = UiTheme.MakeText("Hint", rect, UiTheme.Regular, 15, UiTheme.Faint, TextAnchor.MiddleCenter);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(hint, new Vector2(0f, -124f), new Vector2(600f, 26f));
        }

        private static void Place(Text text, Vector2 at, Vector2 size)
        {
            var r = (RectTransform)text.transform;
            r.anchoredPosition = at;
            r.sizeDelta = size;
        }
    }
}
