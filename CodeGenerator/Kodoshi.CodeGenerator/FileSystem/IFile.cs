using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.FileSystem;

public interface IFile
{
    ValueTask Write(ReadOnlyMemory<byte> buffer, CancellationToken ct);
    ValueTask<ReadOnlyMemory<byte>> Read(CancellationToken ct);
}
