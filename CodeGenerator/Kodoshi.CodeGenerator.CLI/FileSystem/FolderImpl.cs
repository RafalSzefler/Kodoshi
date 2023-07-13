using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator.Core.FileSystem;

public sealed class FolderImpl : IFolder
{
    private static FolderImpl _openOrCreateFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        var basePath = Path.GetDirectoryName(path);
        FolderImpl? baseFolder = null;
        if (!string.IsNullOrEmpty(basePath))
        {
            baseFolder = _openOrCreateFolder(basePath);
        }
        return new FolderImpl(baseFolder, path);
    }

    public static ValueTask<FolderImpl> OpenOrCreateFolder(string path, CancellationToken ct)
        => new ValueTask<FolderImpl>(_openOrCreateFolder(path));

    private readonly string _basePath;

    public string Name => Path.GetFileName(_basePath);
    public string FullName => _basePath;
    public IFolder? ParentFolder { get; }

    public FolderImpl(IFolder? parentFolder, string basePath)
    {
        _basePath = Path.GetFullPath(basePath);
        ParentFolder = parentFolder;
    }

    public ValueTask<IFolder> OpenFolder(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        if (!Directory.Exists(realPath))
        {
            throw new FileNotFoundException("Directory not found", realPath);
        }
        return new ValueTask<IFolder>(new FolderImpl(this, realPath));
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
        return new ValueTask<IFolder>(new FolderImpl(this, realPath));
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
        return new ValueTask<IFile>(new FileImpl(this, realPath));
    }

    public ValueTask<IFile> CreateFile(string path, CancellationToken ct)
    {
        var realPath = Path.Join(_basePath, path);
        if (File.Exists(realPath))
        {
            throw new FileNotFoundException("File already exist", realPath);
        }
        using var _ = File.Create(realPath);
        return new ValueTask<IFile>(new FileImpl(this, realPath));
    }

    public ValueTask<IReadOnlyList<IFolder>> ListFolders(CancellationToken ct)
    {
        var result = new List<IFolder>();
        foreach (var directory in Directory.EnumerateDirectories(_basePath))
        {
            var directoryName = Path.GetFileName(directory);
            result.Add(new FolderImpl(this, Path.Join(_basePath, directoryName)));
        }
        return ValueTask.FromResult(result as IReadOnlyList<IFolder>);
    }

    public ValueTask<IReadOnlyList<IFile>> ListFiles(CancellationToken ct)
    {
        var result = new List<IFile>();
        foreach (var file in Directory.EnumerateFiles(_basePath))
        {
            var fileName = Path.GetFileName(file);
            result.Add(new FileImpl(this, Path.Join(_basePath, fileName)));
        }
        return ValueTask.FromResult(result as IReadOnlyList<IFile>);
    }
}
