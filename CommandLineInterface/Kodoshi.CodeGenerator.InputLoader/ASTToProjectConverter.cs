using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kodoshi.CodeGenerator.Entities;
using Kodoshi.CodeGenerator.FileSystem;
using Kodoshi.CodeGenerator.InputLoader.AST;

namespace Kodoshi.CodeGenerator.InputLoader;

internal sealed class ASTToProjectConverter
{
    private readonly ProjectSettings _settings;
    private readonly Dictionary<ASTBlock, string> _topNamespaces;
    private readonly (IFile, ASTBlock)[] _blockPairs;
    private readonly TwoWayDictionary<ASTNode, Identifier> _toIdentifiersMap
        = new TwoWayDictionary<ASTNode, Identifier>();
    private readonly TwoWayDictionary<ModelDefinition, Identifier> _modelsByIdentifiers
        = new TwoWayDictionary<ModelDefinition, Identifier>();
    private readonly Dictionary<ASTReference, Identifier> _refsByIdentifiers
        = new Dictionary<ASTReference, Identifier>();
    private readonly HashSet<ASTNode> _temporaryMarkings
        = new HashSet<ASTNode>();
    private readonly HashSet<ASTNode> _permamentMarkings
        = new HashSet<ASTNode>();
    private readonly List<ASTStatement> _topologicalySortedModels
        = new List<ASTStatement>();
    private readonly List<ASTServiceDefinition> _services
        = new List<ASTServiceDefinition>();
    private string _currentNamespace = "";
    private IFile _currentFile;
    private ASTBlock _currentBlock;
    private TwoWayDictionary<string, int> _currentGenericsMap;
    private TemplateArgumentReference[] _currentGenerics;

    public ASTToProjectConverter(
            (IFile, ASTBlock)[] blockPairs,
            ProjectSettings settings)
    {
        _settings = settings;
        var topNamespaces = new Dictionary<ASTBlock, string>(blockPairs.Length);
        foreach (var pair in blockPairs)
        {
            var nmspcStatement = NamespaceExtractor.ExtractTopNamespace(pair.Item1, pair.Item2);
            topNamespaces[pair.Item2] = (nmspcStatement is null) ? "" : nmspcStatement.Identifier;
        }
        _topNamespaces = topNamespaces;
        _blockPairs = blockPairs;
        _currentFile = blockPairs[0].Item1;
        _currentBlock = blockPairs[0].Item2;
        _currentGenericsMap = new TwoWayDictionary<string, int>();
        _currentGenerics = Array.Empty<TemplateArgumentReference>();
    }

    public Project Convert()
    {
        FillIdentifiers();
        SortTopologically();
        var models = BuildModels();
        var services = BuildServices();
        var hash = CalculateHash(
            _settings.ProjectName!,
            _settings.Version!,
            models,
            services);
        var project = new Project(
            _settings.ProjectName!,
            _settings.Version!,
            hash,
            models,
            services);
        ValidateProject(project);
        return project;
    }

    private static string CalculateHash(string projectName, string projectVersion, IReadOnlyList<ModelDefinition> models, IReadOnlyList<ServiceDefinition> services)
    {
        using (var hash = SHA256.Create())
        {
            void updateInt(int value)
            {
                byte[] bytes = BitConverter.GetBytes(value);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                hash!.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }

            void updateString(string value)
            {
                updateInt(value.Length);
                var arr = Encoding.UTF8.GetBytes(value);
                hash!.TransformBlock(arr, 0, arr.Length, null, 0);
            }

            void updateIdentifier(Identifier id)
            {
                updateString(Stringifier.ToString(id));
            }

            void updateReference(ModelReference @ref)
            {
                updateString(Stringifier.ToString(@ref));
            }

            updateString(projectName);
            updateString(projectVersion);

            {
                var length = models.Count;
                updateInt(length);

                for (var i = 0; i < length; i++)
                {
                    var model = models[i];
                    updateString("^M^");
                    updateIdentifier(model.FullName);
                    switch (model.Kind)
                    {
                        case ModelKind.Message:
                        {
                            updateString("M-");
                            var msg = (MessageDefinition)model;
                            updateInt(msg.Fields.Count);
                            foreach (var field in msg.Fields)
                            {
                                updateInt(field.Id);
                                updateInt(field.Name.Length);
                                updateString(field.Name);
                                updateReference(field.Type);
                            }
                            updateString("-M");
                            break;
                        }
                        case ModelKind.MessageTemplate:
                        {
                            updateString("MT-");
                            var msg = (MessageTemplateDefinition)model;
                            updateInt(msg.TemplateArguments.Count);
                            updateInt(msg.Fields.Count);
                            foreach (var field in msg.Fields)
                            {
                                updateInt(field.Id);
                                updateInt(field.Name.Length);
                                updateString(field.Name);
                                updateReference(field.Type);
                            }
                            updateString("-MT");
                            break;
                        }
                        case ModelKind.Tag:
                        {
                            updateString("T-");
                            var msg = (TagDefinition)model;
                            updateInt(msg.Fields.Count);
                            foreach (var field in msg.Fields)
                            {
                                updateInt(field.Value);
                                updateInt(field.Name.Length);
                                updateString(field.Name);
                                if (field.AdditionalDataType is null)
                                {
                                    updateString("$");
                                }
                                else
                                {
                                    updateReference(field.AdditionalDataType);
                                }
                            }
                            updateString("-T");
                            break;
                        }
                        case ModelKind.TagTemplate:
                        {
                            updateString("TT-");
                            var msg = (TagTemplateDefinition)model;
                            updateInt(msg.TemplateArguments.Count);
                            updateInt(msg.Fields.Count);
                            foreach (var field in msg.Fields)
                            {
                                updateInt(field.Value);
                                updateInt(field.Name.Length);
                                updateString(field.Name);
                                if (field.AdditionalDataType is null)
                                {
                                    updateString("$");
                                }
                                else
                                {
                                    updateReference(field.AdditionalDataType);
                                }
                            }
                            updateString("-TT");
                            break;
                        }
                        default: throw new NotImplementedException();
                    }
                }
            }

            {
                var length = services.Count;
                updateInt(services.Count);

                for (var i = 0; i < length; i++)
                {
                    var service = services[i];
                    updateString("^S^");
                    updateIdentifier(service.FullName);
                    updateInt(service.Id);
                    updateReference(service.Input);
                    updateReference(service.Output);
                }
            }

            var magicByte = new byte[] { 37, 123, 88, 214 };
            hash.TransformFinalBlock(magicByte, 0, magicByte.Length);
            return System.Convert.ToBase64String(hash.Hash);
        }
    }

    private void FillIdentifiers()
    {
        foreach (var blockPair in _blockPairs)
        {
            _currentNamespace = _topNamespaces[blockPair.Item2];
            FillIdentifiersForBlock(blockPair.Item2);
        }
    }

    private void FillIdentifiersForBlock(ASTBlock block)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt.Kind)
            {
                case ASTKind.MESSAGE:
                {
                    var real = (ASTMessageDefinition)stmt;
                    var id = new Identifier(real.Name, _currentNamespace);
                    _toIdentifiersMap.Add(stmt, id);
                    break;
                }
                case ASTKind.TAG:
                {
                    var real = (ASTTagDefinition)stmt;
                    var id = new Identifier(real.Name, _currentNamespace);
                    _toIdentifiersMap.Add(stmt, id);
                    break;
                }
                case ASTKind.SERVICE:
                {
                    var real = (ASTServiceDefinition)stmt;
                    var id = new Identifier(real.Name, _currentNamespace);
                    _toIdentifiersMap.Add(stmt, id);
                    break;
                }
                case ASTKind.NAMESPACE:
                {
                    var astNode = (ASTNamespaceStatement)stmt;
                    if (astNode.AttachedBlock is null)
                        break;
                    var oldNmspc = _currentNamespace;
                    string newNmspc;
                    if (string.IsNullOrEmpty(_currentNamespace))
                    {
                        newNmspc = astNode.Identifier;
                    }
                    else
                    {
                        newNmspc = $"{_currentNamespace}.{astNode.Identifier}";
                    }
                    _currentNamespace = newNmspc;
                    FillIdentifiersForBlock(astNode.AttachedBlock);
                    _currentNamespace = oldNmspc;
                    break;
                }
                default: break;
            }
        }
    }

    private void SortTopologically()
    {
        foreach (var (_, block) in _blockPairs)
        {
            TraverseTopologically(block);
        }
    }

    private void TraverseTopologically(ASTNode node)
    {
        if (_permamentMarkings.Contains(node))
            return;
        if (_temporaryMarkings.Contains(node))
            throw new ParsingException("Detected cycle between models. This is not allowed.");

        switch (node.Kind)
        {
            case ASTKind.SERVICE:
            {
                var service = (ASTServiceDefinition)node;
                _services.Add(service);
                TraverseTopologically(service.Input);
                TraverseTopologically(service.Output);
                break;
            }
            case ASTKind.NAMESPACE:
            {
                var nmspc = (ASTNamespaceStatement)node;
                if (nmspc.AttachedBlock is not null)
                {
                    var oldNmspc = _currentNamespace;
                    var newNmspc = nmspc.Identifier;
                    if (!string.IsNullOrEmpty(oldNmspc))
                        newNmspc = $"{oldNmspc}.{newNmspc}";
                    _currentNamespace = newNmspc;
                    TraverseTopologically(nmspc.AttachedBlock);
                    _currentNamespace = oldNmspc;
                }
                break;
            }
            case ASTKind.BLOCK:
            {
                foreach (var stmt in ((ASTBlock)node).Statements)
                    TraverseTopologically(stmt);
                break;
            }
            case ASTKind.MESSAGE:
            {
                var msg = (ASTMessageDefinition)node;
                _temporaryMarkings.Add(msg);

                var oldGenerics = _currentGenericsMap;
                if (msg.GenericArguments.Count > 0)
                {
                    var newGenerics = new TwoWayDictionary<string, int>();
                    for (var i = 0; i < msg.GenericArguments.Count; i++)
                    {
                        newGenerics.Add(msg.GenericArguments[i].Identifier, i);
                    }
                    _currentGenericsMap = newGenerics;
                }
                foreach (var field in msg.Fields)
                    TraverseTopologically(field);
                _temporaryMarkings.Remove(msg);
                _permamentMarkings.Add(msg);
                _topologicalySortedModels.Add(msg);
                _currentGenericsMap = oldGenerics;
                break;
            }
            case ASTKind.MESSAGE_FIELD:
            {
                var field = (ASTMessageFieldDefinition)node;
                TraverseTopologically(field.Type);
                break;
            }
            case ASTKind.TAG:
            {
                var tag = (ASTTagDefinition)node;
                _temporaryMarkings.Add(tag);
                var oldGenerics = _currentGenericsMap;
                if (tag.GenericArguments.Count > 0)
                {
                    var newGenerics = new TwoWayDictionary<string, int>();
                    for (var i = 0; i < tag.GenericArguments.Count; i++)
                    {
                        newGenerics.Add(tag.GenericArguments[i].Identifier, i);
                    }
                    _currentGenericsMap = newGenerics;
                }
                foreach (var field in tag.Fields)
                    TraverseTopologically(field);
                _temporaryMarkings.Remove(tag);
                _permamentMarkings.Add(tag);
                _topologicalySortedModels.Add(tag);
                _currentGenericsMap = oldGenerics;
                break;
            }
            case ASTKind.TAG_FIELD:
            {
                var field = (ASTTagFieldDefinition)node;
                if (field.AttachedType is not null)
                {
                    TraverseTopologically(field.AttachedType);
                }
                break;
            }
            case ASTKind.REFERENCE:
            {
                var @ref = (ASTReference)node;
                if (_refsByIdentifiers.ContainsKey(@ref))
                    break;

                foreach (var genericArg in @ref.GenericArguments)
                {
                    TraverseTopologically(genericArg);
                }

                var realIdPieces = @ref.Identifier.Split('.');
                var realId = realIdPieces[realIdPieces.Length - 1];
                var tmpNmspc = string.Join('.', realIdPieces.Take(realIdPieces.Length - 1));
                var realNmspc = "";
                if (!string.IsNullOrEmpty(tmpNmspc)) realNmspc = tmpNmspc;

                if (string.IsNullOrEmpty(realNmspc))
                {
                    var id = new Identifier(@ref.Identifier, BuiltIns.Namespace);
                    if (
                            BuiltIns.Aliases.ContainsKey(id)
                            || BuiltIns.AllModels.ContainsKey(id))
                    {
                        _refsByIdentifiers.Add(@ref, id);
                        return;
                    }

                    if (_currentGenericsMap.ContainsKey(realId))
                    {
                        _refsByIdentifiers.Add(@ref, new Identifier(@ref.Identifier, null));
                        return;
                    }
                }

                var _stack = new Stack<string>(4);
                var _currentNmspc = "";
                var currentNamespaceSplit =  _currentNamespace.Split('.');
                foreach (var nmspcPiece in currentNamespaceSplit)
                {
                    if (string.IsNullOrWhiteSpace(nmspcPiece)) continue;
                    _stack.Push(nmspcPiece);
                    _currentNmspc += nmspcPiece;
                }

                while (_stack.Count > 0)
                {
                    var stackNmspc = _stack.Pop();
                    if (!string.IsNullOrWhiteSpace(realNmspc))
                    {
                        stackNmspc = string.Join('.', stackNmspc, realNmspc);
                    }
                    var currentId = new Identifier(realId, stackNmspc);
                    if (_toIdentifiersMap.TryGetByValue(currentId, out var subNode))
                    {
                        _refsByIdentifiers.Add(@ref, currentId);
                        TraverseTopologically(subNode);
                        return;
                    }
                }
                throw new ParsingException($"Invalid reference {@ref.Identifier}");
            }
            default: break;
        }
    }

    private IReadOnlyList<ModelDefinition> BuildModels()
    {
        var models = new List<ModelDefinition>(_topologicalySortedModels.Count);
        foreach (var stmt in _topologicalySortedModels)
        {
            switch (stmt.Kind)
            {
                case ASTKind.MESSAGE:
                {
                    var msg = (ASTMessageDefinition)stmt;
                    var oldGenericsMap = _currentGenericsMap;
                    var oldGenerics = _currentGenerics;
                    if (msg.GenericArguments.Count > 0)
                    {
                        var newGenericsMap = new TwoWayDictionary<string, int>();
                        var newGenerics = new TemplateArgumentReference[msg.GenericArguments.Count];
                        for (var i = 0; i < msg.GenericArguments.Count; i++)
                        {
                            newGenericsMap.Add(msg.GenericArguments[i].Identifier, i);
                            newGenerics[i] = new TemplateArgumentReference();
                        }
                        _currentGenericsMap = newGenericsMap;
                        _currentGenerics = newGenerics;
                    }
                    var def = BuildMessageDefinition(msg);
                    _modelsByIdentifiers.Add(def, def.FullName);
                    models.Add(def);
                    _currentGenericsMap = oldGenericsMap;
                    _currentGenerics = oldGenerics;
                    break;
                }
                case ASTKind.TAG:
                {
                    var tag = (ASTTagDefinition)stmt;
                    var oldGenericsMap = _currentGenericsMap;
                    var oldGenerics = _currentGenerics;
                    if (tag.GenericArguments.Count > 0)
                    {
                        var newGenericsMap = new TwoWayDictionary<string, int>();
                        var newGenerics = new TemplateArgumentReference[tag.GenericArguments.Count];
                        for (var i = 0; i < tag.GenericArguments.Count; i++)
                        {
                            newGenericsMap.Add(tag.GenericArguments[i].Identifier, i);
                            newGenerics[i] = new TemplateArgumentReference();
                        }
                        _currentGenericsMap = newGenericsMap;
                        _currentGenerics = newGenerics;
                    }
                    var def = BuildTagDefinition(tag);
                    _modelsByIdentifiers.Add(def, def.FullName);
                    models.Add(def);
                    _currentGenericsMap = oldGenericsMap;
                    _currentGenerics = oldGenerics;
                    break;
                }
                default: throw new NotImplementedException();
            }
        }
        return models;
    }

    private ModelDefinition BuildTagDefinition(ASTTagDefinition tag)
    {
        var tagFields = new List<TagFieldDefinition>();
        foreach (var field in tag.Fields)
        {
            ModelReference? @ref = null;
            if (field.AttachedType is not null)
            {
                @ref = Map(field.AttachedType);
            }
            tagFields.Add(new TagFieldDefinition(@ref, field.Name, field.Value));
        }

        tagFields = tagFields.OrderBy(x => x.Value).ToList();

        var id = _toIdentifiersMap.GetByKey(tag);
        if (tag.GenericArguments.Count == 0)
        {
            return new TagDefinition(id, tagFields);
        }
        else
        {
            return new TagTemplateDefinition(id, _currentGenerics, tagFields);
        }
    }

    private ModelDefinition BuildMessageDefinition(ASTMessageDefinition stmt)
    {
        var messageFields = new List<MessageFieldDefinition>();
        foreach (var field in stmt.Fields)
        {
            messageFields.Add(new MessageFieldDefinition(
                Map(field.Type), field.Name, field.Id));
        }
        messageFields = messageFields.OrderBy(x => x.Id).ToList();

        var id = _toIdentifiersMap.GetByKey(stmt);
        if (stmt.GenericArguments.Count == 0)
        {
            return new MessageDefinition(id, messageFields);
        }
        else
        {
            return new MessageTemplateDefinition(
                id,
                _currentGenerics,
                messageFields);
        }
    }

    private ModelReference Map(ASTReference @ref)
    {
        var id = _refsByIdentifiers[@ref];
        if (string.IsNullOrEmpty(id.Namespace) && _currentGenericsMap.TryGetByKey(id.Name, out var idx))
        {
            return _currentGenerics[idx];
        }

        if (BuiltIns.Aliases.TryGetValue(id, out var realId))
        {
            id = realId;
        }

        ModelDefinition? model = null;
        
        {
            if (BuiltIns.AllModels.TryGetValue(id, out var tmpModel))
            {
                model = tmpModel;
            }
        }

        if (model is null)
        {
            if (_modelsByIdentifiers.TryGetByValue(id, out var tmpModel))
            {
                model = tmpModel;
            }
            else
            {
                throw new Exception("Invalid model configuration");
            }
        }

        switch (model.Kind)
        {
            case ModelKind.Message:
            {
                return new MessageReference((MessageDefinition)model);
            }
            case ModelKind.MessageTemplate:
            {
                var msg = (MessageTemplateDefinition)model;
                if (@ref.GenericArguments.Count != msg.TemplateArguments.Count)
                {
                    throw new ParsingException($"Message {@ref.Identifier} expects {msg.TemplateArguments.Count} template arguments, got {@ref.GenericArguments.Count}.");
                }
                var templateArgs = new List<ModelReference>(@ref.GenericArguments.Count);
                foreach (var arg in @ref.GenericArguments)
                {
                    templateArgs.Add(Map(arg));
                }
                if (object.ReferenceEquals(msg, BuiltIns.ArrayModel) || object.ReferenceEquals(msg, BuiltIns.MapModel))
                {
                    foreach (var templateArg in templateArgs)
                    {
                        if (templateArg is MessageReference templateRef && object.ReferenceEquals(templateRef.Definition, BuiltIns.VoidModel))
                        {
                            throw new ParsingException($"Builtin model {msg.FullName.Name} cannot be declared with {BuiltIns.VoidModel.FullName.Name} template argument.");
                        }
                    }
                }
                return new MessageTemplateReference(msg, templateArgs);
            }
            case ModelKind.Tag:
            {
                return new TagReference((TagDefinition)model);
            }
            case ModelKind.TagTemplate:
            {
                var msg = (TagTemplateDefinition)model;
                if (@ref.GenericArguments.Count != msg.TemplateArguments.Count)
                {
                    throw new ParsingException($"Tag {@ref.Identifier} expects {msg.TemplateArguments.Count} template arguments, got {@ref.GenericArguments.Count}.");
                }
                var templateArgs = new List<ModelReference>(@ref.GenericArguments.Count);
                foreach (var arg in @ref.GenericArguments)
                {
                    templateArgs.Add(Map(arg));
                }
                return new TagTemplateReference(msg, templateArgs);
            }
            default: throw new NotImplementedException();
        }
    }

    private void ValidateProject(Project project)
    {
        var duplicates = new HashSet<int>();
        foreach (var model in project.Models)
        {
            duplicates.Clear();
            IEnumerable<int> ids;
            switch (model.Kind)
            {
                case ModelKind.Message:
                {
                    ids = ((MessageDefinition)model).Fields.Select(x => x.Id);
                    break;
                }
                case ModelKind.MessageTemplate:
                {
                    ids = ((MessageTemplateDefinition)model).Fields.Select(x => x.Id);
                    break;
                }
                case ModelKind.Tag:
                {
                    ids = ((TagDefinition)model).Fields.Select(x => x.Value);
                    break;
                }
                case ModelKind.TagTemplate:
                {
                    ids = ((TagTemplateDefinition)model).Fields.Select(x => x.Value);
                    break;
                }
                default: throw new NotImplementedException();
            }

            foreach (var id in ids)
            {
                if (duplicates.Contains(id))
                {
                    var name = model.FullName.Name;
                    if (!string.IsNullOrEmpty(model.FullName.Namespace))
                    {
                        name = $"{model.FullName.Namespace}.{model.FullName}";
                    }
                    throw new ParsingException($"Model {name} contains fields with duplicate ids/values.");
                }
                duplicates.Add(id);
            }
        }

        duplicates.Clear();
        foreach (var service in project.Services)
        {
            if (duplicates.Contains(service.Id))
            {
                throw new ParsingException($"Found multiple services with same id {service.Id}.");
            }
            duplicates.Add(service.Id);
        }
    }

    private IReadOnlyList<ServiceDefinition> BuildServices()
    {
        var count = _services.Count;
        if (count == 0) return Array.Empty<ServiceDefinition>();
        var result = new ServiceDefinition[count];
        for (var i = 0; i < count; i++)
        {
            var astService = _services[i];
            var id = _toIdentifiersMap.GetByKey(astService);
            result[i] = new ServiceDefinition(
                id,
                Map(astService.Input),
                Map(astService.Output),
                astService.Id);
        }
        return result;
    }
}
