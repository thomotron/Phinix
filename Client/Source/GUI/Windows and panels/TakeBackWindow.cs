using System;
using System.Text.RegularExpressions;
using PhinixClient.GUI;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Asks how many of a row to take back off our offer (or ask them to take out of theirs), showing how many
    /// would be left on it.
    /// </summary>
    public class TakeBackWindow : Window
    {
        private static readonly Regex digitsOnly = new Regex("\\d*");

        public override Vector2 InitialSize => new Vector2(360f, 190f);

        private readonly string label;
        private readonly int max;
        private readonly Action<int> confirm;
        private readonly string titleKey;
        private readonly string buttonKey;
        private readonly string leavesKey;
        private int amount = 1;
        private string buffer = "1";
        private bool focused;

        public TakeBackWindow(string label, int max, Action<int> confirm, bool theirs = false)
        {
            titleKey = theirs ? "Phinix_trade_askAmountTitle" : "Phinix_trade_takeBackAmountTitle";
            buttonKey = theirs ? "Phinix_trade_askSome" : "Phinix_trade_takeBackSome";
            leavesKey = theirs ? "Phinix_trade_askLeaves" : "Phinix_trade_takeBackLeaves";

            this.label = label;
            this.max = max;
            this.confirm = confirm;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            draggable = true;
            preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Enter takes back, like pressing the button
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (enter) Event.current.Use();

            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, titleKey.Translate());
            Text.Font = GameFont.Small;

            Rect nameRect = new Rect(inRect.x, titleRect.yMax + 4f, inRect.width, 24f);
            Widgets.Label(nameRect, label.Truncate(nameRect.width));

            // Amount field, with the slider beside it for quick picks
            Rect fieldRect = new Rect(inRect.x, nameRect.yMax + 6f, 80f, 30f);
            UnityEngine.GUI.SetNextControlName("PhinixTakeBackAmount");
            string newBuffer = Widgets.TextField(fieldRect, buffer, 9, digitsOnly);
            if (!focused)
            {
                UnityEngine.GUI.FocusControl("PhinixTakeBackAmount");
                focused = true;
            }
            if (newBuffer != buffer)
            {
                buffer = newBuffer;
                amount = string.IsNullOrEmpty(buffer) ? 0 : Mathf.Clamp(int.TryParse(buffer, out int typed) ? typed : max, 0, max);
            }

            Rect sliderRect = new Rect(fieldRect.xMax + 10f, fieldRect.y + 6f, inRect.width - fieldRect.width - 10f, 24f);
            int slid = Mathf.RoundToInt(Widgets.HorizontalSlider(sliderRect, amount, 1f, max, roundTo: 1f));
            // Only take the slider's value while it's being dragged, so a cleared field isn't filled back in
            if (slid != amount && Input.GetMouseButton(0))
            {
                amount = slid;
                buffer = amount.ToString();
            }

            // What's left on the offer afterwards
            Rect leftRect = new Rect(inRect.x, fieldRect.yMax + 4f, inRect.width, 24f);
            UnityEngine.GUI.color = PhimeStyle.Muted;
            Widgets.Label(leftRect, leavesKey.Translate(max - amount, max));
            UnityEngine.GUI.color = Color.white;

            Rect buttonRect = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
            bool valid = amount >= 1 && amount <= max;
            if (!valid) UnityEngine.GUI.color = Color.gray;
            if ((Widgets.ButtonText(buttonRect, buttonKey.Translate(amount)) || enter) && valid)
            {
                confirm(amount);
                Close();
            }
            UnityEngine.GUI.color = Color.white;
        }
    }
}
