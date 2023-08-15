namespace ConsoleApp1;

public class SourceFileHandler : IDisposable, IAsyncDisposable
{
    private readonly FileInfo fileInfo;
    private readonly int      bufferSize = 10_000;
    private readonly long     numChunks;

    private readonly FileStream            mainSourceStream;
    private readonly FileStream[]          subStreams;
    private readonly FileChunk[]           sourceChunks;
    private readonly FileChunk[]           translatedChunks;
    private readonly Task<FileChunk>[]     readTasks;
    private readonly Task<FileChunk>[]     translationTasks;
    private readonly TranslationCollection translations = new();

    public SourceFileHandler(string sourceFile)
    {
        fileInfo = new FileInfo(sourceFile);
        numChunks = fileInfo.Length / bufferSize;
        sourceChunks = new FileChunk[numChunks];
        translatedChunks = new FileChunk[numChunks];
        subStreams = new FileStream[numChunks];
        readTasks = new Task<FileChunk>[numChunks];
        translationTasks = new Task<FileChunk>[numChunks];
        mainSourceStream = new FileStream(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            FileOptions.Asynchronous);
    }

    public void StartReading(CancellationToken cancellationToken = default)
    {
        var numReads = fileInfo.Length / bufferSize;
        Console.WriteLine($"Reading {numReads} chunks ");
        for (var i = 0; i < numReads; i++)
        {
            var index = i;
            var offset = i * bufferSize;
            readTasks[i] = Task.Run(async () =>
            {
                var subStream = new FileStream(
                    fileInfo.FullName,
                    new FileStreamOptions
                    {
                        Access = FileAccess.Read,
                        Mode = FileMode.Open,
                        Options = FileOptions.Asynchronous
                    });

                subStreams[index] = subStream;
                subStream.Seek(offset, SeekOrigin.Begin);

                Memory<byte> buf = new byte[bufferSize];
                var length = await subStream.ReadAsync(buf, cancellationToken);

                return new FileChunk(index, offset, length, buf);
            }, cancellationToken);
        }
    }

    public async Task<FileChunk[]> WaitForCompletion(
        CancellationToken cancellationToken = default)
    {
        var tasks = readTasks.ToList();
        var processTasks = new List<Task>();
        while (!cancellationToken.IsCancellationRequested
               && tasks.Count > 0)
        {
            var next = await Task.WhenAny(tasks);
            tasks.Remove(next);
            
            processTasks.Add(Task.Run(() => ProcessChunkBytes(next.Result), cancellationToken));
        }

        await Task.WhenAll(readTasks);
        await Task.WhenAll(processTasks);
        if (cancellationToken.IsCancellationRequested)
            return Array.Empty<FileChunk>();

        translations.Generate();
        StartTranslating(cancellationToken);

        tasks = translationTasks.ToList();
        while (!cancellationToken.IsCancellationRequested
               && tasks.Count > 0)
        {
            var next = await Task.WhenAny(tasks);
            tasks.Remove(next);
            
            translatedChunks[next.Result.Index] = next.Result;
        }

        var offset = 0L;
        for (var i = 0; i < translatedChunks.Length; i++)
        {
            var cur = translatedChunks[i];
            translatedChunks[i] = cur with
            {
                Offset = offset
            };

            offset += cur.Length;
        }
        
        Console.WriteLine("Processing complete");

        return translatedChunks;
    }

    private void ProcessChunkBytes(FileChunk chunk)
    {
        sourceChunks[chunk.Index] = chunk;
        var span = chunk.Data.Span;
        for (int i = 0; i < span.Length; i++)
        {
            var b = span[i];
            translations.ProcessByte(b);
        }
    }

    private void StartTranslating(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Reading {sourceChunks.Length} chunks ");
        for (var i = 0; i < sourceChunks.Length; i++)
        {
            var chunk = sourceChunks[i];
            translationTasks[i] = Task.Run(() =>
            {
                var span = chunk.Data.Span;
                var output = new List<byte>();
                for (int j = 0; j < span.Length; j++)
                {
                    var b = span[j];
                    output.AddRange(translations[b]);
                }

                return new FileChunk(
                    chunk.Index,
                    -1,
                    output.Count,
                    new Memory<byte>(output.ToArray()));
            }, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await mainSourceStream.DisposeAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        mainSourceStream.Dispose();
    }
}