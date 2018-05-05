using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;

namespace System.IO.Compression
{
	public partial class BZip2Stream: Stream
	{
		private const int BufferSize = 128 * 1024;
		private readonly BZip2CompressionLevel _level;
		private readonly CompressionMode _mode;
		private readonly bool _leaveOpen;

		private readonly byte[] _buffer = new byte[BufferSize];
		private int _bufferOffset;
		private int _bufferLength;

		private readonly BzStream _data;
		private readonly IntPtr _dataAddr;
	    private GCHandle _dataHandle;

        private Stream _stream;
		private bool _initialized;
		private int _activeAsyncOperation;

		/// <summary>
		/// Initializes a new instance of the BZip2Stream class by using the specified stream and compression mode, and optionally leaves the stream open.
		/// </summary>
		/// <param name="stream">The stream to compress or decompress.</param>
		/// <param name="mode">One of the enumeration values that indicates whether to compress or decompress the stream.</param>
		/// <param name="leaveOpen">true to leave the stream open after disposing the GZipStream object; otherwise, false.</param>
		public BZip2Stream(Stream stream, CompressionMode mode, bool leaveOpen)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream), "stream is null.");
			if (mode != CompressionMode.Compress && mode != CompressionMode.Decompress)
				throw new ArgumentException("mode is not a valid CompressionMode enumeration value.", nameof(mode));
			if (mode == CompressionMode.Compress && !stream.CanWrite)
				throw new ArgumentException("CompressionMode is Compress and CanWrite is false.", nameof(mode));
			if (mode == CompressionMode.Decompress && !stream.CanRead)
				throw new ArgumentException("CompressionMode is Decompress and CanRead is false.", nameof(mode));

		    _stream = stream;
			_mode = mode;
			_level = BZip2CompressionLevel.Default;
			_leaveOpen = leaveOpen;

			_data = new BzStream();
			_dataHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
			_dataAddr = _dataHandle.AddrOfPinnedObject();
		}

		/// <summary>
		/// Initializes a new instance of the BZip2Stream class by using the specified stream and compression mode.
		/// </summary>
		/// <param name="stream">The stream to compress or decompress.</param>
		/// <param name="mode">One of the enumeration values that indicates whether to compress or decompress the stream.</param>
		public BZip2Stream(Stream stream, CompressionMode mode)
			: this(stream, mode, false)
		{
		}

		/// <summary>
		/// Initializes a new instance of the BZip2Stream class by using the specified stream and compression mode, and optionally leaves the stream open.
		/// </summary>
		/// <param name="stream">The stream to compress or decompress.</param>
		/// <param name="level"> </param>
		/// <param name="leaveOpen">true to leave the stream open after disposing the GZipStream object; otherwise, false.</param>
		public BZip2Stream(Stream stream, BZip2CompressionLevel level, bool leaveOpen)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream), "stream is null.");
			if (level < BZip2CompressionLevel.Lowest || level > BZip2CompressionLevel.Highest)
				throw new ArgumentException("level is not a valid BZip2CompressionLevel enumeration value.", nameof(level));
			if (!stream.CanWrite)
				throw new ArgumentException("CanWrite is false.", nameof(stream));

		    _stream = stream;
			_level = level;
			_mode = CompressionMode.Compress;
			_leaveOpen = leaveOpen;

			_data = new BzStream();
			_dataHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
			_dataAddr = _dataHandle.AddrOfPinnedObject();
		}

		/// <summary>
		/// Initializes a new instance of the BZip2Stream class by using the specified stream and compression mode.
		/// </summary>
		/// <param name="stream">The stream to compress or decompress.</param>
		/// <param name="level">One of the enumeration values that indicates whether to compress or decompress the stream.</param>
		public BZip2Stream(Stream stream, BZip2CompressionLevel level)
			: this(stream, level, false)
		{
		}

	    ~BZip2Stream()
	    {
            Dispose(false);
	    }

		private static BzErrorCode CheckErrorCode(BzErrorCode errorCode)
		{
			switch (errorCode)
			{
				case BzErrorCode.BZ_CONFIG_ERROR:
					throw new InvalidOperationException("The libbzip2.dll has been mis-compiled");
				case BzErrorCode.BZ_PARAM_ERROR:
					throw new InvalidOperationException("Invalid parameters (libbzip2.dll)");
				case BzErrorCode.BZ_MEM_ERROR:
					throw new OutOfMemoryException("Not enough memory is available.");
				case BzErrorCode.BZ_SEQUENCE_ERROR:
					throw new InvalidOperationException("Invalid method invokation sequence (libbzip2.dll)");
				case BzErrorCode.BZ_DATA_ERROR:
					throw new InvalidOperationException("A data integrity error is detected in the compressed stream.");
				case BzErrorCode.BZ_DATA_ERROR_MAGIC:
					throw new InvalidOperationException("The compressed stream doesn’t begin with the right magic bytes.");
				case BzErrorCode.BZ_IO_ERROR:
					throw new IOException("I/O error has occured.");
			}
			return errorCode;
		}

		/// <summary>
		/// Gets a reference to the underlying stream.
		/// </summary>
		public Stream BaseStream => _stream;

	    /// <summary>
	    /// Gets a value indicating whether the stream supports reading while decompressing a file.
	    /// </summary>
	    public override bool CanRead => _mode == CompressionMode.Decompress && _stream.CanRead;

	    /// <summary>
		/// Gets a value indicating whether the stream supports writing.
		/// </summary>
		public override bool CanWrite => _mode == CompressionMode.Compress && _stream.CanWrite;

	    /// <summary>
		/// Gets a value indicating whether the stream supports seeking.
		/// </summary>
		public override bool CanSeek => false;

	    /// <summary>
		/// This property is not supported and always throws a NotSupportedException.
		/// </summary>
		public override long Length => throw new NotSupportedException();

	    /// <summary>
		/// This property is not supported and always throws a NotSupportedException.
		/// </summary>
		public override long Position
		{
			get => throw new NotSupportedException();
	        set => throw new NotSupportedException();
	    }

		/// <summary>
		/// Begins an asynchronous read operation.
		/// </summary>
		/// <param name="buffer">The byte array to read the data into.</param>
		/// <param name="offset">The byte offset in array at which to begin reading data from the stream.</param>
		/// <param name="count">The maximum number of bytes to read.</param>
		/// <param name="callback">An optional asynchronous callback, to be called when the read operation is complete.</param>
		/// <param name="state">A user-provided object that distinguishes this particular asynchronous read request from other requests.</param>
		/// <returns>An object that represents the asynchronous read operation, which could still be pending.</returns>
		[HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			if (_stream == null)
				throw new ObjectDisposedException("The read operation cannot be performed because the stream is closed.");
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));
			if (_mode != CompressionMode.Decompress)
				throw new InvalidOperationException("The CompressionMode value was Compress when the object was created.");
			if (!_stream.CanRead)
				throw new InvalidOperationException("The underlying stream does not support reading.");
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset), "offset or count is less than zero.");
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "offset or count is less than zero.");
			if (buffer.Length - offset < count)
				throw new ArgumentOutOfRangeException(nameof(count), "array length minus the index starting point is less than count.");

			if (Interlocked.CompareExchange(ref _activeAsyncOperation, 1, 0) != 0)
				throw new InvalidOperationException("Another asyncronious operation is active.");

			Func<int> action = () => Read(buffer, offset, count);
			BzAsyncResult asyncResult = new BzAsyncResult(action, callback, CallbackMethod, state);
			if (asyncResult.CompletedSynchronously)
				_activeAsyncOperation = 0;
			return asyncResult;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buffer">The buffer containing data to write to the current stream.</param>
		/// <param name="offset">The byte offset in array at which to begin writing.</param>
		/// <param name="count">The maximum number of bytes to write.</param>
		/// <param name="callback">An optional asynchronous callback to be called when the write operation is complete.</param>
		/// <param name="state">A user-provided object that distinguishes this particular asynchronous write request from other requests.</param>
		/// <returns>An object that represents the asynchronous write operation, which could still be pending.</returns>
		[HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			if (_stream == null)
				throw new ObjectDisposedException("The write operation cannot be performed because the stream is closed.");
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));
			if (_mode != CompressionMode.Compress)
				throw new InvalidOperationException("The CompressionMode value was Decompress when the object was created.");
			if (!_stream.CanWrite)
				throw new InvalidOperationException("The underlying stream does not support writing.");
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset), "offset or count is less than zero.");
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "offset or count is less than zero.");
			if (buffer.Length - offset < count)
				throw new ArgumentOutOfRangeException(nameof(count), "array length minus the index starting point is less than count.");

			if (Interlocked.CompareExchange(ref _activeAsyncOperation, 1, 0) != 0)
				throw new InvalidOperationException("Another asyncronious operation is active.");

		    Func<int> action = () =>
			{
				Write(buffer, offset, count);
				return 0;
			};
			BzAsyncResult asyncResult = new BzAsyncResult(action, callback, CallbackMethod, state);
			if (asyncResult.CompletedSynchronously)
				_activeAsyncOperation = 0;
			return asyncResult;
		}

		/// <summary>
		/// Releases the unmanaged resources used by the BZip2Stream and optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		protected override void Dispose(bool disposing)
		{
			if (_initialized)
			{
			    try
			    {
			        switch (_mode)
			        {
			            case CompressionMode.Compress:
			                FinalizeCompression();
			                break;
			            case CompressionMode.Decompress:
			                CheckErrorCode(BZ2_bzDecompressEnd(_dataAddr));
			                break;
			        }
			    }
			    finally
			    {
			        _initialized = false;
			        _dataHandle.Free();
			        try
			        {
			            if (!_leaveOpen && disposing)
			                _stream.Dispose();
			        }
			        finally
			        {
			            _stream = null;
			        }
			    }
			}
		}

		private void CallbackMethod(IAsyncResult result)
		{
			_activeAsyncOperation = 0;
		}

		/// <summary>
		/// Waits for the pending asynchronous read to complete.
		/// </summary>
		/// <param name="asyncResult">The reference to the pending asynchronous request to finish.</param>
		/// <returns>The number of bytes read from the stream, between 0 (zero) and the number of bytes you requested. GZipStream returns 0 only at the end of the stream; otherwise, it blocks until at least one byte is available.</returns>
		public override int EndRead(IAsyncResult asyncResult)
		{
			if (asyncResult == null)
				throw new ArgumentNullException(nameof(asyncResult), "asyncResult is null.");

		    BzAsyncResult result = asyncResult as BzAsyncResult;
		    if (result == null)
				throw new ArgumentException("asyncResult did not originate from a BeginRead method on the current stream.", nameof(asyncResult));

		    // InvalidOperationException The end operation cannot be performed because the stream is closed.
			return result.EndInvoke();
		}

		/// <summary>
		/// Handles the end of an asynchronous write operation.
		/// </summary>
		/// <param name="asyncResult">The object that represents the asynchronous call.</param>
		public override void EndWrite(IAsyncResult asyncResult)
		{
			if (asyncResult == null)
				throw new ArgumentNullException(nameof(asyncResult), "asyncResult is null.");

			BzAsyncResult result = asyncResult as BzAsyncResult;
			if (result == null)
				throw new ArgumentException("asyncResult did not originate from a BeginWrite method on the current stream.", nameof(asyncResult));

			// InvalidOperationException The underlying stream is null. -or- The underlying stream is closed.
			result.EndInvoke();
		}

		/// <summary>
		/// Flushes the contents of the internal buffer of the current BZip2Stream object to the underlying stream.
		/// </summary>
		public override void Flush()
		{
			if (_stream == null)
				throw new ObjectDisposedException("The read operation cannot be performed because the stream is closed.");

		    if (_mode == CompressionMode.Compress && _bufferOffset != 0)
			{
				_stream.Write(_buffer, 0, _bufferOffset);
				_bufferOffset = 0;
			}
		}

		/// <summary>
		/// Reads a number of decompressed bytes into the specified byte array.
		/// </summary>
		/// <param name="buffer">The array used to store decompressed bytes.</param>
		/// <param name="offset">The byte offset in array at which the read bytes will be placed.</param>
		/// <param name="count">The maximum number of decompressed bytes to read.</param>
		/// <returns>The number of bytes that were decompressed into the byte array. If the end of the stream has been reached, zero or the number of bytes read is returned.</returns>
		public override unsafe int Read(byte[] buffer, int offset, int count)
		{
			if (_stream == null)
				throw new ObjectDisposedException("The read operation cannot be performed because the stream is closed.");
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));
			if (_mode != CompressionMode.Decompress)
				throw new InvalidOperationException("The CompressionMode value was Compress when the object was created.");
			if (!_stream.CanRead)
				throw new InvalidOperationException("The underlying stream does not support reading.");
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset), "offset or count is less than zero.");
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "offset or count is less than zero.");
			if (buffer.Length - offset < count)
				throw new ArgumentOutOfRangeException(nameof(count), "array length minus the index starting point is less than count.");

			if (count == 0)
				return 0;

			_data.avail_in = _bufferLength;
			_data.avail_out = count;

			fixed (byte* input = &_buffer[0], output = &buffer[offset])
			{
				_data.next_in = input + _bufferOffset;
				_data.next_out = output;

				while (_data.avail_out > 0)
				{
					if (_data.avail_in == 0)
					{
						_data.avail_in = _stream.Read(_buffer, 0, BufferSize);
						_data.next_in = input;
					}

					if (!_initialized)
					{
						if (_data.avail_in == 0) // eof
							break;
						CheckErrorCode(BZ2_bzDecompressInit(_dataAddr, 0, 0));
						_initialized = true;
					}

					bool endOfStream = CheckErrorCode(BZ2_bzDecompress(_dataAddr)) == BzErrorCode.BZ_STREAM_END;
					if (endOfStream)
					{
						CheckErrorCode(BZ2_bzDecompressEnd(_dataAddr));
						_initialized = false;
					}
				}

				_bufferOffset = (int)(_data.next_in - input);
				_bufferLength = _data.avail_in;
			}

			return count - _data.avail_out;
		}

		/// <summary>
		/// This property is not supported and always throws a NotSupportedException.
		/// </summary>
		/// <param name="offset">The location in the stream.</param>
		/// <param name="origin">One of the SeekOrigin values.</param>
		/// <returns>A long value.</returns>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	    /// <summary>
		/// This property is not supported and always throws a NotSupportedException.
		/// </summary>
		/// <param name="value">The length of the stream.</param>
		public override void SetLength(long value) => throw new NotSupportedException();

	    /// <summary>
		/// Writes compressed bytes to the underlying stream from the specified byte array.
		/// </summary>
		/// <param name="buffer">The buffer that contains the data to compress.</param>
		/// <param name="offset">The byte offset in array from which the bytes will be read.</param>
		/// <param name="count">The maximum number of bytes to write.</param>
		public override unsafe void Write(byte[] buffer, int offset, int count)
		{
			if (_stream == null)
				throw new ObjectDisposedException("The write operation cannot be performed because the stream is closed.");
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));
			if (_mode != CompressionMode.Compress)
				throw new InvalidOperationException("The CompressionMode value was Decompress when the object was created.");
			if (!_stream.CanWrite)
				throw new InvalidOperationException("The underlying stream does not support writing.");
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset), "offset or count is less than zero.");
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "offset or count is less than zero.");
			if (buffer.Length - offset < count)
				throw new ArgumentOutOfRangeException(nameof(count), "array length minus the index starting point is less than count.");

			if (count == 0)
				return;

			_data.avail_in = count;
			_data.avail_out = BufferSize - _bufferOffset;

			fixed (byte* output = &_buffer[0], input = &buffer[offset])
			{
				_data.next_in = input;
				_data.next_out = output + _bufferOffset;

				if (!_initialized)
				{
					CheckErrorCode(BZ2_bzCompressInit(_dataAddr, _level, 0, 0));
					_initialized = true;
				}

				while (_data.avail_in > 0)
				{
					if (_data.avail_out == 0)
					{
						_stream.Write(_buffer, 0, BufferSize);
						_bufferOffset = 0;
						_data.avail_out = BufferSize;
						_data.next_out = output;
					}

					CheckErrorCode(BZ2_bzCompress(_dataAddr, BzAction.BZ_RUN));
				}

				_bufferOffset = BufferSize - _data.avail_out;
			}
		}

		private unsafe void FinalizeCompression()
		{
			_data.avail_in = 0;
			_data.avail_out = BufferSize - _bufferOffset;
			_data.next_in = null;

			try
			{
				fixed (byte* output = &_buffer[0])
				{
					_data.next_out = output + _bufferOffset;

					if (!_initialized)
					{
						CheckErrorCode(BZ2_bzCompressInit(_dataAddr, _level, 0, 0));
						_initialized = true;
					}
					BzErrorCode errorCode = BzErrorCode.BZ_FINISH_OK;
					while (errorCode != BzErrorCode.BZ_STREAM_END)
					{
						if (_data.avail_out == 0)
						{
							_stream.Write(_buffer, 0, BufferSize);
							_bufferOffset = 0;
							_data.avail_out = BufferSize;
							_data.next_out = output;
						}
						errorCode = BZ2_bzCompress(_dataAddr, BzAction.BZ_FINISH);
						CheckErrorCode(errorCode);
					}
					_bufferOffset = BufferSize - _data.avail_out;
					if (_bufferOffset > 0)
					{
						_stream.Write(_buffer, 0, _bufferOffset);
						_bufferOffset = 0;
						_data.avail_out = BufferSize;
						_data.next_out = output;
					}
				}
			}
			finally
			{
				CheckErrorCode(BZ2_bzCompressEnd(_dataAddr));
			}
		}
	}
}