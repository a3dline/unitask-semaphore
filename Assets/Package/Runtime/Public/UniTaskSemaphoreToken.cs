using System;

namespace AUniTaskSemaphore
{
    public struct UniTaskSemaphoreToken : IDisposable
    {
        private UniTaskSemaphore _semaphore;

        public UniTaskSemaphoreToken(UniTaskSemaphore semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore?.Release();
            _semaphore = null;
        }
    }
}