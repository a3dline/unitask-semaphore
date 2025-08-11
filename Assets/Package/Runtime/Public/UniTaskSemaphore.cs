using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace AUniTaskSemaphore
{
    [DebuggerDisplay("Semaphore {_currentSemaphoreIndex}: {_currentCount}/{_maxCount} ({_queue.Count})")]
    public class UniTaskSemaphore
    {
        private static int _debugIndex = -1;
        private readonly int _currentSemaphoreIndex;
        private readonly int _maxCount;
        private readonly Queue<SemaphorePromise> _queue;
        private int _currentCount;
        private bool _wasReleased;

        public UniTaskSemaphore(int count)
        {
            _debugIndex++;
            _currentSemaphoreIndex = _debugIndex;

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be more then 0.");
            }

            _currentCount = count;
            _maxCount = count;
            _queue = new Queue<SemaphorePromise>(count);
        }

        public bool IsEmpty => _currentCount == _maxCount;

        internal void Release()
        {
            while (_queue.Count > 0)
            {
                var ts = _queue.Dequeue();
                if (ts.TryRelease())
                {
                    return;
                }
            }

            if (_currentCount >= _maxCount)
            {
                throw new InvalidOperationException("Cannot release more than the maximum count.");
            }

            _currentCount++;
        }

        public UniTask<UniTaskSemaphoreToken> Acquire(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return UniTask.FromCanceled<UniTaskSemaphoreToken>(token);
            }

            if (_currentCount > 0)
            {
                _currentCount--;
                return UniTask.FromResult(new UniTaskSemaphoreToken(this));
            }

            var promise = SemaphorePromise.Create(this, token, out var version);
            _queue.Enqueue(promise);

            return new UniTask<UniTaskSemaphoreToken>(promise, version);
        }

        public override string ToString()
        {
            return $"Semaphore {_currentSemaphoreIndex}: {_currentCount}/{_maxCount} ({_queue.Count})";
        }

        private sealed class SemaphorePromise : IUniTaskSource<UniTaskSemaphoreToken>
        {
            private CancellationToken _cancellationToken;
            private UniTaskCompletionSourceCore<UniTaskSemaphoreToken> _core;
            private CancellationTokenRegistration _registration;
            private UniTaskSemaphore _semaphore;

            [Preserve]
            private SemaphorePromise() { }

            public UniTaskStatus GetStatus(short token)
            {
                return _core.GetStatus(token);
            }

            UniTaskSemaphoreToken IUniTaskSource<UniTaskSemaphoreToken>.GetResult(short token)
            {
                try
                {
                    return _core.GetResult(token);
                }
                finally
                {
                    TaskTracker.RemoveTracking(this);
                    _registration.Dispose();
                }
            }

            public UniTaskStatus UnsafeGetStatus()
            {
                return _core.UnsafeGetStatus();
            }

            public void OnCompleted(Action<object> continuation,
                                    object state,
                                    short token)
            {
                _core.OnCompleted(continuation, state, token);
            }

            public void GetResult(short token)
            {
                try
                {
                    _core.GetResult(token);
                }
                finally
                {
                    TaskTracker.RemoveTracking(this);
                    _registration.Dispose();
                }
            }

            public static SemaphorePromise Create(UniTaskSemaphore semaphore,
                                                   CancellationToken cancellationToken,
                                                   out short token)
            {
                var result = new SemaphorePromise();
                result._cancellationToken = cancellationToken;
                result._semaphore = semaphore;

                if (cancellationToken.CanBeCanceled)
                {
                    result._registration = cancellationToken.Register(
                                                                      static state =>
                                                                      {
                                                                          var promise = (SemaphorePromise)state;
                                                                          promise._core.TrySetCanceled(promise._cancellationToken);
                                                                      },
                                                                      result,
                                                                      true);
                }

                token = result._core.Version;
                TaskTracker.TrackActiveTask(result, 3);
                return result;
            }

            public bool TryRelease()
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                return _core.TrySetResult(new UniTaskSemaphoreToken(_semaphore));
            }
        }
    }
}