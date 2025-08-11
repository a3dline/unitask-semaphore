using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AUniTaskSemaphore;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class UniTaskSemaphoreStressTest
    {
        [Test]
        [Timeout(15000)]
        [TestCase(1, 23)]
        [TestCase(1, 77)]
        [TestCase(1, 16)]
        [TestCase(2, 18)]
        [TestCase(6, 66)]
        [TestCase(25, 25)]
        [TestCase(100, 54)]
        public async Task UniTaskSemaphoreStressTest_SemaphoreIsEmpty(int semaphoreLimit, int seed)
        {
            RunningTasksCount = 0;

            var random = new Random(seed);
            var semaphore = new UniTaskSemaphore(semaphoreLimit);
            var tokens = new List<CancellationTokenSource>
                         {
                             new(),
                             new(),
                             new(),
                             new(),
                             new()
                         };
            var sources = new Queue<UniTaskCompletionSource>();

            for (var i = 0; i < 100; i++)
            {
                var rand = random.Next(0, 3);
                switch (rand)
                {
                    case 0:
                        WaitForSemaphore(semaphore, null, default).Forget();
                        break;
                    case 1:
                        var randomTokenSource = tokens[random.Next(0, tokens.Count)];
                        WaitForSemaphore(semaphore, null, randomTokenSource.Token).Forget();
                        break;
                    case 2:
                        var ts = new UniTaskCompletionSource();
                        sources.Enqueue(ts);
                        WaitForSemaphore(semaphore, ts, default).Forget();
                        break;
                }
            }

            while (tokens.Count > 0 || sources.Count > 0)
            {
                var completeTask = random.Next(0, 3) == 0 && sources.Count > 0;
                var cancelToken = random.Next(0, 3) == 0 && tokens.Count > 0;

                if (completeTask)
                {
                    sources.Dequeue().TrySetResult();
                }

                if (cancelToken)
                {
                    var randomTokenSource = tokens[random.Next(0, tokens.Count)];
                    randomTokenSource.Cancel();
                    tokens.Remove(randomTokenSource);
                }

                Assert.That(RunningTasksCount, Is.LessThanOrEqualTo(semaphoreLimit), "Running tasks count should be less than or equal to semaphore limit.");
                await UniTask.Delay(1);
            }

            // passing the last semaphore that will complete only when all test tasks are completed
            var semaphoreToken = await semaphore.Acquire(default);
            semaphoreToken.Dispose();

            Assert.That(semaphore.IsEmpty, Is.True, "Semaphore should be empty after all tasks are completed or cancelled.");
        }

        private static int RunningTasksCount;

        private static async UniTaskVoid WaitForSemaphore(UniTaskSemaphore semaphore,
                                                          UniTaskCompletionSource source,
                                                          CancellationToken token)
        {
            using var _ = await semaphore.Acquire(token);
            RunningTasksCount++;
            if (source is not null)
            {
                await source.Task;
            }
            else
            {
                await UniTask.Delay(1, cancellationToken: token).SuppressCancellationThrow();
            }

            RunningTasksCount--;
        }
    }
}