using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// New GDI+ methods live here
	/// </summary>
	public partial class InputRoll
	{
		private Brush baseBackground = null;

		#region Initialization and Destruction

		/// <summary>
		/// Initializes GDI+ related stuff
		/// (called from constructor)
		/// </summary>
		private void GDIPConstruction()
		{
			// HFont?
			// Rotated HFont?

			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			SetStyle(ControlStyles.UserPaint, true);
			SetStyle(ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.Opaque, true);

			using (var g = CreateGraphics())
			{
				var sizeF = g.MeasureString("A", _commonFont);
				_charSize = Size.Round(sizeF);
			}
		}

		private void GDIPDispose()
		{

		}

		#endregion

		#region Helper Functions

		private int GetAlpha(int val)
		{
			var w = (val & 0xFF0000);
			return w;
		}

		#endregion

		#region Drawing Methods Using GDI+

		private void GDIP_OnPaint(PaintEventArgs e)
		{
			// white background
			if (baseBackground == null)
			{
				baseBackground = new SolidBrush(Color.White);
			}

			Rectangle rect = e.ClipRectangle;
			e.Graphics.FillRectangle(baseBackground, rect);
			e.Graphics.Flush();

			// Lag frame calculations
			SetLagFramesArray();

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

		private void GDIP_DrawColumnDrag(PaintEventArgs e)
		{

		}

		private void GDIP_DrawCellDrag(PaintEventArgs e)
		{

		}

		private void GDIP_DrawColumnText(PaintEventArgs e, List<RollColumn> visibleColumns)
		{

		}

		private void GDIP_DrawData(PaintEventArgs e, List<RollColumn> visibleColumns)
		{

		}

		private void GDIP_DrawColumnBg(PaintEventArgs e, List<RollColumn> visibleColumns)
		{
			Brush b = new SolidBrush(SystemColors.ControlLight);
			Pen p = new Pen(Color.Black);

			if (HorizontalOrientation)
			{
				e.Graphics.FillRectangle(b, 0, 0, ColumnWidth + 1, DrawHeight + 1);
				e.Graphics.DrawLine(p, 0, 0, 0, visibleColumns.Count * CellHeight + 1);
				e.Graphics.DrawLine(p, ColumnWidth, 0, ColumnWidth, visibleColumns.Count * CellHeight + 1);

				int start = -_vBar.Value;
				foreach (var column in visibleColumns)
				{
					e.Graphics.DrawLine(p, 1, start, ColumnWidth, start);
					start += CellHeight;
				}

				if (visibleColumns.Any())
				{
					e.Graphics.DrawLine(p, 1, start, ColumnWidth, start);
				}
			}
			else
			{
				int bottomEdge = RowsToPixels(0);

				// Gray column box and black line underneath
				e.Graphics.FillRectangle(b, 0, 0, Width + 1, bottomEdge + 1);
				e.Graphics.DrawLine(p, 0, 0, TotalColWidth.Value + 1, 0);
				e.Graphics.DrawLine(p, 0, bottomEdge, TotalColWidth.Value + 1, bottomEdge);

				// Vertical black seperators
				for (int i = 0; i < visibleColumns.Count; i++)
				{
					int pos = visibleColumns[i].Left.Value - _hBar.Value;
					e.Graphics.DrawLine(p, pos, 0, pos, bottomEdge);
				}

				// Draw right most line
				if (visibleColumns.Any())
				{
					int right = TotalColWidth.Value - _hBar.Value;
					e.Graphics.DrawLine(p, right, 0, right, bottomEdge);
				}
			}

			// Emphasis
			foreach (var column in visibleColumns.Where(c => c.Emphasis))
			{
				b = new SolidBrush(SystemColors.ActiveBorder);
				if (HorizontalOrientation)
				{
					e.Graphics.FillRectangle(b, 1, visibleColumns.IndexOf(column) * CellHeight + 1, ColumnWidth - 1, ColumnHeight - 1);
				}
				else
				{
					e.Graphics.FillRectangle(b, column.Left.Value + 1 - _hBar.Value, 1, column.Width.Value - 1, ColumnHeight - 1);
				}
			}

			// If the user is hovering over a column
			if (IsHoveringOnColumnCell)
			{
				if (HorizontalOrientation)
				{
					for (int i = 0; i < visibleColumns.Count; i++)
					{
						if (visibleColumns[i] != CurrentCell.Column)
						{
							continue;
						}

						if (CurrentCell.Column.Emphasis)
						{
							b = new SolidBrush(Color.FromArgb(GetAlpha(0x00222222), SystemColors.Highlight));
							//_gdi.SetBrush(Add(SystemColors.Highlight, 0x00222222));
						}
						else
						{
							b = new SolidBrush(SystemColors.Highlight);
							//_gdi.SetBrush(SystemColors.Highlight);
						}

						e.Graphics.FillRectangle(b, 1, i * CellHeight + 1, ColumnWidth - 1, ColumnHeight - 1);
					}
				}
				else
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
								b = new SolidBrush(Color.FromArgb(GetAlpha(0x00550000), SystemColors.Highlight));
								//_gdi.SetBrush(Add(SystemColors.Highlight, 0x00550000));
							}
							else
							{
								b = new SolidBrush(SystemColors.Highlight);
								//_gdi.SetBrush(SystemColors.Highlight);
							}

							e.Graphics.FillRectangle(b, left + 1, 1, width - 1, ColumnHeight - 1);
						}
					}
				}
			}
		}

		private void GDIP_DrawBg(PaintEventArgs e, List<RollColumn> visibleColumns)
		{

		}

		private void GDIP_DrawCellBG(Color color, Cell cell, List<RollColumn> visibleColumns)
		{

		}

		#endregion
	}
}
