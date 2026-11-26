using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>What a press sounds like.</summary>
    public enum UiPressSound
    {
        Confirm,
        Back,
        Start,
        None
    }

    /// <summary>
    /// How every menu button and settings row responds: the mouse and the keyboard share one focus (hovering
    /// selects), the focused label brightens and steps right while a short amber leader line draws in front
    /// of it, and focus and presses make a sound. Plain text on the ground - no plates - so a column of
    /// options reads like a list, not a stack of boxes. Animates on unscaled time: menus run while the
    /// game is frozen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class UiButtonFeel : MonoBehaviour,
        IPointerEnterHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler, IPointerDownHandler
    {
        [SerializeField] private UiPressSound pressSound = UiPressSound.Confirm;

        [Tooltip("The label that brightens and moves. Defaults to a child named Label.")]
        [SerializeField] private Text label;

        [SerializeField] private Color idleColor = new Color(0.64f, 0.62f, 0.58f, 1f);
        [SerializeField] private Color focusColor = new Color(0.906f, 0.886f, 0.847f, 1f);
        [SerializeField] private Color pressColor = new Color(0.851f, 0.651f, 0.341f, 1f);

        [Tooltip("How far the label steps right when focused, in reference pixels.")]
        [SerializeField] private float shift = 14f;

        [Tooltip("Length of the amber leader line in front of a focused label. 0 hides it.")]
        [SerializeField] private float markerLength = 22f;

        private Selectable selectable;
        private RectTransform labelRect;
        private Vector2 labelMin;
        private Vector2 labelMax;
        private RectTransform marker;
        private Image markerImage;
        private float focus;
        private float press;
        private bool focused;

        public UiPressSound PressSound
        {
            get => pressSound;
            set => pressSound = value;
        }

        /// <summary>Selects a button without the focus tick (a screen choosing its own first option).</summary>
        public static void SelectSilently(GameObject target)
        {
            if (target == null || EventSystem.current == null)
            {
                return;
            }

            UiSound.QuietFocus(0.1f);
            EventSystem.current.SetSelectedGameObject(target);
        }

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (label == null)
            {
                Transform child = transform.Find("Label");
                label = child != null ? child.GetComponent<Text>() : null;
            }

            if (label != null)
            {
                labelRect = label.rectTransform;
                labelMin = labelRect.offsetMin;
                labelMax = labelRect.offsetMax;
                label.color = idleColor;
            }

            if (markerLength > 0f)
            {
                Transform existing = transform.Find("Marker");
                GameObject go = existing != null ? existing.gameObject : new GameObject("Marker", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                marker = (RectTransform)go.transform;
                marker.anchorMin = marker.anchorMax = new Vector2(0f, 0.5f);
                // Ends just before where the focused label starts and draws leftwards from there.
                marker.pivot = new Vector2(1f, 0.5f);
                marker.anchoredPosition = new Vector2((labelRect != null ? labelMin.x : 0f) + shift - 8f, 0f);
                marker.sizeDelta = new Vector2(0f, 2f);
                markerImage = go.GetComponent<Image>();
                markerImage.raycastTarget = false;
                markerImage.color = new Color(pressColor.r, pressColor.g, pressColor.b, 0f);
            }
        }

        private void OnDisable()
        {
            focused = false;
            focus = 0f;
            press = 0f;
            Apply();
        }

        private void Update()
        {
            bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            focused = selected && selectable.IsInteractable();
            float dt = Time.unscaledDeltaTime;
            // Exponential ease-out towards the target: quick to answer, soft to settle.
            focus = Mathf.Lerp(focus, focused ? 1f : 0f, 1f - Mathf.Exp(-dt * 16f));
            press = Mathf.MoveTowards(press, 0f, dt * 3.5f);
            Apply();
        }

        private void Apply()
        {
            if (labelRect != null)
            {
                Vector2 offset = new Vector2(shift * focus, 0f);
                labelRect.offsetMin = labelMin + offset;
                labelRect.offsetMax = labelMax + offset;
                Color color = Color.Lerp(idleColor, focusColor, focus);
                if (selectable != null && !selectable.IsInteractable())
                {
                    color = new Color(idleColor.r, idleColor.g, idleColor.b, 0.35f);
                }

                label.color = Color.Lerp(color, pressColor, press);
            }

            if (marker != null)
            {
                marker.sizeDelta = new Vector2(markerLength * focus, 2f);
                markerImage.color = new Color(pressColor.r, pressColor.g, pressColor.b, focus);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable != null && selectable.IsInteractable() && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject != gameObject)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (selectable != null && selectable.IsInteractable())
            {
                UiSound.Focus();
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && selectable.IsInteractable())
            {
                press = 1f;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Pressed();
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            press = 1f;
            Pressed();
        }

        private void Pressed()
        {
            if (selectable == null || !selectable.IsInteractable())
            {
                return;
            }

            switch (pressSound)
            {
                case UiPressSound.Confirm:
                    UiSound.Confirm();
                    break;
                case UiPressSound.Back:
                    UiSound.Back();
                    break;
                case UiPressSound.Start:
                    UiSound.Start();
                    break;
            }
        }
    }
}
