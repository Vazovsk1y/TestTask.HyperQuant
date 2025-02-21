namespace Connector;

internal class ReaderWriterLockWrapper : IDisposable
{
    public readonly struct WriteLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;
        
        public WriteLockToken(ReaderWriterLockSlim @lock)
        {
            _lock = @lock;
            @lock.EnterWriteLock();
        }
        
        public void Dispose() => _lock.ExitWriteLock();
    }

    public readonly struct ReadLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;
        
        public ReadLockToken(ReaderWriterLockSlim @lock)
        {
            _lock = @lock;
            @lock.EnterReadLock();
        }
        
        public void Dispose() => _lock.ExitReadLock();
    }

    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    
    public ReadLockToken EnterReadLock() => new(_lock);
    
    public WriteLockToken EnterWriteLock() => new(_lock);

    public void Dispose() => _lock.Dispose();
}