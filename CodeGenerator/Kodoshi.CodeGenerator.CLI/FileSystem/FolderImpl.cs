using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator.Core.FileSystem;

public sealed class FolderImpl : IFolder
{
    private readonly string _basePath;

    public FolderImpl(string basePath)
    {
        _basePath = basePath;
    }

    public ValueTask<IFolder> OpenFolder(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        if (!Directory.Exists(realPath))
        {
            throw new FileNotFoundException("Directory not found", realPath);
        }
        return new ValueTask<IFolder>(new FolderImpl(realPath));
    }

    public ValueTask<IFolder> CreateFolder(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        if (Directory.Exists(realPath))
        {
            throw new FileNotFoundException(
                "Directory already exists", realPath);
        }
        Directory.CreateDirectory(realPath);
        return new ValueTask<IFolder>(new FolderImpl(realPath));
    }

    public ValueTask<bool> Delete(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        if (!Directory.Exists(realPath))
        {
            return new ValueTask<bool>(false);
        }
        Directory.Delete(realPath, true);
        return new ValueTask<bool>(true);
    }

    public ValueTask<bool> Exists(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        return new ValueTask<bool>(Directory.Exists(realPath));
    }

    public ValueTask<IFile> OpenFile(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        if (!File.Exists(realPath))
        {
            throw new FileNotFoundException("File doesn't exist", realPath);
        }
        return new ValueTask<IFile>(new FileImpl(realPath));
    }

    public ValueTask<IFile> CreateFile(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        if (File.Exists(realPath))
        {
            throw new FileNotFoundException("File already exist", realPath);
        }
        using var _ = File.Create(realPath);
        return new ValueTask<IFile>(new FileImpl(realPath));
    }
}
