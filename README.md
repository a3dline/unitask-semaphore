# UniTask Semaphore

A simple semaphore implementation for UniTask.

How to user
```C#

async UniTask SomeMethod()
{
    var semaphore = new UniTaskSemaphore(1);
    // Code before acquiring the semaphore will run each time
    
    using var _ = await semaphore.Acquire(CancellationToken.None);
    // Code after acquiring the semaphore will run one by one
}
```

# Example

`Assets/Example/PlayerQueue.cs` contains an example of how to use `UniTaskSemaphore` to control object transfers in a queue.
```C#
new UniTaskSemaphore(3);
```
![example.gif](example.gif)