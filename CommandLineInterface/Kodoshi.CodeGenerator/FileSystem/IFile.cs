using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.FileSystem;

public interface IFile
{
    string FullName { get; }
    string Name { get; }
    IFolder ParentFolder { get; }
    ValueTask Write(ReadOnlyMemory<byte> buffer, CancellationToken ct);
    ValueTask<ReadOnlyMemory<byte>> Read(CancellationToken ct);
}
