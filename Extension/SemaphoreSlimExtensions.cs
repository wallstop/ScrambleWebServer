namespace ScrambleWebServer.Extension
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class SemaphoreSlimExtensions
    {
        public static async Task<IDisposable> WaitAsyncWithAutoRelease(this SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            return new SemaphoreSlimAutoReleaser(semaphore);
        }

        private sealed class SemaphoreSlimAutoReleaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            public SemaphoreSlimAutoReleaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
            }

            public void Dispose()
            {
                _ = _semaphore.Release();
            }
        }
    }
}
