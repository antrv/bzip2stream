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
			    _callback?.Invoke(this);
			}

			public bool IsCompleted => _result.IsCompleted;
		    public WaitHandle AsyncWaitHandle => _result.AsyncWaitHandle;
		    public object AsyncState => _result.AsyncState;
		    public bool CompletedSynchronously => _result.CompletedSynchronously;
		    public int EndInvoke() => _action.EndInvoke(_result);
		}
	}
}