using Inventor;

namespace SkinChannelPunch.UI
{
    internal static class Ce3RibbonHelper
    {
        public const string TabDisplayName = "Ce3";
        public const string TabInternalName = "TAB_Ce3";
        public const string TabClientId = "{C3A0E000-0001-4000-8000-000000CE3001}";

        public static RibbonTab GetOrCreateTab(Ribbon ribbon)
        {
            try
            {
                return ribbon.RibbonTabs[TabInternalName];
            }
            catch
            {
                return ribbon.RibbonTabs.Add(
                    TabDisplayName,
                    TabInternalName,
                    TabClientId,
                    "id_TabTools",
                    false,
                    false);
            }
        }
    }
}
