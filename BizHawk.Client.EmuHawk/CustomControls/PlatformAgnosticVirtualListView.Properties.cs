using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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
		#region ListView Compatibility Properties

		/// <summary>
		/// This VirtualListView implementation doesn't really need this, but it is here for compatibility
		/// </summary>
		[Category("Behavior")]
		public int VirtualListSize
		{
			get
			{
				return _itemCount;
			}

			set
			{
				_itemCount = value;
				RecalculateScrollBars();
			}
		}

		/// <summary>
		/// ListView compatibility property
		/// </summary>
		[System.ComponentModel.Browsable(false)]
		public System.Windows.Forms.ListView.SelectedIndexCollection SelectedIndices
		{
			// !!! does not work properly, avoid using this in the calling implementation !!!
			get
			{
				var tmpListView = new System.Windows.Forms.ListView();
				//tmpListView.VirtualMode = true;
				//var selectedIndexCollection = new System.Windows.Forms.ListView.SelectedIndexCollection(tmpListView);
				//tmpListView.VirtualListSize = ItemCount;
				for (int i = 0; i < ItemCount; i++)
				{
					tmpListView.Items.Add(i.ToString());
				}
				
				//tmpListView.Refresh();				

				if (AnyRowsSelected)
				{
					var indices = SelectedRows.ToList();
					foreach (var i in indices)
					{
						tmpListView.SelectedIndices.Add(i);
						//selectedIndexCollection.Add(i);
					}
				}

				return tmpListView.SelectedIndices; // selectedIndexCollection;
			}
		}

		/// <summary>
		/// Compatibility property
		/// With a standard ListView you can add columns in the Designer
		/// We will ignore this (but leave it here for compatibility)
		/// Columns must be added through the AddColumns() public method
		/// </summary>
		public System.Windows.Forms.ListView.ColumnHeaderCollection Columns = new System.Windows.Forms.ListView.ColumnHeaderCollection(new System.Windows.Forms.ListView());

		/// <summary>
		/// Compatibility with ListView class
		/// This is not used in this implementation
		/// </summary>
		[Category("Behavior")]
		public bool VirtualMode { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the selected item in the control remains highlighted when the control loses focus
		/// </summary>
		[Category("Behavior")]
		public bool HideSelection { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the ListView uses state image behavior that is compatible with the .NET Framework 1.1 or the .NET Framework 2.0.
		/// Here for ListView api compatibility (we dont care about this)
		/// </summary>
		[System.ComponentModel.Browsable(false)]
		public bool UseCompatibleStateImageBehavior { get; set; }

		/// <summary>
		/// Gets or sets how items are displayed in the control.
		/// Here for ListView api compatibility (we dont care about this)
		/// </summary>
		public System.Windows.Forms.View View { get; set; }

		#endregion

		#region VirtualListView Compatibility Properties

		/// <summary>
		/// Informs user that a select all event is in place, can be used in change events to wait until this is false
		/// Not used in this implementation (yet)
		/// </summary>
		public bool SelectAllInProgress { get; set; }

		/// <summary>
		/// Gets/Sets the selected item
		/// Here for compatibility with VirtualListView.cs
		/// </summary>
		public int selectedItem
		{
			get
			{
				if (SelectedRows.Count() == 0)
				{
					return -1;
				}
				else
				{
					return SelectedRows.First();
				}
			}
			set
			{
				SelectItem(value, true);
			}
		}

		#endregion

		public bool AllowRightClickSelecton { get; set; }
		public bool LetKeysModifySelection { get; set; }
		public bool SuspendHotkeys { get; set; }

		public bool BlazingFast { get; set; }

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
		public int ItemCount
		{
			get
			{
				return _itemCount;
			}

			set
			{
				_itemCount = value;
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
