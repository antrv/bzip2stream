using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace System.IO.Compression
{
	partial class BZip2Stream
	{
#if EMBED_LIBBZIP2
		private const string cLibbzip2 = "AR.BZip2.dll";
#else
		//private const string cLibbzip2 = "libbzip2.dll";
		private const string cLibbzip2 = "AR.BZip2.dll";
#endif

		private enum BzErrorCode
		{
			BZ_OK = 0,
			BZ_RUN_OK = 1,
			BZ_FLUSH_OK = 2,
			BZ_FINISH_OK = 3,
			BZ_STREAM_END = 4,
			BZ_SEQUENCE_ERROR = -1,
			BZ_PARAM_ERROR = -2,
			BZ_MEM_ERROR = -3,
			BZ_DATA_ERROR = -4,
			BZ_DATA_ERROR_MAGIC = -5,
			BZ_IO_ERROR = -6,
			BZ_UNEXPECTED_EOF = -7,
			BZ_OUTBUFF_FULL = -8,
			BZ_CONFIG_ERROR = -9,
		}

		[StructLayout(LayoutKind.Sequential)]
		private unsafe class BzStream
		{
			public byte* next_in;
			public int avail_in;
			public long total_in;

			public byte* next_out;
			public int avail_out;
			public long total_out;

			private IntPtr state;

			private IntPtr bzalloc;
			private IntPtr bzfree;

			private IntPtr opaque;
		}

		private enum BzAction
		{
			BZ_RUN = 0,
			BZ_FLUSH = 1,
			BZ_FINISH = 2,
		}

		[DllImport(cLibbzip2)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static extern BzErrorCode BZ2_bzCompressInit(IntPtr stream, [MarshalAs(UnmanagedType.I4)]BZip2CompressionLevel blockSize100k, int verbosity, int workFactor);

		[DllImport(cLibbzip2)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static extern BzErrorCode BZ2_bzCompress(IntPtr stream, [MarshalAs(UnmanagedType.I4)]BzAction action);

		[DllImport(cLibbzip2)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static extern BzErrorCode BZ2_bzCompressEnd(IntPtr stream);

		[DllImport(cLibbzip2)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static extern BzErrorCode BZ2_bzDecompressInit(IntPtr stream, int verbosity, int small);

		[DllImport(cLibbzip2)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static extern BzErrorCode BZ2_bzDecompress(IntPtr stream);

		[DllImport(cLibbzip2)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static extern BzErrorCode BZ2_bzDecompressEnd(IntPtr stream);
	}
}
