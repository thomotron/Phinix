using System.Collections.Generic;
using System.Linq;
using PhinixClient.GUI;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// A player's card: their name, whether they're on (or when they were last seen), their bio, their colony, and how their mods
    /// compare to ours, with buttons for the usual things to do with them.
    /// </summary>
    /// <remarks>
    /// Opened with "View profile" on the right-click menu for a name in the user list, chat or Inbox. Everything is read
    /// fresh each frame, so it keeps up if they log in or out, or change their bio, while it's open.
    /// </remarks>
    public class PlayerCardWindow : Window
    {
        private const float SPACING = 10f;
        private const float TITLE_HEIGHT = 35f;
        private const float BIO_BOX_HEIGHT = 62f;
        private const float COLONY_BOX_HEIGHT = 52f;
        private const float BUTTON_HEIGHT = 32f;
        private const float BUTTON_GAP = 6f;

        public override Vector2 InitialSize => new Vector2(480f, 540f);

        private readonly string uuid;
        private Vector2 modsScroll = Vector2.zero;

        /// <param name="uuid">The player's UUID</param>
        public PlayerCardWindow(string uuid)
        {
            this.uuid = uuid;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            bool known = Client.Instance.TryGetUser(uuid, out ImmutableUser user);
            string name = known ? user.DisplayName : "???";
            float y = inRect.y;

            // Name, as they've formatted it if the player shows formatting. Long names wrap onto more lines (up to
            // three) rather than being cut off, and the mods box below gives up the room
            string shownName = Client.ShownName(name);
            Text.Font = GameFont.Medium;
            float titleWidth = inRect.width - 30f;
            float titleHeight = Mathf.Clamp(Text.CalcHeight(shownName, titleWidth), TITLE_HEIGHT, Text.LineHeight * 3f);
            Widgets.Label(new Rect(inRect.x, y, titleWidth, titleHeight), shownName);
            Text.Font = GameFont.Small;
            y += titleHeight + 4f;

            // "Online · Friend", "Last seen 3h ago", ...
            List<string> status = new List<string>();
            if (known && user.LoggedIn) status.Add("Phinix_card_online".Translate());
            else if (Client.Instance.TryGetLastSeen(uuid, out long lastSeen)) status.Add(Client.FormatLastSeen(lastSeen));
            else status.Add("Phinix_card_offline".Translate());
            if (Client.Instance.IsFriend(uuid)) status.Add("Phinix_card_friend".Translate());
            if (Client.Instance.IsBlocked(uuid)) status.Add("Phinix_card_blocked".Translate());
            PhimeStyle.Label(new Rect(inRect.x, y, inRect.width, 22f), string.Join(" · ", status), PhimeStyle.Muted);
            y += 22f + SPACING;

            // Bio
            PhimeStyle.Heading(new Rect(inRect.x, y, inRect.width, 18f), "Phinix_card_bioHeading".Translate());
            y += 20f;
            Rect bioRect = new Rect(inRect.x, y, inRect.width, BIO_BOX_HEIGHT);
            Widgets.DrawMenuSection(bioRect);
            if (Client.Instance.TryGetBio(uuid, out string bio)) Widgets.Label(bioRect.ContractedBy(6f), bio);
            else PhimeStyle.Label(bioRect.ContractedBy(6f), "Phinix_card_noBio".Translate(), PhimeStyle.Dim);
            y = bioRect.yMax + SPACING;

            // Colony. Our own card has the switch for sharing it
            bool own = uuid == Client.Instance.Uuid;
            Rect colonyHeadingRect = new Rect(inRect.x, y, inRect.width, 18f);
            PhimeStyle.Heading(colonyHeadingRect, "Phinix_card_colonyHeading".Translate());
            if (own)
            {
                bool share = Client.Instance.Settings.ShareColonyStats;
                Text.Font = GameFont.Tiny;
                string shareLabel = "Phinix_card_colonyShare".Translate();
                float shareWidth = Text.CalcSize(shareLabel).x + 30f;
                Widgets.CheckboxLabeled(new Rect(colonyHeadingRect.xMax - shareWidth, y - 2f, shareWidth, 22f), shareLabel, ref share);
                Text.Font = GameFont.Small;
                Client.Instance.SetShareColonyStats(share);
            }
            y += 20f;
            Rect colonyRect = new Rect(inRect.x, y, inRect.width, COLONY_BOX_HEIGHT);
            Widgets.DrawMenuSection(colonyRect);
            if (Client.Instance.TryGetColony(uuid, out ColonyStats colony)) Widgets.Label(colonyRect.ContractedBy(6f), colonyText(colony));
            else PhimeStyle.Label(colonyRect.ContractedBy(6f), "Phinix_card_noColony".Translate(), PhimeStyle.Dim);
            y = colonyRect.yMax + SPACING;

            // Mods, filling the space down to the buttons
            bool self = uuid == Client.Instance.Uuid;
            float buttonsTop = self ? inRect.yMax : inRect.yMax - BUTTON_HEIGHT;
            PhimeStyle.Heading(new Rect(inRect.x, y, inRect.width, 18f), "Phinix_card_modsHeading".Translate());
            y += 20f;
            Rect modsRect = new Rect(inRect.x, y, inRect.width, buttonsTop - SPACING - y);
            Widgets.DrawMenuSection(modsRect);
            drawMods(modsRect.ContractedBy(6f), self);

            if (!self) drawButtons(new Rect(inRect.x, inRect.yMax - BUTTON_HEIGHT, inRect.width, BUTTON_HEIGHT), known && user.LoggedIn);
        }

        /// <summary>
        /// "New Hope · Temperate forest" over "Day 84 · 6 colonists · 11 animals · $92,400".
        /// </summary>
        private static string colonyText(ColonyStats colony)
        {
            string where = string.IsNullOrEmpty(colony.Biome) ? colony.Name : colony.Name + " · " + colony.Biome.CapitalizeFirst();
            List<string> facts = new List<string>
            {
                "Phinix_card_colonyDay".Translate(colony.Days + 1),
                (colony.Colonists == 1 ? "Phinix_card_colonyColonist" : "Phinix_card_colonyColonists").Translate(colony.Colonists),
                (colony.Animals == 1 ? "Phinix_card_colonyAnimal" : "Phinix_card_colonyAnimals").Translate(colony.Animals),
                "Phinix_card_colonyWealth".Translate(colony.Wealth.Money())
            };
            return where + "\n" + string.Join(" · ", facts).Colorize(PhimeStyle.Muted);
        }

        /// <summary>
        /// How their mods compare to ours: how many they have, and which ones each of us is missing.
        /// </summary>
        private void drawMods(Rect inRect, bool self)
        {
            HashSet<string> own = new HashSet<string>(LoadedModManager.RunningMods.Where(mod => mod.PackageId != null).Select(mod => mod.PackageId.ToLowerInvariant()));
            string text;
            if (self) text = "Phinix_card_modsOwn".Translate(own.Count);
            else if (!Client.Instance.TryGetModList(uuid, out HashSet<string> theirs)) text = "Phinix_card_modsUnknown".Translate();
            else
            {
                List<string> weLack = theirs.Where(id => !own.Contains(id)).Select(id => Client.Instance.ModName(id)).OrderBy(n => n).ToList();
                List<string> theyLack = LoadedModManager.RunningMods
                    .Where(mod => mod.PackageId != null && !theirs.Contains(mod.PackageId.ToLowerInvariant()))
                    .Select(mod => mod.Name).OrderBy(n => n).ToList();

                text = "Phinix_card_modsCount".Translate(theirs.Count);
                if (weLack.Count == 0 && theyLack.Count == 0) text += "\n\n" + "Phinix_card_modsSame".Translate();
                if (weLack.Count > 0) text += "\n\n" + "Phinix_card_modsWeLack".Translate(weLack.Count).Colorize(PhimeStyle.Warning) + "\n" + string.Join(", ", weLack);
                if (theyLack.Count > 0) text += "\n\n" + "Phinix_card_modsTheyLack".Translate(theyLack.Count).Colorize(PhimeStyle.Them) + "\n" + string.Join(", ", theyLack);
            }

            float width = inRect.width - 16f;
            float height = Mathf.Max(Text.CalcHeight(text, width), inRect.height);
            Widgets.BeginScrollView(inRect, ref modsScroll, new Rect(0f, 0f, width, height));
            Widgets.Label(new Rect(0f, 0f, width, height), text);
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Trade, gift, friend and block buttons, the same things the right-click menu offers.
        /// </summary>
        private void drawButtons(Rect inRect, bool online)
        {
            bool blocked = Client.Instance.IsBlocked(uuid);
            List<(string Label, System.Action Action)> buttons = new List<(string, System.Action)>();
            if (!blocked && online) buttons.Add(("Phinix_card_trade".Translate(), () => { Client.Instance.CreateTrade(uuid); Close(); }));
            if (!blocked && Client.Instance.GiftsAvailable) buttons.Add(("Phinix_card_gift".Translate(), () => Find.WindowStack.Add(new SendGiftWindow(uuid))));
            if (!blocked && Client.Instance.FriendsEnabled)
            {
                bool friend = Client.Instance.IsFriend(uuid);
                buttons.Add(((friend ? "Phinix_chat_contextMenu_removeFriend" : "Phinix_chat_contextMenu_addFriend").Translate(), () =>
                {
                    if (friend) Client.Instance.RemoveFriend(uuid);
                    else Client.Instance.AddFriend(uuid);
                }));
            }
            buttons.Add(((blocked ? "Phinix_chat_contextMenu_unblockUser" : "Phinix_chat_contextMenu_blockUser").Translate(), () =>
            {
                if (blocked) Client.Instance.UnBlockUser(uuid);
                else Client.Instance.BlockUser(uuid);
            }));

            float width = (inRect.width - BUTTON_GAP * (buttons.Count - 1)) / buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
            {
                Rect buttonRect = new Rect(inRect.x + i * (width + BUTTON_GAP), inRect.y, width, inRect.height);
                if (PhimeStyle.Button(buttonRect, buttons[i].Label)) buttons[i].Action();
            }
        }
    }
}
