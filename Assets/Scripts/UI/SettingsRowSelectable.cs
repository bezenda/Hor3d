using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// One settings row as a single focusable line: up/down moves between rows, left/right (arrows, A/D,
    /// d-pad) changes the value, a click on the left or right half of the value steps it down or up.
    /// The "Prev"/"Next" buttons stay as mouse targets; they are taken out of keyboard navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsRowSelectable : Selectable, IPointerClickHandler
    {
        [SerializeField] private string rowName;

        public string RowName
        {
            get => rowName;
            set => rowName = value;
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                Step(eventData.moveDir == MoveDirection.Right ? 1 : -1);
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Clicks on the arrows are handled by the arrow buttons themselves.
            if (eventData.button != PointerEventData.InputButton.Left || eventData.pointerPress != gameObject)
            {
                return;
            }

            Step(1);
        }

        private void Step(int direction)
        {
            SettingsMenu menu = GetComponentInParent<SettingsMenu>();
            if (menu != null && IsInteractable())
            {
                menu.StepRow(rowName, direction);
            }
        }
    }
}
