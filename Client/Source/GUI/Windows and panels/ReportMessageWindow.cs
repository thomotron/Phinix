using System;
using RimWorld;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Pop-up for reporting a message: shows the message, lets the player say why, and saves the report.
    /// </summary>
    /// <remarks>
    /// Opened from the right-click menu on a message in the main chat or in the Inbox. Reports are only saved on this
    /// computer for now (see <see cref="MessageReports"/>); nothing is sent to anyone.
    /// </remarks>
    public class ReportMessageWindow : Window
    {
        private const float SPACING = 10f;
        private const float TITLE_HEIGHT = 35f;
        private const float MESSAGE_BOX_HEIGHT = 110f;
        private const float REASON_BOX_HEIGHT = 80f;
        private const float BUTTON_HEIGHT = 40f;
        private const float BUTTON_WIDTH = 120f;

        public override Vector2 InitialSize => new Vector2(520f, 440f);

        private readonly MessageReports.Report report;
        private string reason = "";
        private Vector2 messageScroll = Vector2.zero;

        /// <param name="report">The message being reported, from <see cref="MessageReports.ForChatMessage"/> or
        /// <see cref="MessageReports.ForDirectMessage"/></param>
        public ReportMessageWindow(MessageReports.Report report)
        {
            this.report = report;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;   // Enter adds a line in the reason box rather than reporting
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, TITLE_HEIGHT), "Phinix_report_title".Translate());
            Text.Font = GameFont.Small;
            y += TITLE_HEIGHT;

            // "From Lumen · 21 Oct 14:02", then the message itself in a box (scrolling if it's long)
            string sentAt = report.SentAt.ToLocalTime().ToString("g");
            UnityEngine.GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f), "Phinix_report_from".Translate(report.SenderName, sentAt));
            UnityEngine.GUI.color = Color.white;
            y += 24f;

            Rect messageRect = new Rect(inRect.x, y, inRect.width, MESSAGE_BOX_HEIGHT);
            Widgets.DrawMenuSection(messageRect);
            Rect messageInner = messageRect.ContractedBy(6f);
            // Shown without its colours and bold, so it reads the same however it was formatted
            string text = TextHelper.StripRichText(report.Text ?? "");
            float textHeight = Text.CalcHeight(text, messageInner.width - 16f);
            Widgets.BeginScrollView(messageInner, ref messageScroll, new Rect(0f, 0f, messageInner.width - 16f, Math.Max(textHeight, messageInner.height)));
            Widgets.Label(new Rect(0f, 0f, messageInner.width - 16f, textHeight), text);
            Widgets.EndScrollView();
            y = messageRect.yMax + SPACING;

            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f), "Phinix_report_reasonLabel".Translate());
            y += 24f;
            reason = Widgets.TextArea(new Rect(inRect.x, y, inRect.width, REASON_BOX_HEIGHT), reason);
            if (reason.Length > MessageReports.MAX_REASON_LENGTH) reason = reason.Substring(0, MessageReports.MAX_REASON_LENGTH);
            y += REASON_BOX_HEIGHT + 4f;

            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "Phinix_report_savedLocallyNote".Translate());
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect reportRect = new Rect(inRect.xMax - BUTTON_WIDTH, inRect.yMax - BUTTON_HEIGHT, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect cancelRect = new Rect(reportRect.x - SPACING - BUTTON_WIDTH, reportRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(cancelRect, "CancelButton".Translate())) Close();
            if (Widgets.ButtonText(reportRect, "Phinix_report_button".Translate())) save();
        }

        /// <summary>
        /// Saves the report with the reason typed, says so, and closes.
        /// </summary>
        private void save()
        {
            report.Reason = reason.Trim();
            if (MessageReports.Save(report))
            {
                Messages.Message("Phinix_report_saved".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
                Close();
            }
            else
            {
                Messages.Message("Phinix_report_saveFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            }
        }
    }
}
