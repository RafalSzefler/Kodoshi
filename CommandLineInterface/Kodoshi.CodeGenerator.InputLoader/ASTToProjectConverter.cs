using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private readonly List<ModelDefinition> _modelDefinitions
        = new List<ModelDefinition>();
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
    private readonly List<ServiceDefinition> _serviceDefinitions
        = new List<ServiceDefinition>();
    private readonly List<ASTReference> _materializedModels
        = new List<ASTReference>();
    private readonly List<ModelReference> _materializedReferences
        = new List<ModelReference>();
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
        BuildModels();
        BuildServices();
        BuildMaterializedModels();
        var hash = CalculateHash(
            _settings.ProjectName!,
            _settings.Version!,
            _modelDefinitions,
            _serviceDefinitions,
            _materializedReferences);
        var project = new Project(
            _settings.ProjectName!,
            _settings.Version!,
            hash,
            _modelDefinitions,
            _serviceDefinitions,
            _materializedReferences);
        ValidateProject(project);
        return project;
    }

    private static string CalculateHash(
            string projectName,
            string projectVersion,
            IReadOnlyList<ModelDefinition> models,
            IReadOnlyList<ServiceDefinition> services,
            IReadOnlyList<ModelReference> materializedModels)
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

            // Update models
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

            // Update services
            {
                var length = services.Count;
                updateInt(length);

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

            // Update materialized models
            {
                var length = materializedModels.Count;
                updateInt(length);
                for (var i = 0; i < length; i++)
                {
                    var materializedModel = materializedModels[i];
                    updateString("^MM^");
                    updateReference(materializedModel);
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
            case ASTKind.MATERIALIZED_MODELS:
            {
                var materializedModels = (ASTMaterializedModels)node;
                foreach (var @ref in materializedModels.References)
                {
                    TraverseTopologically(@ref);
                }
                _materializedModels.AddRange(materializedModels.References);
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
                _stack.Push(_currentNmspc);
                var _currentNamespaceSplit = _currentNamespace.Split('.');
                var _currentNamespaceSplitLength = _currentNamespaceSplit.Length;
                if (_currentNamespaceSplitLength > 0)
                {
                    _currentNmspc = _currentNamespaceSplit[0];
                    _stack.Push(_currentNmspc);
                    for (var i = 1; i < _currentNamespaceSplitLength; i++)
                    {
                        var nmspcPiece = _currentNamespaceSplit[i];
                        _currentNmspc += '.' + nmspcPiece;
                        _stack.Push(_currentNmspc);
                    }
                }

                var foundMatch = false;
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
                        if (!foundMatch)
                        {
                            foundMatch = true;
                        }
                        else
                        {
                            throw new ParsingException($"Multiple choices for identifier {@ref.Identifier}. Use full path instead.");
                        }
                    }
                }

                if (!foundMatch)
                {
                    throw new ParsingException($"Invalid reference {@ref.Identifier}");
                }

                return;
            }
            default: break;
        }
    }

    private void BuildModels()
    {
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
                    _modelDefinitions.Add(def);
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
                    _modelDefinitions.Add(def);
                    _currentGenericsMap = oldGenericsMap;
                    _currentGenerics = oldGenerics;
                    break;
                }
                case ASTKind.MATERIALIZED_MODELS: break;
                default: throw new NotImplementedException();
            }
        }
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

    private void BuildServices()
    {
        var count = _services.Count;
        if (count == 0) return;
        for (var i = 0; i < count; i++)
        {
            var astService = _services[i];
            var id = _toIdentifiersMap.GetByKey(astService);
            _serviceDefinitions.Add(new ServiceDefinition(
                id,
                Map(astService.Input),
                Map(astService.Output),
                astService.Id));
        }
    }


    private sealed class ModelReferenceComparer : IEqualityComparer<ModelReference>
    {
        public bool Equals(ModelReference x, ModelReference y)
        {
            if (x is MessageReference xmr && y is MessageReference ymr)
            {
                return xmr.Definition.FullName.Equals(ymr.Definition.FullName);
            }

            if (x is TagReference xt && y is TagReference yt)
            {
                return xt.Definition.FullName.Equals(yt.Definition.FullName);
            }

            bool calculate(Identifier left, Identifier right, IReadOnlyList<ModelReference> leftArgs, IReadOnlyList<ModelReference> rightArgs)
            {
                if (!left.Equals(right))
                {
                    return false;
                }

                var c = leftArgs.Count;
                if (c != rightArgs.Count)
                {
                    return false;
                }
                for (var i = 0; i < c; i++)
                {
                    if (!this.Equals(leftArgs[i], rightArgs[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (x is MessageTemplateReference xmtr && y is MessageTemplateReference ymtr)
                return calculate(xmtr.Definition.FullName, ymtr.Definition.FullName, xmtr.ModelArguments, ymtr.ModelArguments);

            if (x is TagTemplateReference xttr && y is TagTemplateReference yttr)
                return calculate(xttr.Definition.FullName, yttr.Definition.FullName, xttr.ModelArguments, yttr.ModelArguments);

            return false;
        }

        public int GetHashCode(ModelReference obj)
        {
            if (obj is MessageReference omr) return omr.Definition.FullName.GetHashCode();
            if (obj is TagReference otr) return otr.Definition.FullName.GetHashCode();
            int calculate(Identifier id, IEnumerable<ModelReference> refs)
            {
                unchecked
                {
                    uint hash = 2166136261;
                    hash = (hash ^ (uint)id.GetHashCode()) * 16777619;
                    foreach (var arg in refs)
                    {
                        hash = (hash ^ (uint)this.GetHashCode(arg)) * 16777619;
                    }
                    return (int)hash;
                }
            }

            if (obj is MessageTemplateReference omtr) return calculate(omtr.Definition.FullName, omtr.ModelArguments);
            if (obj is TagTemplateReference ottr) return calculate(ottr.Definition.FullName, ottr.ModelArguments);
            throw new NotImplementedException();
        }
    };

    private void BuildMaterializedModels()
    {
        var result = new List<ModelReference>();
        var seen = new HashSet<ModelReference>(new ModelReferenceComparer());
        foreach (var model in _modelDefinitions)
        {
            switch (model.Kind)
            {
                case ModelKind.Message:
                {
                    var @ref = new MessageReference((MessageDefinition)model);
                    ScanModelForMaterial(result, seen, @ref);
                    break;
                }
                case ModelKind.Tag:
                {
                    var @ref = new TagReference((TagDefinition)model);
                    ScanModelForMaterial(result, seen, @ref);
                    break;
                }
                default: break;
            }
        }

        foreach (var service in _serviceDefinitions)
        {
            ScanModelForMaterial(result, seen, service.Input);
            ScanModelForMaterial(result, seen, service.Output);
        }

        foreach (var materializedAst in _materializedModels)
        {
            var @ref = Map(materializedAst);
            ScanModelForMaterial(result, seen, @ref);
        }
        
        _materializedReferences.AddRange(result);
    }

    private void ScanModelForMaterial(
            List<ModelReference> result,
            HashSet<ModelReference> seen,
            ModelReference @ref)
    {
        if (@ref is TemplateArgumentReference) return;
        if (seen.Contains(@ref)) return;
        switch (@ref.Kind)
        {
            case ModelReferenceKind.Message:
            {
                var realRef = (MessageReference)@ref;
                foreach (var field in realRef.Definition.Fields)
                {
                    ScanModelForMaterial(result, seen, field.Type);
                }
                break;
            }
            case ModelReferenceKind.Tag:
            {
                var realRef = (TagReference)@ref;
                foreach (var field in realRef.Definition.Fields)
                {
                    if (field.AdditionalDataType is not null)
                        ScanModelForMaterial(result, seen, field.AdditionalDataType);
                }
                break;
            }
            case ModelReferenceKind.MessageTemplate:
            {
                var realRef = (MessageTemplateReference)@ref;
                foreach (var nestedRef in realRef.ModelArguments)
                {
                    ScanModelForMaterial(result, seen, nestedRef);
                }
                foreach (var field in realRef.Definition.Fields)
                {
                    ScanModelForMaterial(result, seen, field.Type);
                }
                break;
            }
            case ModelReferenceKind.TagTemplate:
            {
                var realRef = (TagTemplateReference)@ref;
                foreach (var nestedRef in realRef.ModelArguments)
                {
                    ScanModelForMaterial(result, seen, nestedRef);
                }
                foreach (var field in realRef.Definition.Fields)
                {
                    if (field.AdditionalDataType is not null)
                        ScanModelForMaterial(result, seen, field.AdditionalDataType);
                }
                break;
            }
            default: throw new NotImplementedException();
        }
        result.Add(@ref);
        seen.Add(@ref);
    }
}
