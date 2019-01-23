using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// A performant VirtualListView implementation that doesn't rely on native Win32 API calls
	/// (and in fact does not inherit the ListView class at all)
	/// It is a simplified version of the work done with GDI+ rendering in InputRoll.cs
	/// -------------------
	/// *** API Related ***
	/// -------------------
	/// </summary>
	public partial class PlatformAgnosticVirtualListView
	{
		private Cell _draggingCell;

		#region Methods

		/// <summary>
		/// Parent form calls this to add columns
		/// </summary>
		/// <param name="columnName"></param>
		/// <param name="columnText"></param>
		/// <param name="columnWidth"></param>
		/// <param name="columnType"></param>
		public void AddColumn(string columnName, string columnText, int columnWidth, ListColumn.InputType columnType = ListColumn.InputType.Boolean)
		{
			if (AllColumns[columnName] == null)
			{
				var column = new ListColumn
				{
					Name = columnName,
					Text = columnText,
					Width = columnWidth,
					Type = columnType
				};

				AllColumns.Add(column);
			}
		}

		/// <summary>
		/// Sets the state of the passed row index
		/// </summary>
		/// <param name="index"></param>
		/// <param name="val"></param>
		public void SelectItem(int index, bool val)
		{
			if (_columns.VisibleColumns.Any())
			{
				if (val)
				{
					SelectCell(new Cell
					{
						RowIndex = index,
						Column = _columns[0]
					});
				}
				else
				{
					IEnumerable<Cell> items = _selectedItems.Where(cell => cell.RowIndex == index);
					_selectedItems.RemoveWhere(items.Contains);
				}
			}
		}

		public void SelectAll()
		{
			var oldFullRowVal = FullRowSelect;
			FullRowSelect = true;
			for (int i = 0; i < ItemCount; i++)
			{
				SelectItem(i, true);
			}

			FullRowSelect = oldFullRowVal;
		}

		public void DeselectAll()
		{
			_selectedItems.Clear();
		}

		public void TruncateSelection(int index)
		{
			_selectedItems.RemoveWhere(cell => cell.RowIndex > index);
		}

		public bool IsVisible(int index)
		{
			return (index >= FirstVisibleRow) && (index <= LastFullyVisibleRow);
		}

		public bool IsPartiallyVisible(int index)
		{
			return index >= FirstVisibleRow && index <= LastVisibleRow;
		}

		public void DragCurrentCell()
		{
			_draggingCell = CurrentCell;
		}

		public void ReleaseCurrentCell()
		{
			if (_draggingCell != null)
			{
				var draggedCell = _draggingCell;
				_draggingCell = null;

				if (CurrentCell != draggedCell)
				{
					CellDropped?.Invoke(this, new CellEventArgs(draggedCell, CurrentCell));
				}
			}
		}

		/// <summary>
		/// Scrolls to the given index, according to the scroll settings.
		/// </summary>
		public void ScrollToIndex(int index)
		{
			if (ScrollMethod == "near")
			{
				MakeIndexVisible(index);
			}

			if (!IsVisible(index) || AlwaysScroll)
			{
				if (ScrollMethod == "top")
				{
					FirstVisibleRow = index;
				}
				else if (ScrollMethod == "bottom")
				{
					LastVisibleRow = index;
				}
				else if (ScrollMethod == "center")
				{
					FirstVisibleRow = Math.Max(index - (VisibleRows / 2), 0);
				}
			}
		}

		/// <summary>
		/// Scrolls so that the given index is visible, if it isn't already; doesn't use scroll settings.
		/// </summary>
		public void MakeIndexVisible(int index)
		{
			if (!IsVisible(index))
			{
				if (FirstVisibleRow > index)
				{
					FirstVisibleRow = index;
				}
				else
				{
					LastVisibleRow = index;
				}
			}
		}

		public void ClearSelectedRows()
		{
			_selectedItems.Clear();
		}

		#endregion

		#region Properties

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool IsPointingAtColumnHeader => IsHoveringOnColumnCell;

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int? FirstSelectedIndex
		{
			get
			{
				if (AnyRowsSelected)
				{
					return SelectedRows.Min();
				}

				return null;
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int? LastSelectedIndex
		{
			get
			{
				if (AnyRowsSelected)
				{
					return SelectedRows.Max();
				}

				return null;
			}
		}

		/// <summary>
		/// Gets or sets the current Cell that the mouse was in.
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Cell CurrentCell { get; set; }

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool CurrentCellIsDataCell => CurrentCell?.RowIndex != null && CurrentCell.Column != null;

		/// <summary>
		/// Gets or sets the previous Cell that the mouse was in.
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Cell LastCell { get; private set; }

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool IsPaintDown { get; private set; }

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool UseCustomBackground { get; set; }

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int DrawHeight { get; private set; }

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int DrawWidth { get; private set; }		

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool RightButtonHeld { get; private set; }

		/// <summary>
		/// Gets or sets the first visible row index, if scrolling is needed
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int FirstVisibleRow
		{
			get // SuuperW: This was checking if the scroll bars were needed, which is useless because their Value is 0 if they aren't needed.
			{
				if (CellHeight == 0) CellHeight++;
				return _vBar.Value / CellHeight;
			}

			set
			{
				if (NeedsVScrollbar)
				{
					_programmaticallyUpdatingScrollBarValues = true;
					if (value * CellHeight <= _vBar.Maximum)
					{
						_vBar.Value = value * CellHeight;
					}
					else
					{
						_vBar.Value = _vBar.Maximum;
					}

					_programmaticallyUpdatingScrollBarValues = false;
				}
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		private int LastFullyVisibleRow
		{
			get
			{
				int halfRow = 0;
				if ((DrawHeight - ColumnHeight - 3) % CellHeight < CellHeight / 2)
				{
					halfRow = 1;
				}

				return FirstVisibleRow + VisibleRows - halfRow; // + CountLagFramesDisplay(VisibleRows - halfRow);
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int LastVisibleRow
		{
			get
			{
				return FirstVisibleRow + VisibleRows; // + CountLagFramesDisplay(VisibleRows);
			}

			set
			{
				int halfRow = 0;
				if ((DrawHeight - ColumnHeight - 3) % CellHeight < CellHeight / 2)
				{
					halfRow = 1;
				}

				FirstVisibleRow = Math.Max(value - (VisibleRows - halfRow), 0);
			}
		}

		/// <summary>
		/// Gets the number of rows currently visible including partially visible rows.
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int VisibleRows
		{
			get
			{
				if (CellHeight == 0) CellHeight++;
				return (DrawHeight - ColumnHeight - 3) / CellHeight; // Minus three makes it work
			}
		}

		/// <summary>
		/// Gets the first visible column index, if scrolling is needed
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int FirstVisibleColumn
		{
			get
			{
				if (CellHeight == 0) CellHeight++;
				var columnList = VisibleColumns.ToList();
				return columnList.FindIndex(c => c.Right > _hBar.Value);
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int LastVisibleColumnIndex
		{
			get
			{
				if (CellHeight == 0) CellHeight++;
				List<ListColumn> columnList = VisibleColumns.ToList();
				int ret;
				ret = columnList.FindLastIndex(c => c.Left <= DrawWidth + _hBar.Value);
				return ret;
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public IEnumerable<int> SelectedRows
		{
			get
			{
				return _selectedItems
					.Where(cell => cell.RowIndex.HasValue)
					.Select(cell => cell.RowIndex.Value)
					.Distinct();
			}
		}

		public bool AnyRowsSelected
		{
			get
			{
				return _selectedItems.Any(cell => cell.RowIndex.HasValue);
			}
		}

		#endregion
	}
}
