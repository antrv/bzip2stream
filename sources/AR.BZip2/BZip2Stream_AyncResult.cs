using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace System.IO.Compression
{
	partial class BZip2Stream
	{
		private class BzAsyncResult: IAsyncResult
		{
			private readonly Func<int> _action;
			private readonly AsyncCallback _callback;
			private readonly AsyncCallback _streamCallback;
			private readonly IAsyncResult _result;

			public BzAsyncResult(Func<int> action, AsyncCallback callback, AsyncCallback streamCallback, object state)
			{
				_action = action;
				_callback = callback;
				_streamCallback = streamCallback;
				_result = action.BeginInvoke(CallbackMethod, state);
			}

			private void CallbackMethod(object state)
			{
				_streamCallback(this);
				if (_callback != null)
					_callback(this);
			}

			public bool IsCompleted
			{
				get
				{
					return _result.IsCompleted;
				}
			}

			public WaitHandle AsyncWaitHandle
			{
				get
				{
					return _result.AsyncWaitHandle;
				}
			}

			public object AsyncState
			{
				get
				{
					return _result.AsyncState;
				}
			}

			public bool CompletedSynchronously
			{
				get
				{
					return _result.CompletedSynchronously;
				}
			}

			public int EndInvoke()
			{
				return _action.EndInvoke(_result);
			}
		}
	}
}
