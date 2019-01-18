using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// New GDI+ methods live here
	/// </summary>
	public partial class InputRoll
	{
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

		#region Drawing Methods Using GDI+

		private void GDIP_OnPaint(PaintEventArgs e)
		{

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
