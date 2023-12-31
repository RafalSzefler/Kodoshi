using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator.Core.FileSystem;

public sealed class FileImpl : IFile
{
    public static async ValueTask<FileImpl> OpenFile(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File doesn't exist", path);
        }
        var basePath = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(basePath))
        {
            throw new InvalidOperationException("Directory path is empty. Fail.");
        }
        var folder = await FolderImpl.OpenOrCreateFolder(basePath, ct);
        return new FileImpl(folder, path);
    }

    private readonly string _path;

    public string Name => Path.GetFileName(_path);
    public string FullName => _path;
    public IFolder ParentFolder { get; }

    public FileImpl(IFolder parent, string path)
    {
        _path = Path.GetFullPath(path);
        ParentFolder = parent;
    }

    public async ValueTask<ReadOnlyMemory<byte>> Read(CancellationToken ct)
    {
        var fi = new FileInfo(_path);
        if (!fi.Exists)
        {
            throw new FileNotFoundException("File doesn't exist", _path);
        }
        var length = fi.Length;
        if (length == 0)
        {
            return Array.Empty<byte>().AsMemory();
        }

        const int max = int.MaxValue - 1;
        if (length > max)
        {
            throw new ArgumentException(
                $"File too big. We can only handle files up to {max} size.");
        }

        var buffer = new byte[length];

        using (var fo = File.Open(_path, FileMode.Open, FileAccess.Read))
        {
            var offset = 0;
            var remaining = (int)length;
            while (remaining > 0)
            {
                var read = await fo.ReadAsync(buffer, offset, remaining, ct);
                offset += read;
                remaining -= read;
            }
        }

        return buffer;
    }

    public async ValueTask Write(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        var fi = new FileInfo(_path);
        if (!fi.Exists)
        {
            throw new FileNotFoundException("File doesn't exist", _path);
        }

        if (buffer.Length == 0)
        {
            return;
        }

        using (var fo = File.Open(_path, FileMode.Open, FileAccess.Write))
        {
            await fo.WriteAsync(buffer, ct);
        }
    }
}
