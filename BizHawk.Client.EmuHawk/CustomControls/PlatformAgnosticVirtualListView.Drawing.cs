using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// A performant VirtualListView implementation that doesn't rely on native Win32 API calls
	/// (and in fact does not inherit the ListView class at all)
	/// It is a simplified version of the work done with GDI+ rendering in InputRoll.cs
	/// ------------------------------
	/// *** GDI+ Rendering Methods ***
	/// ------------------------------
	/// </summary>
	public partial class PlatformAgnosticVirtualListView
	{
		// single instance to mirror GDI implementation
		private Pen sPen = null;
		// single instance to mirror GDI implementation
		private Brush sBrush = null;

		protected override void OnPaint(PaintEventArgs e)
		{
			// white background
			sBrush = new SolidBrush(Color.White);
			sPen = new Pen(Color.White);

			Rectangle rect = e.ClipRectangle;
			e.Graphics.FillRectangle(sBrush, rect);
			e.Graphics.Flush();

			// Lag frame calculations
			//SetLagFramesArray();

			var visibleColumns = _columns.VisibleColumns.ToList();

			if (visibleColumns.Any())
			{
				DrawColumnBg(e, visibleColumns);
				DrawColumnText(e, visibleColumns);
			}

			// Background
			DrawBg(e, visibleColumns);

			// Foreground
			DrawData(e, visibleColumns);

			DrawColumnDrag(e);
			DrawCellDrag(e);
		}

		private void DrawColumnDrag(PaintEventArgs e)
		{
			if (_draggingCell != null)
			{
				var text = "";
				int offsetX = 0;
				int offsetY = 0;
				QueryItemText?.Invoke(_draggingCell.RowIndex.Value, _draggingCell.Column, out text, ref offsetX, ref offsetY);

				Color bgColor = _backColor;
				QueryItemBkColor?.Invoke(_draggingCell.RowIndex.Value, _draggingCell.Column, ref bgColor);

				int x1 = _currentX.Value - (_draggingCell.Column.Width.Value / 2);
				int y1 = _currentY.Value - (CellHeight / 2);
				int x2 = x1 + _draggingCell.Column.Width.Value;
				int y2 = y1 + CellHeight;

				sBrush = new SolidBrush(bgColor);
				e.Graphics.FillRectangle(sBrush, x1, y1, x2 - x1, y2 - y1);
				sBrush = new SolidBrush(_foreColor);
				e.Graphics.DrawString(text, _commonFont, sBrush, (PointF)(new Point(x1 + CellWidthPadding + offsetX, y1 + CellHeightPadding + offsetY)));
			}
		}

		private void DrawCellDrag(PaintEventArgs e)
		{
			if (_draggingCell != null)
			{
				var text = "";
				int offsetX = 0;
				int offsetY = 0;
				QueryItemText?.Invoke(_draggingCell.RowIndex.Value, _draggingCell.Column, out text, ref offsetX, ref offsetY);

				Color bgColor = _backColor;
				QueryItemBkColor?.Invoke(_draggingCell.RowIndex.Value, _draggingCell.Column, ref bgColor);

				int x1 = _currentX.Value - (_draggingCell.Column.Width.Value / 2);
				int y1 = _currentY.Value - (CellHeight / 2);
				int x2 = x1 + _draggingCell.Column.Width.Value;
				int y2 = y1 + CellHeight;

				sBrush = new SolidBrush(bgColor);
				e.Graphics.FillRectangle(sBrush, x1, y1, x2 - x1, y2 - y1);
				sBrush = new SolidBrush(_foreColor);
				e.Graphics.DrawString(text, _commonFont, sBrush, (PointF)(new Point(x1 + CellWidthPadding + offsetX, y1 + CellHeightPadding + offsetY)));
			}
		}

		private void DrawColumnText(PaintEventArgs e, List<ListColumn> visibleColumns)
		{
			sBrush = new SolidBrush(_foreColor);

			foreach (var column in visibleColumns)
			{
				var point = new Point(column.Left.Value + 2 * CellWidthPadding - _hBar.Value, CellHeightPadding); // TODO: fix this CellPadding issue (2 * CellPadding vs just CellPadding)

				if (IsHoveringOnColumnCell && column == CurrentCell.Column)
				{
					sBrush = new SolidBrush(SystemColors.HighlightText);
					e.Graphics.DrawString(column.Text, _commonFont, sBrush, (PointF)(point));
					sBrush = new SolidBrush(_foreColor);
				}
				else
				{
					e.Graphics.DrawString(column.Text, _commonFont, sBrush, (PointF)(point));
				}
			}
		}

		private void DrawData(PaintEventArgs e, List<ListColumn> visibleColumns)
		{
			// Prevent exceptions with small TAStudio windows
			if (visibleColumns.Count == 0)
			{
				return;
			}

			//bool isRotated = false;

			if (QueryItemText != null)
			{
				int startRow = FirstVisibleRow;
				int range = Math.Min(LastVisibleRow, RowCount - 1) - startRow + 1;

				sBrush = new SolidBrush(_foreColor);

				int xPadding = CellWidthPadding + 1 - _hBar.Value;
				for (int i = 0, f = 0; f < range; i++, f++) // Vertical
				{
					//f += _lagFrames[i];
					int LastVisible = LastVisibleColumnIndex;
					for (int j = FirstVisibleColumn; j <= LastVisible; j++) // Horizontal
					{
						ListColumn col = visibleColumns[j];

						string text;
						int strOffsetX = 0;
						int strOffsetY = 0;
						Point point = new Point(col.Left.Value + xPadding, RowsToPixels(i) + CellHeightPadding);

						Bitmap image = null;
						int bitmapOffsetX = 0;
						int bitmapOffsetY = 0;

						QueryItemIcon?.Invoke(f + startRow, visibleColumns[j], ref image, ref bitmapOffsetX, ref bitmapOffsetY);

						if (image != null)
						{
							e.Graphics.DrawImage(image, new Point(point.X + bitmapOffsetX, point.Y + bitmapOffsetY + CellHeightPadding));
						}

						QueryItemText(f + startRow, visibleColumns[j], out text, ref strOffsetX, ref strOffsetY);

						bool rePrep = false;
						if (_selectedItems.Contains(new Cell { Column = visibleColumns[j], RowIndex = f + startRow }))
						{
							sBrush = new SolidBrush(SystemColors.HighlightText);
							rePrep = true;
						}

						if (!string.IsNullOrWhiteSpace(text))
						{
							e.Graphics.DrawString(text, _commonFont, sBrush, (PointF)(new Point(point.X + strOffsetX, point.Y + strOffsetY)));
						}

						if (rePrep)
						{
							sBrush = new SolidBrush(_foreColor);
						}
					}
				}
			}
		}

		private void DrawColumnBg(PaintEventArgs e, List<ListColumn> visibleColumns)
		{
			sBrush = new SolidBrush(SystemColors.ControlLight);
			sPen = new Pen(Color.Black);

			int bottomEdge = RowsToPixels(0);

			// Gray column box and black line underneath
			e.Graphics.FillRectangle(sBrush, 0, 0, Width + 1, bottomEdge + 1);
			e.Graphics.DrawLine(sPen, 0, 0, TotalColWidth.Value + 1, 0);
			e.Graphics.DrawLine(sPen, 0, bottomEdge, TotalColWidth.Value + 1, bottomEdge);

			// Vertical black seperators
			for (int i = 0; i < visibleColumns.Count; i++)
			{
				int pos = visibleColumns[i].Left.Value - _hBar.Value;
				e.Graphics.DrawLine(sPen, pos, 0, pos, bottomEdge);
			}

			// Draw right most line
			if (visibleColumns.Any())
			{
				int right = TotalColWidth.Value - _hBar.Value;
				e.Graphics.DrawLine(sPen, right, 0, right, bottomEdge);
			}

			// Emphasis
			foreach (var column in visibleColumns.Where(c => c.Emphasis))
			{
				sBrush = new SolidBrush(SystemColors.ActiveBorder);
				e.Graphics.FillRectangle(sBrush, column.Left.Value + 1 - _hBar.Value, 1, column.Width.Value - 1, ColumnHeight - 1);
			}

			// If the user is hovering over a column
			if (IsHoveringOnColumnCell)
			{
				// TODO multiple selected columns
				for (int i = 0; i < visibleColumns.Count; i++)
				{
					if (visibleColumns[i] == CurrentCell.Column)
					{
						// Left of column is to the right of the viewable area or right of column is to the left of the viewable area
						if (visibleColumns[i].Left.Value - _hBar.Value > Width || visibleColumns[i].Right.Value - _hBar.Value < 0)
						{
							continue;
						}

						int left = visibleColumns[i].Left.Value - _hBar.Value;
						int width = visibleColumns[i].Right.Value - _hBar.Value - left;

						if (CurrentCell.Column.Emphasis)
						{
							sBrush = new SolidBrush(Color.FromArgb(SystemColors.Highlight.ToArgb() + 0x00550000));
						}
						else
						{
							sBrush = new SolidBrush(SystemColors.Highlight);
						}

						e.Graphics.FillRectangle(sBrush, left + 1, 1, width - 1, ColumnHeight - 1);
					}
				}
			}
		}

		// TODO refactor this and DoBackGroundCallback functions.
		/// <summary>
		/// Draw Gridlines and background colors using QueryItemBkColor.
		/// </summary>
		private void DrawBg(PaintEventArgs e, List<ListColumn> visibleColumns)
		{
			if (UseCustomBackground && QueryItemBkColor != null)
			{
				DoBackGroundCallback(e, visibleColumns);
			}

			if (GridLines)
			{
				sPen = new Pen(SystemColors.ControlLight);

				// Columns
				int y = ColumnHeight + 1;
				int? totalColWidth = TotalColWidth;
				foreach (var column in visibleColumns)
				{
					int x = column.Left.Value - _hBar.Value;
					e.Graphics.DrawLine(sPen, x, y, x, Height - 1);
				}

				if (visibleColumns.Any())
				{
					e.Graphics.DrawLine(sPen, totalColWidth.Value - _hBar.Value, y, totalColWidth.Value - _hBar.Value, Height - 1);
				}

				// Rows
				for (int i = 1; i < VisibleRows + 1; i++)
				{
					e.Graphics.DrawLine(sPen, 0, RowsToPixels(i), Width + 1, RowsToPixels(i));
				}
			}

			if (_selectedItems.Any())
			{
				DoSelectionBG(e, visibleColumns);
			}
		}

		/// <summary>
		/// Given a cell with rowindex inbetween 0 and VisibleRows, it draws the background color specified. Do not call with absolute rowindices.
		/// </summary>
		private void DrawCellBG(PaintEventArgs e, Color color, Cell cell, List<ListColumn> visibleColumns)
		{
			int x, y, w, h;

			w = cell.Column.Width.Value - 1;
			x = cell.Column.Left.Value - _hBar.Value + 1;
			y = RowsToPixels(cell.RowIndex.Value) + 1; // We can't draw without row and column, so assume they exist and fail catastrophically if they don't
			h = CellHeight - 1;
			if (y < ColumnHeight)
			{
				return;
			}

			if (x > DrawWidth || y > DrawHeight)
			{
				return;
			} // Don't draw if off screen.

			var col = cell.Column.Name;
			if (color.A == 0)
			{
				sBrush = new SolidBrush(Color.FromArgb(255, color));
			}
			else
			{
				sBrush = new SolidBrush(color);
			}

			e.Graphics.FillRectangle(sBrush, x, y, w, h);
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
			// Do nothing, and this should never be called
		}

		private void DoSelectionBG(PaintEventArgs e, List<ListColumn> visibleColumns)
		{
			// SuuperW: This allows user to see other colors in selected frames.
			Color rowColor = Color.White;
			int _lastVisibleRow = LastVisibleRow;
			int lastRow = -1;
			foreach (Cell cell in _selectedItems)
			{
				if (cell.RowIndex > _lastVisibleRow || cell.RowIndex < FirstVisibleRow || !VisibleColumns.Contains(cell.Column))
				{
					continue;
				}

				Cell relativeCell = new Cell
				{
					RowIndex = cell.RowIndex - FirstVisibleRow,
					Column = cell.Column,
				};
				//relativeCell.RowIndex -= CountLagFramesAbsolute(relativeCell.RowIndex.Value);

				if (QueryRowBkColor != null && lastRow != cell.RowIndex.Value)
				{
					QueryRowBkColor(cell.RowIndex.Value, ref rowColor);
					lastRow = cell.RowIndex.Value;
				}

				Color cellColor = rowColor;
				QueryItemBkColor(cell.RowIndex.Value, cell.Column, ref cellColor);

				// Alpha layering for cell before selection
				float alpha = (float)cellColor.A / 255;
				if (cellColor.A != 255 && cellColor.A != 0)
				{
					cellColor = Color.FromArgb(rowColor.R - (int)((rowColor.R - cellColor.R) * alpha),
						rowColor.G - (int)((rowColor.G - cellColor.G) * alpha),
						rowColor.B - (int)((rowColor.B - cellColor.B) * alpha));
				}

				// Alpha layering for selection
				alpha = 0.33f;
				cellColor = Color.FromArgb(cellColor.R - (int)((cellColor.R - SystemColors.Highlight.R) * alpha),
					cellColor.G - (int)((cellColor.G - SystemColors.Highlight.G) * alpha),
					cellColor.B - (int)((cellColor.B - SystemColors.Highlight.B) * alpha));
				DrawCellBG(e, cellColor, relativeCell, visibleColumns);
			}
		}

		/// <summary>
		/// Calls QueryItemBkColor callback for all visible cells and fills in the background of those cells.
		/// </summary>
		/// <param name="e"></param>
		private void DoBackGroundCallback(PaintEventArgs e, List<ListColumn> visibleColumns)
		{
			int startIndex = FirstVisibleRow;
			int range = Math.Min(LastVisibleRow, RowCount - 1) - startIndex + 1;
			int lastVisible = LastVisibleColumnIndex;
			int firstVisibleColumn = FirstVisibleColumn;
			// Prevent exceptions with small TAStudio windows
			if (firstVisibleColumn < 0)
			{
				return;
			}
			for (int i = 0, f = 0; f < range; i++, f++) // Vertical
			{
				//f += _lagFrames[i];

				Color rowColor = Color.White;
				QueryRowBkColor?.Invoke(f + startIndex, ref rowColor);

				for (int j = FirstVisibleColumn; j <= lastVisible; j++) // Horizontal
				{
					Color itemColor = Color.White;
					QueryItemBkColor(f + startIndex, visibleColumns[j], ref itemColor);
					if (itemColor == Color.White)
					{
						itemColor = rowColor;
					}
					else if (itemColor.A != 255 && itemColor.A != 0)
					{
						float alpha = (float)itemColor.A / 255;
						itemColor = Color.FromArgb(rowColor.R - (int)((rowColor.R - itemColor.R) * alpha),
							rowColor.G - (int)((rowColor.G - itemColor.G) * alpha),
							rowColor.B - (int)((rowColor.B - itemColor.B) * alpha));
					}

					if (itemColor != Color.White) // An easy optimization, don't draw unless the user specified something other than the default
					{
						var cell = new Cell
						{
							Column = visibleColumns[j],
							RowIndex = i
						};
						DrawCellBG(e, itemColor, cell, visibleColumns);
					}
				}
			}
		}
	}
}
