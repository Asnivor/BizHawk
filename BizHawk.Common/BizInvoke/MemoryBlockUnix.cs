using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BizHawk.Common.BizInvoke
{
	public sealed class MemoryBlockUnix : MemoryBlock
	{
		/// <summary>
		/// handle returned by memfd_create
		/// </summary>
		private int _fd;

		/// <summary>
		/// allocate size bytes at any address
		/// </summary>
		/// <param name="size"></param>
		public MemoryBlockUnix(ulong size) : this(0, size) {}

		/// <summary>
		/// allocate size bytes starting at a particular address
		/// </summary>
		/// <param name="start"></param>
		/// <param name="size"></param>
		public MemoryBlockUnix(ulong start, ulong size)
		{
			if (!WaterboxUtils.Aligned(start)) throw new ArgumentOutOfRangeException();
			if (size == 0) throw new ArgumentOutOfRangeException();
			size = WaterboxUtils.AlignUp(size);
			_fd = Kernel.memfd_create("MemoryBlockUnix", 0);
			if (_fd == -1) throw new InvalidOperationException("memfd_create() returned -1");
			Start = start;
			End = start + size;
			Size = size;
			_pageData = new Protection[GetPage(End - 1) + 1];
		}

		/// <summary>
		/// activate the memory block, swapping it in at the specified address
		/// </summary>
		public override void Activate()
		{
			if (Active) throw new InvalidOperationException("Already active");
			if (Kernel.mmap(Z.US(Start), Z.UU(Size), Kernel.Protection.Read | Kernel.Protection.Write | Kernel.Protection.Execute, 16, _fd, IntPtr.Zero) != Z.US(Start))
				throw new InvalidOperationException("mmap() returned NULL");
			ProtectAll();
			Active = true;
		}

		/// <summary>
		/// deactivate the memory block, removing it from RAM but leaving it immediately available to swap back in
		/// </summary>
		public override void Deactivate()
		{
			if (!Active)
				throw new InvalidOperationException("Not active");
			if (Kernel.munmap(Z.US(Start), Z.UU(Size)) != 0)
				throw new InvalidOperationException("munmap() returned -1");
			Active = false;
		}

		/// <summary>
		/// take a snapshot of the entire memory block's contents, for use in GetXorStream
		/// </summary>
		public override void SaveXorSnapshot()
		{
			if (_snapshot != null)
				throw new InvalidOperationException("Snapshot already taken");
			if (!Active)
				throw new InvalidOperationException("Not active");

			// temporarily switch the entire block to `R`: in case some areas are unreadable, we don't want
			// that to complicate things
			if (Kernel.mprotect(Z.US(Start), Z.UU(Size), Kernel.Protection.Read) != 0)
				throw new InvalidOperationException("mprotect() returned -1!");

			_snapshot = new byte[Size];
			var ds = new MemoryStream(_snapshot, true);
			var ss = GetStream(Start, Size, false);
			ss.CopyTo(ds);
			XorHash = WaterboxUtils.Hash(_snapshot);

			ProtectAll();
		}

		/// <summary>
		/// take a hash of the current full contents of the block, including unreadable areas
		/// </summary>
		/// <returns></returns>
		public override byte[] FullHash()
		{
			if (!Active)
				throw new InvalidOperationException("Not active");
			// temporarily switch the entire block to `R`
			if (Kernel.mprotect(Z.US(Start), Z.UU(Size), Kernel.Protection.Read) != 0)
				throw new InvalidOperationException("mprotect() returned -1!");
			var ret = WaterboxUtils.Hash(GetStream(Start, Size, false));
			ProtectAll();
			return ret;
		}

		private static Kernel.Protection GetKernelMemoryProtectionValue(Protection prot)
		{
			switch (prot)
			{
				case Protection.None: return 0;
				case Protection.R: return Kernel.Protection.Read;
				case Protection.RW: return Kernel.Protection.Read | Kernel.Protection.Write;
				case Protection.RX: return Kernel.Protection.Read | Kernel.Protection.Execute;
			}
			throw new ArgumentOutOfRangeException(nameof(prot));
		}

		/// <summary>
		/// restore all recorded protections
		/// </summary>
		protected override void ProtectAll()
		{
			int ps = 0;
			for (int i = 0; i < _pageData.Length; i++)
			{
				if (i == _pageData.Length - 1 || _pageData[i] != _pageData[i + 1])
				{
					var p = GetKernelMemoryProtectionValue(_pageData[i]);
					ulong zstart = GetStartAddr(ps);
					ulong zend = GetStartAddr(i + 1);
					if (Kernel.mprotect(Z.US(zstart), Z.UU(zend - zstart), p) != 0)
						throw new InvalidOperationException("mprotect() returned -1!");
					ps = i + 1;
				}
			}
		}

		/// <summary>
		/// set r/w/x protection on a portion of memory.  rounded to encompassing pages
		/// </summary>
		public override void Protect(ulong start, ulong length, Protection prot)
		{
			if (length == 0)
				return;
			int pstart = GetPage(start);
			int pend = GetPage(start + length - 1);

			var p = GetKernelMemoryProtectionValue(prot);
			for (int i = pstart; i <= pend; i++)
				_pageData[i] = prot; // also store the value for later use

			if (Active) // it's legal to Protect() if we're not active; the information is just saved for the next activation
			{
				var computedStart = WaterboxUtils.AlignDown(start);
				var computedEnd = WaterboxUtils.AlignUp(start + length);
				var computedLength = computedEnd - computedStart;

				if (Kernel.mprotect(Z.US(computedStart), Z.UU(computedLength), p) != 0)
					throw new InvalidOperationException("mprotect() returned -1!");
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (_fd != 0)
			{
				if (Active) Deactivate();
				Kernel.close(_fd);
				_fd = -1;
			}
		}

		~MemoryBlockUnix()
		{
			Dispose(false);
		}

		private static class Kernel
		{
			[DllImport("libc.so.6")]
			public static extern int memfd_create(string name, uint flags);
			[DllImport("libc.so.6")]
			public static extern int close(int fd);
			[DllImport("libc.so.6")]
			public static extern IntPtr mmap(IntPtr addr, UIntPtr length, int prot, int flags, int fd, IntPtr offset);
			[DllImport("libc.so.6")]
			public static extern int munmap(IntPtr addr, UIntPtr length);
			[DllImport("libc.so.6")]
			public static extern int mprotect(IntPtr addr, UIntPtr len, int prot);

			public static IntPtr mmap(IntPtr addr, UIntPtr length, Protection prot, int flags, int fd, IntPtr offset)
			{
				return mmap(addr, length, (int) prot, flags, fd, offset);
			}
			public static int mprotect(IntPtr addr, UIntPtr len, Protection prot)
			{
				return mprotect(addr, len, (int) prot);
			}

			[Flags]
			public enum Protection : int
			{
				Read = 0x1,
				Write = 0x2,
				Execute = 0x4
			}
		}
	}
}
