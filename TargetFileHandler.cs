namespace ConsoleApp1;

public class TargetFileHandler : IDisposable, IAsyncDisposable
{
    private readonly FileInfo fileInfo;
    private readonly int      bufferSize = 10_000;
    private readonly long     fileSize;
    private readonly long     numChunks;

    private readonly FileChunk[] chunksToWrite;

    private readonly FileStream        mainTargetStream;
    private readonly FileStream[]      subStreams;
    private readonly Task<FileChunk>[] writeTasks;

    public TargetFileHandler(
        string targetFile,
        FileChunk[] chunksToWrite)
    {
        fileInfo = new FileInfo(targetFile);
        this.chunksToWrite = chunksToWrite;
        numChunks = this.chunksToWrite.LongLength;
        writeTasks = new Task<FileChunk>[numChunks];
        subStreams = new FileStream[numChunks];
        fileSize = bufferSize * numChunks;
        mainTargetStream = new FileStream(
            fileInfo.FullName,
            new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.Create,
                Options = FileOptions.Asynchronous,
                PreallocationSize = fileSize
            });
    }

    public void StartWriting(CancellationToken cancellationToken = default)
    {
        mainTargetStream.SetLength(fileSize);
        var numWrites = numChunks;
        for (int i = 0; i < numWrites; i++)
        {
            var index = i;
            var chunk = chunksToWrite[i];
            writeTasks[i] = Task.Run(
                async () =>
                {
                    var subStream = new FileStream(
                        fileInfo.FullName,
                        new FileStreamOptions
                        {
                            Access = FileAccess.Write,
                            Mode = FileMode.Open,
                            Options = FileOptions.Asynchronous,
                        });

                    subStreams[index] = subStream;
                    subStream.Lock(chunk.Offset, chunk.Length);
                    try
                    {
                        subStream.Seek(chunk.Offset, SeekOrigin.Begin);
                        await subStream.WriteAsync(
                            chunk.Data,
                            cancellationToken);
                    }
                    finally
                    {
                        subStream.Unlock(chunk.Offset, chunk.Length);
                    }

                    return chunk;
                },
                cancellationToken);
        }
    }

    public async Task<FileInfo> WaitForCompletion(CancellationToken cancellationToken = default)
    {
        var tasks = writeTasks.ToList();
        while (!cancellationToken.IsCancellationRequested
               && tasks.Count > 0)
        {
            var next = await Task.WhenAny(tasks);
            tasks.Remove(next);
            var chunk = await next;
            Console.WriteLine(
                $"Wrote Chunk {chunk.Index} ({chunk.Offset} to {chunk.Length})");
        }

        foreach (var fileStream in subStreams)
        {
            await fileStream.FlushAsync(cancellationToken);
        }

        await mainTargetStream.FlushAsync(cancellationToken);

        return fileInfo;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        mainTargetStream.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await mainTargetStream.DisposeAsync();
    }
}