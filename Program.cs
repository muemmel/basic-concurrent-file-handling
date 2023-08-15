// See https://aka.ms/new-console-template for more information

using ConsoleApp1;

// WriteTestFile(1_000_000_000);

await using var f = new SourceFileHandler("one_gigabyte");
f.StartReading();
var chunks = await f.WaitForCompletion();

await using var t = new TargetFileHandler("target_file", chunks);
t.StartWriting();
await t.WaitForCompletion();

Console.WriteLine();


static void WriteTestFile(long size)
{
    var a = (int) 'A';
    var z = (int) 'z';
    
    var bytes = new byte[size];
    
    for (var i = 0; i < bytes.Length; i++)
    {
        bytes[i] = (byte) Random.Shared.Next(a, z);
    }

    Console.WriteLine(Directory.GetCurrentDirectory());
    File.WriteAllBytes("one_gigabyte", bytes);
}
