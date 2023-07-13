using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.FileSystem;

public interface IFolder
{
    string FullName { get; }
    string Name { get; }
    IFolder? ParentFolder { get; }
    ValueTask<bool> Exists(string path, CancellationToken ct);
    ValueTask<IFolder> OpenFolder(string path, CancellationToken ct);
    ValueTask<IFolder> CreateFolder(string path, CancellationToken ct);
    ValueTask<IFile> OpenFile(string path, CancellationToken ct);
    ValueTask<IFile> CreateFile(string path, CancellationToken ct);
    ValueTask<bool> Delete(string path, CancellationToken ct);
    ValueTask<IReadOnlyList<IFolder>> ListFolders(CancellationToken ct);
    ValueTask<IReadOnlyList<IFile>> ListFiles(CancellationToken ct);
}
