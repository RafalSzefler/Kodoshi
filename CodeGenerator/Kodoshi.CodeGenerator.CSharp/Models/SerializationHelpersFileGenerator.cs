using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Kodoshi.CodeGenerator.CSharp.Models;

internal sealed class SerializationHelpersFile
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public SerializationHelpersFile(
        ProjectContext inputContext,
        GenerationContext context,
        Helpers helpers)
    {
        _intputContext = inputContext;
        _context = context;
        _helpers = helpers;
    }

    public async Task Generate(CancellationToken ct)
    {
        await Task.Yield();
        var compilationUnit = BuildTagGetterFile().NormalizeWhitespace(eol: "\n");
        var result = await Helpers.SerializeNode(compilationUnit);
        var file = await (await _context.ModelsFolder).CreateFile("SerializationHelpers.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildTagGetterFile()
    {
        var code = @"
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core;

namespace NAMESPACE
{
    internal static class SerializationHelpers
    {
        private const byte _numericTag = 0;
        private const byte _lengthPrefixedTag = 1;
        private const byte _32bitTag = 2;
        private const byte _64bitTag = 3;
        private const byte _128bitTag = 4;
        private static readonly FrozenDictionary<Type, byte> _tags;

        public static readonly StreamPipeWriterOptions WriterOptions = new StreamPipeWriterOptions(leaveOpen: true);

        static SerializationHelpers()
        {
            var tags = new Dictionary<Type, byte>()
            {
                { typeof(bool), _numericTag },
                { typeof(byte), _numericTag },
                { typeof(sbyte), _numericTag },
                { typeof(short), _numericTag },
                { typeof(ushort), _numericTag },
                { typeof(int), _numericTag },
                { typeof(uint), _numericTag },
                { typeof(long), _numericTag },
                { typeof(ulong), _numericTag },
                { typeof(float), _32bitTag },
                { typeof(double), _64bitTag },
                { typeof(Guid), _128bitTag },
            };
            _tags = tags.ToFrozenDictionary();
        }

        public static byte GetTagValue<T>()
        {
            var t = typeof(T);
            if (_tags.TryGetValue(t, out var result)) return result;
            return _lengthPrefixedTag;
        }

        public static async ValueTask ForwardReader(byte flag, ISerializer<uint> serializer, PipeReader pipeReader, CancellationToken ct)
        {
            int sizeToForward;
            switch (flag)
            {
                case _numericTag:
                {
                    await serializer.DeserializeAsync(pipeReader, ct).ConfigureAwait(false);
                    return;
                }
                case _lengthPrefixedTag:
                {
                    var size = await serializer.DeserializeAsync(pipeReader, ct).ConfigureAwait(false);
                    sizeToForward = (int)size;
                    break;
                }
                case _32bitTag:
                {
                    sizeToForward = 4;
                    break;
                }
                case _64bitTag:
                {
                    sizeToForward = 8;
                    break;
                }
                case _128bitTag:
                {
                    sizeToForward = 16;
                    break;
                }
                default: throw new InvalidOperationException($""Unrecognized field tag {flag}"");
            }
            var result = await pipeReader.ReadAtLeastAsync(sizeToForward, ct).ConfigureAwait(false);
            pipeReader.AdvanceTo(result.Buffer.GetPosition(sizeToForward));
            ct.ThrowIfCancellationRequested();
            if (result.IsCanceled) throw new OperationCanceledException();
        }
    }
}
";
        var unit = ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = NamespaceDeclaration(ParseName(_context.CoreNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        return unit.ReplaceNode(nmspc, newNmspc);
    }
}
