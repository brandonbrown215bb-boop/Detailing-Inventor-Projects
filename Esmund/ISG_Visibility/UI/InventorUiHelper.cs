using System;
using System.Windows.Forms;
using InvApp = Inventor.Application;

namespace VisTog.UI
{
    internal sealed class InventorWindowOwner : IWin32Window
    {
        private readonly IntPtr _handle;

        public InventorWindowOwner(InvApp app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            _handle = new IntPtr(app.MainFrameHWND);
        }

        public IntPtr Handle => _handle;
    }

    internal static class InventorUiHelper
    {
        public static IWin32Window GetOwner(InvApp app)
        {
            try
            {
                if (app != null && app.MainFrameHWND != 0)
                {
                    return new InventorWindowOwner(app);
                }
            }
            catch
            {
            }

            return null;
        }

        public static void ShowMessage(InvApp app, string message, string title, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            MessageBox.Show(GetOwner(app), message, title, MessageBoxButtons.OK, icon);
        }
    }
}
