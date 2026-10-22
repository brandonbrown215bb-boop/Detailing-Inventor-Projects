using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Re-flows the verifier's side-by-side grids into one column for a narrow dock beside the model, and back. Each
    /// grid's original rows, columns and cell positions are remembered the first time it is changed, so switching is
    /// repeatable and <see cref="Restore"/> always returns the layout written in XAML.
    /// </summary>
    public static class ResponsiveLayout
    {
        /// <summary>Views narrower than this (WPF units) use the one-column layout.</summary>
        public const double NarrowBelow = 1040; // the two-column layout was designed for 1080 and clips below ~1030

        private sealed class Cell
        {
            public UIElement Element = null!;
            public int Row, Column, RowSpan, ColumnSpan;
            public Visibility Visibility;
        }

        private sealed class Original
        {
            public Original(Grid grid)
            {
                Columns = grid.ColumnDefinitions.ToArray();
                Rows = grid.RowDefinitions.ToArray();
                Cells = grid.Children.OfType<UIElement>().Select(e => new Cell
                {
                    Element = e,
                    Row = Grid.GetRow(e),
                    Column = Grid.GetColumn(e),
                    RowSpan = Grid.GetRowSpan(e),
                    ColumnSpan = Grid.GetColumnSpan(e),
                    Visibility = e.Visibility,
                }).ToList();
            }

            public ColumnDefinition[] Columns { get; }
            public RowDefinition[] Rows { get; }
            public List<Cell> Cells { get; }
        }

        private static readonly ConditionalWeakTable<Grid, Original> Originals = new ConditionalWeakTable<Grid, Original>();

        /// <summary>Puts a grid back exactly as it was before any fold or stack.</summary>
        public static void Restore(Grid grid)
        {
            if (!Originals.TryGetValue(grid, out Original original)) return;

            grid.ColumnDefinitions.Clear();
            foreach (ColumnDefinition column in original.Columns) grid.ColumnDefinitions.Add(column);
            grid.RowDefinitions.Clear();
            foreach (RowDefinition row in original.Rows) grid.RowDefinitions.Add(row);

            foreach (Cell cell in original.Cells)
            {
                Grid.SetRow(cell.Element, cell.Row);
                Grid.SetColumn(cell.Element, cell.Column);
                Grid.SetRowSpan(cell.Element, cell.RowSpan);
                Grid.SetColumnSpan(cell.Element, cell.ColumnSpan);
                cell.Element.Visibility = cell.Visibility;
            }
        }

        /// <summary>
        /// Moves every cell at or right of column <paramref name="split"/> directly under the cells left of it in the
        /// same row, so a two-half form reads top to bottom in its original order. Right-half cells that
        /// <paramref name="isRepeatedHeader"/> matches (for example a second GAUGE / MATERIAL label) are hidden.
        /// </summary>
        public static void FoldColumns(Grid grid, int split, Func<UIElement, bool>? isRepeatedHeader = null)
        {
            Original original = Originals.GetValue(grid, g => new Original(g));
            Restore(grid);

            int rowCount = Math.Max(1, original.Rows.Length);
            var rightRowUsed = new bool[rowCount];

            foreach (Cell cell in original.Cells)
            {
                int row = Math.Min(cell.Row, rowCount - 1);
                int span = Math.Max(1, cell.RowSpan);
                if (cell.Column >= split)
                {
                    if (isRepeatedHeader?.Invoke(cell.Element) == true)
                    {
                        cell.Element.Visibility = Visibility.Collapsed;
                        continue;
                    }

                    if (cell.Visibility == Visibility.Visible) rightRowUsed[row] = true;
                    Grid.SetRow(cell.Element, row * 2 + 1);
                    Grid.SetColumn(cell.Element, cell.Column - split);
                }
                else
                {
                    Grid.SetRow(cell.Element, row * 2);
                    Grid.SetColumnSpan(cell.Element, Math.Max(1, Math.Min(cell.ColumnSpan, split - cell.Column)));
                }

                Grid.SetRowSpan(cell.Element, span * 2 - 1);
            }

            grid.ColumnDefinitions.Clear();
            foreach (ColumnDefinition column in original.Columns.Take(split))
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width, MinWidth = column.MinWidth, MaxWidth = column.MaxWidth });
            }

            grid.RowDefinitions.Clear();
            for (int r = 0; r < rowCount; r++)
            {
                GridLength height = original.Rows.Length == 0 ? GridLength.Auto : original.Rows[r].Height;
                grid.RowDefinitions.Add(new RowDefinition { Height = height });
                grid.RowDefinitions.Add(new RowDefinition { Height = rightRowUsed[r] ? height : new GridLength(0) });
            }
        }

        /// <summary>
        /// Turns columns into rows: the cell in column <c>i</c> moves to row <c>i</c>, with the given row heights.
        /// Used for "surface list | splitter | detail", which becomes list above detail; a <see cref="GridSplitter"/>
        /// that stretches both ways switches to resizing rows on its own.
        /// </summary>
        public static void StackColumns(Grid grid, params RowDefinition[] rows)
        {
            Original original = Originals.GetValue(grid, g => new Original(g));
            Restore(grid);

            foreach (Cell cell in original.Cells)
            {
                Grid.SetRow(cell.Element, cell.Column);
                Grid.SetColumn(cell.Element, 0);
                Grid.SetRowSpan(cell.Element, Math.Max(1, cell.ColumnSpan));
                Grid.SetColumnSpan(cell.Element, 1);
            }

            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            foreach (RowDefinition row in rows) grid.RowDefinitions.Add(row);
        }
    }
}
