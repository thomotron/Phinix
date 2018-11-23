using Authentication;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    // TODO: Make this look pretty
    public class CredentialsWindow : Window
    {
        private const float DEFAULT_SPACING = 10f;
        
        private const float TITLE_HEIGHT = 30f;

        private const float SERVER_NAME_HEIGHT = 40f;
        private const float SERVER_DESCRIPTION_HEIGHT = 60f;

        private const float USERNAME_INPUT_HEIGHT = 30f;
        private const float PASSWORD_INPUT_HEIGHT = 30f;
        private const float SUBMIT_BUTTON_HEIGHT = 30f;
        
        public override Vector2 InitialSize => new Vector2(400f, 600f);

        public string SessionId;
        public string ServerName;
        public string ServerDescription;
        public AuthTypes AuthType;
        public ClientAuthenticator.ReturnCredentialsDelegate CredentialsCallback;
        
        private Vector2 serverDescriptionScrollPosition = new Vector2(0f, 0f);
        
        private string usernameText = "";
        private string passwordText = "";
        
        public override void DoWindowContents(Rect inRect)
        {
            doCloseX = true;
            doCloseButton = false;
            doWindowBackground = true;
            
            // Log in title
            Rect titleRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width,
                height: TITLE_HEIGHT
            );
            Widgets.Label(titleRect, "Phinix_login_logInLabel".Translate());

            // Server details (name and description) container
            Rect serverDetailRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin + TITLE_HEIGHT + DEFAULT_SPACING,
                width: inRect.width,
                height: SERVER_NAME_HEIGHT + SERVER_DESCRIPTION_HEIGHT + DEFAULT_SPACING
            );
            DrawServerDetails(serverDetailRect);
            
            // Credential input container
            Rect credentialInputRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin + TITLE_HEIGHT + SERVER_NAME_HEIGHT + SERVER_DESCRIPTION_HEIGHT + (DEFAULT_SPACING * 3),
                width: inRect.width,
                height: USERNAME_INPUT_HEIGHT + PASSWORD_INPUT_HEIGHT + SUBMIT_BUTTON_HEIGHT + (DEFAULT_SPACING * 2)
            );
            DrawUsernamePasswordInput(credentialInputRect);
        }
        
        public override void PostClose()
        {
            base.PostClose();
            
            // Failed credentials collection attempt, only call if the callback isn't null (i.e. we've already responded)
            CredentialsCallback?.Invoke(false, null, 0, null, null);
        }

        private void DrawUsernamePasswordInput(Rect container)
        {
            Rect usernameRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width,
                height: USERNAME_INPUT_HEIGHT
            );
            usernameText = Widgets.TextField(usernameRect, usernameText);
            
            Rect passwordRect = new Rect(
                x: container.xMin,
                y: container.yMin + USERNAME_INPUT_HEIGHT + DEFAULT_SPACING,
                width: container.width,
                height: PASSWORD_INPUT_HEIGHT
            );
            passwordText = Widgets.TextField(passwordRect, passwordText); // TODO: Hide password text
            
            Rect submitButtonRect = new Rect(
                x: container.xMin,
                y: container.yMin + USERNAME_INPUT_HEIGHT + PASSWORD_INPUT_HEIGHT + (DEFAULT_SPACING * 2),
                width: container.width,
                height: SUBMIT_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(submitButtonRect, "Phinix_login_submitButton".Translate()))
            {
                // Return the entered credentials
                CredentialsCallback?.Invoke(true, SessionId, AuthType, usernameText, passwordText);
                
                // Nullify the callback to prevent calling again in PostClose()
                CredentialsCallback = null;
                
                // Close the window
                this.Close();
            }
        }

        private void DrawServerDetails(Rect container)
        {
            Rect serverNameRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width,
                height: SERVER_NAME_HEIGHT
            );
            Widgets.Label(serverNameRect, ServerName);
            
            Rect serverDescriptionRect = new Rect(
                x: container.xMin,
                y: container.yMin + SERVER_NAME_HEIGHT + DEFAULT_SPACING,
                width: container.width,
                height: SERVER_DESCRIPTION_HEIGHT
            );
            Widgets.TextAreaScrollable(serverDescriptionRect, ServerDescription, ref serverDescriptionScrollPosition, true);
        }
    }
}