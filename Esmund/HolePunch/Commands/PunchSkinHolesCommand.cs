using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;
using SkinChannelPunch.Core;
using SkinChannelPunch.UI;

namespace SkinChannelPunch.Commands
{
    public sealed class PunchSkinHolesCommand : CommandBase
    {
        public PunchSkinHolesCommand(InvApp app, ButtonDefinition buttonDefinition)
            : base(app, buttonDefinition)
        {
        }

        protected override void Execute(NameValueMap context)
        {
            if (!TryGetActiveAssemblyDocument(out AssemblyDocument assemblyDocument))
            {
                MessageBox.Show("Open an assembly before running Punch Skin Holes.", "Hole Punch");
                return;
            }

            double bottomInsetIn;
            double topInsetIn;
            double maxSpacingIn;
            double holeDiameterIn;

            using (var dialog = new PunchSkinHolesDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                bottomInsetIn = dialog.BottomInsetIn;
                topInsetIn = dialog.TopInsetIn;
                maxSpacingIn = dialog.MaxSpacingIn;
                holeDiameterIn = dialog.HoleDiameterIn;
            }

            var service = new SkinHolePunchService(App);

            // Settings dialog once; then channel → skin → confirm → punch, repeat until pick cancel.
            while (true)
            {
                ComponentOccurrence channelOcc = InteractionHelper.PickOccurrence(
                    App,
                    assemblyDocument,
                    "Select a vertical channel");
                if (channelOcc == null)
                {
                    return;
                }

                if (!InteractionHelper.TryPickSkinFace(
                    App,
                    assemblyDocument,
                    "Select a skin",
                    out Face skinFace,
                    out ComponentOccurrence skinOcc)
                    || skinFace == null
                    || skinOcc == null)
                {
                    // Esc on skin pick ends the session (same as canceling channel pick).
                    return;
                }

                Document skinDoc = IsgMomDataHelper.GetOccurrenceDocument(skinOcc);
                if (IsgMomDataHelper.IsDocumentStatusNoChange(skinDoc))
                {
                    DialogResult isgConfirm = MessageBox.Show(
                        "Punching an unconverted part is ill advised."
                        + System.Environment.NewLine
                        + System.Environment.NewLine
                        + "Convert to a Library Part first."
                        + System.Environment.NewLine
                        + System.Environment.NewLine
                        + "Continue punching anyway?",
                        "Hole Punch",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning);
                    if (isgConfirm != DialogResult.OK)
                    {
                        continue;
                    }
                }
                else
                {
                    DialogResult confirm = MessageBox.Show(
                        "Apply punch?",
                        "Hole Punch",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question);
                    if (confirm != DialogResult.OK)
                    {
                        // Skip this pair; offer another channel pick.
                        continue;
                    }
                }

                SkinHolePunchResult result = service.PunchVerticalPattern(
                    assemblyDocument,
                    channelOcc,
                    skinOcc,
                    skinFace,
                    bottomInsetIn,
                    topInsetIn,
                    maxSpacingIn,
                    holeDiameterIn);

                // Success: no popup — immediately pick the next channel.
                // Failure: brief notice so the session can continue or be canceled on next Esc.
                if (result.Created <= 0)
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(result.Message) ? "Punch failed." : result.Message,
                        "Hole Punch",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }
    }
}
