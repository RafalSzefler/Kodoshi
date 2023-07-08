using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.FileSystem;

public interface IFolder
{
    ValueTask<bool> Exists(string path, CancellationToken ct);
    ValueTask<IFolder> OpenFolder(string path, CancellationToken ct);
    ValueTask<IFolder> CreateFolder(string path, CancellationToken ct);
    ValueTask<IFile> OpenFile(string path, CancellationToken ct);
    ValueTask<IFile> CreateFile(string path, CancellationToken ct);
    ValueTask<bool> Delete(string path, CancellationToken ct);
}
