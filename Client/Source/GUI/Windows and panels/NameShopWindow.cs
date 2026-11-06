using Agora;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Small pop-up for naming our market: opening it for the first time, or renaming it.
    /// </summary>
    public class NameShopWindow : Window
    {
        private const float SPACING = 10f;
        private const float ROW_HEIGHT = 30f;
        private const float BUTTON_WIDTH = 120f;

        public override Vector2 InitialSize => new Vector2(420f, 170f);

        private string name;

        /// <summary>
        /// Whether we're opening a new market rather than renaming one, which changes the title.
        /// </summary>
        private readonly bool opening;

        /// <param name="currentName">The market's name now, or null when opening a new one</param>
        public NameShopWindow(string currentName = null)
        {
            opening = currentName == null;
            name = currentName ?? "Phinix_shop_defaultName".Translate(TextHelper.StripRichText(Client.Instance.Settings.DisplayName));

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), (opening ? "Phinix_shop_nameTitle" : "Phinix_shop_renameTitle").Translate());
            Text.Font = GameFont.Small;

            name = Widgets.TextField(new Rect(inRect.x, inRect.y + 35f + SPACING, inRect.width, ROW_HEIGHT), name);
            if (name.Length > ServerAgora.MAX_SHOP_NAME_LENGTH) name = name.Substring(0, ServerAgora.MAX_SHOP_NAME_LENGTH);

            bool valid = !string.IsNullOrWhiteSpace(name);
            if (Widgets.ButtonText(inRect.BottomPartPixels(ROW_HEIGHT).RightPartPixels(BUTTON_WIDTH), (opening ? "Phinix_shop_openButton" : "Phinix_shop_renameButton").Translate(), active: valid) && valid)
            {
                OnAcceptKeyPressed();
            }
        }

        /// <summary>
        /// Enter (or the button) sends the name to the server and closes the window.
        /// </summary>
        public override void OnAcceptKeyPressed()
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            Client.Instance.OpenShop(name.Trim());
            Close();
        }
    }
}
