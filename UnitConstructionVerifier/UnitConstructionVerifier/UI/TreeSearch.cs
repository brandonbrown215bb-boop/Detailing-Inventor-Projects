using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace UnitConstructionVerifier.UI
{
    /// <summary>Walks up from a clicked element to find an ancestor of a given type.</summary>
    public static class TreeSearch
    {
        /// <summary>
        /// Visual parents for visuals; logical parents for content elements such as a <c>Run</c> inside a
        /// TextBlock, which <see cref="VisualTreeHelper.GetParent"/> rejects with an exception.
        /// </summary>
        public static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = current is Visual || current is Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
