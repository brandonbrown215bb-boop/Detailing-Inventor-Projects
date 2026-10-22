using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using NUnit.Framework;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class ResponsiveLayoutTests
    {
        private static TextBlock Cell(Grid grid, string text, int row, int column, int columnSpan = 1)
        {
            var cell = new TextBlock { Text = text, Tag = "header" == text ? "header" : null };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            Grid.SetColumnSpan(cell, columnSpan);
            grid.Children.Add(cell);
            return cell;
        }

        /// <summary>The roof form shape: header row, then two halves of label / gauge / material with a gap column.</summary>
        private static Grid Form()
        {
            var grid = new Grid();
            foreach (var width in new[] { 80.0, 50, 90, 32, 80, 50, 90 })
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            return grid;
        }

        private static (int Row, int Column) At(UIElement e) => (Grid.GetRow(e), Grid.GetColumn(e));

        [Test]
        public void Fold_Puts_Each_Right_Field_Under_Its_Left_Neighbour()
        {
            Grid grid = Form();
            Cell(grid, "header", 0, 1);
            TextBlock rightHeader = Cell(grid, "header", 0, 5);
            TextBlock exterior = Cell(grid, "Exterior", 1, 0);
            TextBlock interior = Cell(grid, "Interior", 1, 4);
            TextBlock interiorGauge = Cell(grid, "18", 1, 5);
            TextBlock thermal = Cell(grid, "Thermal", 2, 0);

            ResponsiveLayout.FoldColumns(grid, 4, e => (e as FrameworkElement)?.Tag as string == "header");

            Assert.AreEqual((2, 0), At(exterior));
            Assert.AreEqual((3, 0), At(interior), "right half goes directly under the left field of the same row");
            Assert.AreEqual((3, 1), At(interiorGauge));
            Assert.AreEqual((4, 0), At(thermal));
            Assert.AreEqual(Visibility.Collapsed, rightHeader.Visibility, "repeated GAUGE / MATERIAL labels are hidden");
            Assert.AreEqual(4, grid.ColumnDefinitions.Count);
            Assert.AreEqual(6, grid.RowDefinitions.Count);
            Assert.AreEqual(0, grid.RowDefinitions[1].Height.Value, "header row has no right half left to show");
            Assert.AreEqual(0, grid.RowDefinitions[5].Height.Value, "a row with nothing on the right takes no space");
            Assert.AreEqual(32, grid.RowDefinitions[3].Height.Value);
        }

        [Test]
        public void Spans_Across_The_Split_Are_Clamped()
        {
            Grid grid = Form();
            TextBlock unit = Cell(grid, "UNIT", 2, 0, columnSpan: 7);

            ResponsiveLayout.FoldColumns(grid, 4);

            Assert.AreEqual(4, Grid.GetColumnSpan(unit));
        }

        [Test]
        public void Restore_Returns_The_Original_Layout_After_Repeated_Folds()
        {
            Grid grid = Form();
            TextBlock rightHeader = Cell(grid, "header", 0, 5);
            TextBlock interior = Cell(grid, "Interior", 1, 4);
            ColumnDefinition firstColumn = grid.ColumnDefinitions[0];

            ResponsiveLayout.FoldColumns(grid, 4, e => e == rightHeader);
            ResponsiveLayout.FoldColumns(grid, 4, e => e == rightHeader);
            ResponsiveLayout.Restore(grid);

            Assert.AreEqual((1, 4), At(interior));
            Assert.AreEqual(Visibility.Visible, rightHeader.Visibility);
            Assert.AreEqual(7, grid.ColumnDefinitions.Count);
            Assert.AreEqual(3, grid.RowDefinitions.Count);
            Assert.AreSame(firstColumn, grid.ColumnDefinitions[0], "original definitions come back, keeping splitter-dragged widths");
        }

        [Test]
        public void Stack_Turns_List_Splitter_Detail_Into_Rows_And_Back()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var list = new DockPanel();
            var splitter = new GridSplitter();
            Grid.SetColumn(splitter, 1);
            var detail = new Grid();
            Grid.SetColumn(detail, 2);
            grid.Children.Add(list);
            grid.Children.Add(splitter);
            grid.Children.Add(detail);

            ResponsiveLayout.StackColumns(grid,
                new RowDefinition { Height = new GridLength(190) },
                new RowDefinition { Height = new GridLength(5) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Assert.AreEqual(new[] { 0, 1, 2 }, new UIElement[] { list, splitter, detail }.Select(Grid.GetRow).ToArray());
            Assert.That(new UIElement[] { list, splitter, detail }.All(e => Grid.GetColumn(e) == 0));
            Assert.AreEqual(0, grid.ColumnDefinitions.Count);
            Assert.AreEqual(3, grid.RowDefinitions.Count);

            ResponsiveLayout.Restore(grid);

            Assert.AreEqual(new[] { 0, 1, 2 }, new UIElement[] { list, splitter, detail }.Select(Grid.GetColumn).ToArray());
            Assert.AreEqual(3, grid.ColumnDefinitions.Count);
            Assert.AreEqual(0, grid.RowDefinitions.Count);
        }
    }
}
