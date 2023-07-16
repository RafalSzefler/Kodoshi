using System.Collections.Generic;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator;

public interface IInputContext
{
    IFile ProjectFile { get; }
    IReadOnlyDictionary<string, string> Settings { get; }
}
