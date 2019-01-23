using System.Collections.Generic;
using System.ComponentModel;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// A performant VirtualListView implementation that doesn't rely on native Win32 API calls
	/// (and in fact does not inherit the ListView class at all)
	/// It is a simplified version of the work done with GDI+ rendering in InputRoll.cs
	/// -------------------------
	/// *** Public Properties ***
	/// -------------------------
	/// </summary>
	public partial class PlatformAgnosticVirtualListView
	{
		public bool AllowRightClickSelecton { get; set; }
		public bool LetKeysModifySelection { get; set; }
		public bool SuspendHotkeys { get; set; }

		/// <summary>
		/// Gets or sets the amount of left and right padding on the text inside a cell
		/// </summary>
		[DefaultValue(3)]
		[Category("Behavior")]
		public int CellWidthPadding { get; set; }

		/// <summary>
		/// Gets or sets the amount of top and bottom padding on the text inside a cell
		/// </summary>
		[DefaultValue(1)]
		[Category("Behavior")]
		public int CellHeightPadding { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether grid lines are displayed around cells
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(true)]
		public bool GridLines { get; set; }		

		/// <summary>
		/// Gets or sets the scrolling speed
		/// </summary>
		[Category("Behavior")]
		public int ScrollSpeed
		{
			get
			{
				if (CellHeight == 0)
					CellHeight++;
				return _vBar.SmallChange / CellHeight;
			}

			set
			{
				_vBar.SmallChange = value * CellHeight;
			}
		}

		/// <summary>
		/// Gets or sets the sets the virtual number of rows to be displayed. Does not include the column header row.
		/// </summary>
		[Category("Behavior")]
		public int RowCount
		{
			get
			{
				return _rowCount;
			}

			set
			{
				_rowCount = value;
				RecalculateScrollBars();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether columns can be resized
		/// </summary>
		[Category("Behavior")]
		public bool AllowColumnResize { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether columns can be reordered
		/// </summary>
		[Category("Behavior")]
		public bool AllowColumnReorder { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the entire row will always be selected
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(false)]
		public bool FullRowSelect { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether multiple items can to be selected
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool MultiSelect { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the control is in input painting mode
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(false)]
		public bool InputPaintingMode { get; set; }

		/// <summary>
		/// All visible columns
		/// </summary>
		[Category("Behavior")]
		public IEnumerable<ListColumn> VisibleColumns => _columns.VisibleColumns;

		/// <summary>
		/// Gets or sets how the InputRoll scrolls when calling ScrollToIndex.
		/// </summary>
		[DefaultValue("near")]
		[Category("Behavior")]
		public string ScrollMethod { get; set; }

		/// <summary>
		/// Gets or sets a value indicating how the Intever for the hover event
		/// </summary>
		[Category("Behavior")]
		public bool AlwaysScroll { get; set; }

		/// <summary>
		/// Gets or sets the lowest seek interval to activate the progress bar
		/// </summary>
		[Category("Behavior")]
		public int SeekingCutoffInterval { get; set; }

		/// <summary>
		/// Returns all columns including those that are not visible
		/// </summary>
		/// <returns></returns>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ListColumns AllColumns => _columns;

		[DefaultValue(750)]
		[Category("Behavior")]
		public int HoverInterval
		{
			get { return _hoverTimer.Interval; }
			set { _hoverTimer.Interval = value; }
		}
	}
}
