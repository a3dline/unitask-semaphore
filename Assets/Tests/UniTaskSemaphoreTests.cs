using System;
using System.Threading;
using System.Threading.Tasks;
using AUniTaskSemaphore;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    [Timeout(1000)]
    public class UniTaskSemaphoreTests
    {
        [Test]
        public void Test_ThrowsOnInvalidInitialCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UniTaskSemaphore(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UniTaskSemaphore(0));
        }

        [Test]
        public async Task Test_IsEmpty()
        {
            var semaphore = new UniTaskSemaphore(1);
            Assert.That(semaphore.IsEmpty, Is.True);
            var semaphoreToken = await semaphore.Acquire(CancellationToken.None);
            Assert.That(semaphore.IsEmpty, Is.False);
            semaphoreToken.Dispose();
            Assert.That(semaphore.IsEmpty, Is.True);
        }

        // Base flow
        [Test(Description = "Acquire semaphore (limit1), task complete")]
        public void Acquire1From1_Complete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete = false;

            Do(semaphore, null, () => complete = true, default).Forget();

            Assert.That(complete, Is.True);
        }

        [Test(Description = "Acquire semaphore (limit2), task complete")]
        public void Acquire1From2_Complete()
        {
            var semaphore = new UniTaskSemaphore(2);
            var complete = false;

            Do(semaphore, null, () => complete = true, default).Forget();

            Assert.That(complete, Is.True);
        }

        [Test(Description = "Acquire semaphore (limit1), task complete, acquire semaphore (limit1) again, task complete")]
        public void Acquire1From1_Complete_Acquire1From1_Complete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;

            Do(semaphore, null, () => complete1 = true, default).Forget();

            Assert.That(complete1, Is.True);

            Do(semaphore, null, () => complete2 = true, default).Forget();

            Assert.That(complete2, Is.True);
        }

        [Test(Description = "Acquire semaphore 2 times (limit1), 2nd wait, complete 1st, 2nd start and complete")]
        public void Acquire2From1_Complete1st_2ndStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;

            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, null, () => complete2 = true, default).Forget();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.True);
        }

        [Test(Description = "Acquire semaphore 3 times (limit2), 3rd wait, complete 1st, 3rd start and complete")]
        public void Acquire3From2_Complete1st_3rdStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(2);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;

            var ts1 = new UniTaskCompletionSource();
            var ts2 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, ts2, () => complete2 = true, default).Forget();
            Do(semaphore, null, () => complete3 = true, default).Forget();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.True);

            ts2.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.True);
            Assert.That(complete3, Is.True);
        }

        [Test(Description = "Acquire semaphore 3 times (limit1), 2nd and 3rd wait, complete 1st, 2nd start, complete 2nd, 3rd start and complete")]
        public void Acquire3From1_Complete1st_2ndStart_Complete2nd_3rdStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;

            var ts1 = new UniTaskCompletionSource();
            var ts2 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, ts2, () => complete2 = true, default).Forget();
            Do(semaphore, null, () => complete3 = true, default).Forget();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);

            ts2.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.True);
            Assert.That(complete3, Is.True);
        }

        // Cancellation flow

        [Test(Description = "Acquire semaphore with canceled token (limit1), acquire semaphore (limit1) again, task complete")]
        public void Acquire1From1_CancelToken_Acquire1From1_Complete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Do(semaphore, null, () => complete1 = true, cts.Token).Forget();

            Assert.That(complete1, Is.False);

            Do(semaphore, null, () => complete2 = true, default).Forget();

            Assert.That(complete2, Is.True);
        }

        [Test(Description = "Acquire semaphore (limit1), cancel token, acquire semaphore (limit1) again, task complete")]
        public void Acquire1From1_CancelToken_Acquire1From1_Complete2()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;

            using var cts = new CancellationTokenSource();
            var ts1 = new UniTaskCompletionSource();
            Do(semaphore, ts1, () => complete1 = true, cts.Token).Forget();
            cts.Cancel();

            Assert.That(complete1, Is.False);

            Do(semaphore, null, () => complete2 = true, default).Forget();
            Assert.That(complete2, Is.True);
        }

        [Test(Description = "Acquire semaphore 2 times (limit2), cancel token for 1st task, complete 2nd task, acquire semaphore (limit1) again, task complete")]
        public void Acquire2From2_CancelToken_Complete2nd_Acquire1From1_Complete()
        {
            var semaphore = new UniTaskSemaphore(2);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;

            using var cts = new CancellationTokenSource();
            var ts1 = new UniTaskCompletionSource();
            var ts2 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, cts.Token).Forget();
            Do(semaphore, ts2, () => complete2 = true, default).Forget();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);

            cts.Cancel();
            ts2.TrySetResult();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.True);

            Do(semaphore, null, () => complete3 = true, default).Forget();

            Assert.That(complete3, Is.True);
        }

        [Test(Description = "Acquire semaphore 3 times (limit1), cancel token for 2nd task, 3rd task still wait, complete 1st task, 3rd task start and complete")]
        public void Acquire3From1_Cancel2nd_Complete1st_3rdStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;

            using var cts = new CancellationTokenSource();
            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, null, () => complete2 = true, cts.Token).Forget();
            Do(semaphore, null, () => complete3 = true, default).Forget();

            cts.Cancel();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.True);
        }

        [Test(Description = "Acquire semaphore 3 times (limit1), cancel token for 3rd task, 2nd task still wait, complete 1st task, 2nd task start and complete")]
        public void Acquire3From1_Cancel3rd_Complete2st_2ndStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;

            using var cts = new CancellationTokenSource();
            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, null, () => complete2 = true, default).Forget();
            Do(semaphore, null, () => complete3 = true, cts.Token).Forget();

            cts.Cancel();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.True);
            Assert.That(complete3, Is.False);
        }

        [Test(Description =
                     "Acquire semaphore 4 times (limit1), cancel token for 2nd and 3rd tasks, 4th task still wait, complete 1st task, 4th task start and complete")]
        public void Acquire4From1_Cancel2ndAnd3rd_Complete1st_4thStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;
            var complete4 = false;

            using var cts = new CancellationTokenSource();
            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, null, () => complete2 = true, cts.Token).Forget();
            Do(semaphore, null, () => complete3 = true, cts.Token).Forget();
            Do(semaphore, null, () => complete4 = true, default).Forget();

            cts.Cancel();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);
            Assert.That(complete4, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);
            Assert.That(complete4, Is.True);
        }

        [Test(Description =
                     "Acquire semaphore 4 times (limit1), cancel token for 2nd and 4th tasks, 3rd task still wait, complete 1st task, 3rd task start and complete")]
        public void Acquire4From1_Cancel2ndAnd4th_Complete1st_3rdStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;
            var complete4 = false;

            using var cts = new CancellationTokenSource();
            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, null, () => complete2 = true, cts.Token).Forget();
            Do(semaphore, null, () => complete3 = true, default).Forget();
            Do(semaphore, null, () => complete4 = true, cts.Token).Forget();

            cts.Cancel();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);
            Assert.That(complete4, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.True);
            Assert.That(complete4, Is.False);
        }

        [Test(Description =
                     "Acquire semaphore 4 times (limit1), cancel token for 4th and 3rd tasks, 2nd task still wait, complete 1st task, 2nd task start and complete")]
        public void Acquire4From1_Cancel4thAnd3rd_Complete1st_2ndStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;
            var complete4 = false;

            using var cts = new CancellationTokenSource();
            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, null, () => complete2 = true, default).Forget();
            Do(semaphore, null, () => complete3 = true, cts.Token).Forget();
            Do(semaphore, null, () => complete4 = true, cts.Token).Forget();

            cts.Cancel();

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.False);
            Assert.That(complete3, Is.False);
            Assert.That(complete4, Is.False);

            ts1.TrySetResult();

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.True);
            Assert.That(complete3, Is.False);
            Assert.That(complete4, Is.False);
        }

        // Exception flow

        [Test(Description = "Acquire semaphore (limit1), throw exception in task, acquire semaphore (limit1) again, task complete")]
        public void Acquire1From1_ThrowException_Acquire1From1_Complete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;

            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();

            ts1.TrySetException(new Exception());
            Assert.That(complete1, Is.False);

            Do(semaphore, null, () => complete2 = true, default).Forget();

            Assert.That(complete2, Is.True);
        }

        [Test(Description = "Acquire semaphore 2 times (limit1), throw exception in 1st task, 2nd task start and complete")]
        public void Acquire2From1_ThrowException_2ndStartAndComplete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;

            var ts1 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, null, () => complete2 = true, default).Forget();

            ts1.TrySetException(new Exception());

            Assert.That(complete1, Is.False);
            Assert.That(complete2, Is.True);
        }

        [Test(Description =
                     "Acquire semaphore 2 times (limit1), complete 1st task, throw exception in 2nd task, acquire semaphore (limit1) again, task complete")]
        public void Acquire2From1_Complete1st_ThrowException_Acquire1From1_Complete()
        {
            var semaphore = new UniTaskSemaphore(1);
            var complete1 = false;
            var complete2 = false;
            var complete3 = false;

            var ts1 = new UniTaskCompletionSource();
            var ts2 = new UniTaskCompletionSource();

            Do(semaphore, ts1, () => complete1 = true, default).Forget();
            Do(semaphore, ts2, () => complete2 = true, default).Forget();

            ts1.TrySetResult();
            ts2.TrySetException(new Exception());

            Assert.That(complete1, Is.True);
            Assert.That(complete2, Is.False);

            Do(semaphore, null, () => complete3 = true, default).Forget();

            Assert.That(complete3, Is.True);
        }

        private static async UniTask Do(UniTaskSemaphore semaphore,
                                        [CanBeNull] UniTaskCompletionSource source,
                                        Action completeCallback,
                                        CancellationToken token)
        {
            using var _ = await semaphore.Acquire(token);
            if (source is not null)
            {
                try
                {
                    await source.Task.AttachExternalCancellation(token);
                    completeCallback.Invoke();
                }
                catch (Exception)
                {
                    // Ignore propagation
                }
            }
            else
            {
                completeCallback.Invoke();
            }
        }
    }
}