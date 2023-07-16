using System;
using System.Collections.Generic;
using System.Linq;
using sly.lexer;
using sly.parser.generator;
using sly.parser.parser;

namespace Kodoshi.CodeGenerator.InputLoader
{
    internal sealed class ExpressionParser
    {
        [Production("root : statement*")]
        public AST.ASTNode Root(List<AST.ASTNode> statements) => new AST.ASTBlock(statements.Cast<AST.ASTStatement>().ToArray());

        [Production("number : NUMBER")]
        public AST.ASTNode Number(Token<ExpressionToken> token) => new AST.ASTNumber(token.IntValue);

        [Production("name : IDENTIFIER")]
        public AST.ASTNode Name(Token<ExpressionToken> token) => new AST.ASTName(token.Value);

        [Production("id : name (SYMBOL_DOT[d] name)*")]
        public AST.ASTNode IdentifierWithDot(AST.ASTName identifier, List<Group<ExpressionToken, AST.ASTNode>> names)
            => new AST.ASTName(identifier.Value + string.Join(".", names.Select(x => ((AST.ASTName)x.Value(0)).Value)));

        [Production("namespace : KEYWORD_NAMESPACE[d] id SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode Namespace(AST.ASTNode identifier)
            => new AST.ASTNamespaceStatement(((AST.ASTName)identifier).Value, null);

        [Production("namespace : KEYWORD_NAMESPACE[d] id SYMBOL_LEFT_CURLY_BRACKET[d] statement* SYMBOL_RIGHT_CURLY_BRACKET[d]")]
        public AST.ASTNode NamespaceWithBlock(AST.ASTNode identifier, List<AST.ASTNode> seq)
            => new AST.ASTNamespaceStatement(((AST.ASTName)identifier).Value, new AST.ASTBlock(seq.Cast<AST.ASTStatement>().ToArray()));

        [Production("statement : message_template")]
        [Production("statement : message_non_template")]
        [Production("statement : tag_template")]
        [Production("statement : tag_non_template")]
        [Production("statement : namespace")]
        [Production("statement : service")]
        public AST.ASTNode NamespaceStatement(AST.ASTNode @namespace) => @namespace;

        [Production("type_reference : id")]
        public AST.ASTNode TypeReferenceNonGeneric(AST.ASTName @namespace) => new AST.ASTReference(@namespace.Value, Array.Empty<AST.ASTReference>());

        [Production("type_reference : id SYMBOL_LEFT_ANGLE_BRACKET[d] type_reference (SYMBOL_COMMA[d] type_reference)* SYMBOL_RIGHT_ANGLE_BRACKET[d]")]
        public AST.ASTNode TypeReferenceGeneric(AST.ASTName name, AST.ASTReference reference, List<Group<ExpressionToken, AST.ASTNode>> otherReferences)
        {
            var args = new List<AST.ASTReference>(otherReferences.Count + 1);
            args.Add(reference);
            foreach (var otherRef in otherReferences)
            {
                args.Add((AST.ASTReference)otherRef.Value(0));
            }
            return new AST.ASTReference(name.Value, args);
        }

        [Production("message_field : key_value_pair")]
        public AST.ASTNode KVPMessageField(AST.ASTNode node) => node;

        [Production("message_field : type_reference name SYMBOL_EQUALS[d] number SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode MessageField(AST.ASTReference type, AST.ASTName name, AST.ASTNumber number)
            => new AST.ASTMessageFieldDefinition(name.Value, type, number.Value);

        [Production("message_template : KEYWORD_MESSAGE_TEMPLATE[d] name SYMBOL_LEFT_ANGLE_BRACKET[d] id (SYMBOL_COMMA[d] id)* SYMBOL_RIGHT_ANGLE_BRACKET[d] SYMBOL_LEFT_CURLY_BRACKET[d] message_field+ SYMBOL_RIGHT_CURLY_BRACKET[d]")]
        public AST.ASTNode MessageTemplate(AST.ASTName name, AST.ASTName genericArg1, List<Group<ExpressionToken, AST.ASTNode>> genericArgs, List<AST.ASTNode> messageFields)
        {
            var finalGenericArgs = new List<AST.ASTReference>(genericArgs.Count + 1);
            var empty = Array.Empty<AST.ASTReference>();
            finalGenericArgs.Add(new AST.ASTReference(genericArg1.Value, empty));
            foreach (var genericArg in genericArgs)
            {
                finalGenericArgs.Add(new AST.ASTReference(((AST.ASTName)genericArg.Value(0)).Value, empty));
            }
            var deprecatedFields = new HashSet<int>();
            var foundFields = new HashSet<int>();
            var messageFieldDefs = new List<AST.ASTMessageFieldDefinition>();
            foreach (var node in messageFields)
            {
                if (node is AST.ASTKeyValuePair kvp)
                {
                    if (kvp.Key != "used_ids")
                    {
                        throw new ParsingException($"Option [{kvp.Key}] not allowed. Allowed options are: used_ids.");
                    }

                    if (kvp.Value is AST.ASTNumber num)
                    {
                        deprecatedFields.Add(num.Value);
                    }
                    else if (kvp.Value is AST.ASTNumberArray arr)
                    {
                        foreach (var value in arr.Values)
                            deprecatedFields.Add(value);
                    }
                    else
                    {
                        throw new ParsingException($"Option [{kvp.Key}] requires number or number array as value.");
                    }
                }
                else if (node is AST.ASTMessageFieldDefinition mfd)
                {
                    foundFields.Add(mfd.Id);
                    messageFieldDefs.Add(mfd);
                }
            }

            var intersection = deprecatedFields.Intersect(foundFields).ToArray();
            if (intersection.Length > 0)
            {
                var ids = string.Join(", ", intersection.OrderBy(x => x).Select(x => x.ToString()));
                throw new ParsingException($"Ids {ids} are marked as used.");
            }

            return new AST.ASTMessageDefinition(
                name.Value,
                finalGenericArgs,
                messageFieldDefs);
        }

        [Production("message_non_template : KEYWORD_MESSAGE[d] name SYMBOL_LEFT_CURLY_BRACKET[d] message_field+ SYMBOL_RIGHT_CURLY_BRACKET[d]")]
        public AST.ASTNode MessageNonTemplate(AST.ASTName name, List<AST.ASTNode> messageFields)
            => new AST.ASTMessageDefinition(
                name.Value,
                Array.Empty<AST.ASTReference>(),
                messageFields.Cast<AST.ASTMessageFieldDefinition>().ToArray());

        [Production("tag_field : name SYMBOL_EQUALS[d] number SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode TagField(AST.ASTName name, AST.ASTNumber number)
            => new AST.ASTTagFieldDefinition(name.Value, number.Value, null);

        [Production("tag_field : name SYMBOL_LEFT_ROUND_BRACKET[d] type_reference SYMBOL_RIGHT_ROUND_BRACKET[d] SYMBOL_EQUALS[d] number SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode TagFieldWithData(AST.ASTName name, AST.ASTReference attachedType, AST.ASTNumber number)
            => new AST.ASTTagFieldDefinition(name.Value, number.Value, attachedType);

        [Production("tag_template : KEYWORD_TAG_TEMPLATE[d] name SYMBOL_LEFT_ANGLE_BRACKET[d] id (SYMBOL_COMMA[d] id)* SYMBOL_RIGHT_ANGLE_BRACKET[d] SYMBOL_LEFT_CURLY_BRACKET[d] tag_field+ SYMBOL_RIGHT_CURLY_BRACKET[d]")]
        public AST.ASTNode TagTemplate(AST.ASTName name, AST.ASTName genericArg1, List<Group<ExpressionToken, AST.ASTNode>> genericArgs, List<AST.ASTNode> tagFields)
        {
            var finalGenericArgs = new List<AST.ASTReference>(genericArgs.Count + 1);
            var empty = Array.Empty<AST.ASTReference>();
            finalGenericArgs.Add(new AST.ASTReference(genericArg1.Value, empty));
            foreach (var genericArg in genericArgs)
            {
                finalGenericArgs.Add(new AST.ASTReference(((AST.ASTName)genericArg.Value(0)).Value, empty));
            }
            return new AST.ASTTagDefinition(
                name.Value,
                finalGenericArgs,
                tagFields.Cast<AST.ASTTagFieldDefinition>().ToArray());
        }

        [Production("tag_non_template : KEYWORD_TAG[d] name SYMBOL_LEFT_CURLY_BRACKET[d] tag_field+ SYMBOL_RIGHT_CURLY_BRACKET[d]")]
        public AST.ASTNode TagNontTemplate(AST.ASTName name, List<AST.ASTNode> tagFields)
            => new AST.ASTTagDefinition(
                name.Value,
                Array.Empty<AST.ASTReference>(),
                tagFields.Cast<AST.ASTTagFieldDefinition>().ToArray());

        [Production("number_array : number (SYMBOL_COMMA[d] number)+")]
        public AST.ASTNode NumberArray(AST.ASTNumber number, List<Group<ExpressionToken, AST.ASTNode>> items)
        {
            var count = items.Count;
            var numbers = new int[count + 1];
            numbers[0] = number.Value;
            for (var i = 1; i < count + 1; i++)
            {
                numbers[i] = ((AST.ASTNumber)items[i].Value(0)).Value;
            }
            return new AST.ASTNumberArray(numbers);
        }

        [Production("key_value_pair_value : type_reference")]
        [Production("key_value_pair_value : number")]
        [Production("key_value_pair_value : number_array")]
        [Production("key_value_pair_value : name")]
        public AST.ASTNode KeyValuePairValue(AST.ASTNode node) => node;
        
        [Production("key_value_pair : KEY SYMBOL_EQUALS[d] key_value_pair_value SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode KeyValuePair(Token<ExpressionToken> token, AST.ASTNode value)
            => new AST.ASTKeyValuePair(token.Value.Substring(1, token.Value.Length-1), value);

        
        [Production("service : KEYWORD_SERVICE[d] name SYMBOL_LEFT_CURLY_BRACKET[d] key_value_pair+ SYMBOL_RIGHT_CURLY_BRACKET[d]")]
        public AST.ASTNode ServiceDefinition(AST.ASTName name, List<AST.ASTNode> keyValuePairs)
        {
            const string inputField = "input";
            const string outputField = "output";
            const string idField = "id";
            int? id = null;
            AST.ASTReference? input = null;
            AST.ASTReference? output = null;
            foreach (var node in keyValuePairs)
            {
                var kvp = (AST.ASTKeyValuePair)node;
                if (kvp.Key == inputField)
                {
                    if (input is not null)
                    {
                        throw new ParsingException($"Multiple [{inputField}] fields on service {name.Value}.");
                    }
                    if (kvp.Value is not AST.ASTReference @ref)
                    {
                        throw new ParsingException($"[{inputField}] field on service {name.Value} is not a reference.");
                    }
                    input = @ref;
                }
                else if (kvp.Key == outputField)
                {
                    if (output is not null)
                    {
                        throw new ParsingException($"Multiple [{outputField}] fields on service {name.Value}.");
                    }
                    if (kvp.Value is not AST.ASTReference @ref)
                    {
                        throw new ParsingException($"[{outputField}] field on service {name.Value} is not a reference.");
                    }
                    output = @ref;
                }
                else if (kvp.Key == idField)
                {
                    if (id.HasValue)
                    {
                        throw new ParsingException($"Multiple [{idField}] fields on service {name.Value}.");
                    }
                    if (kvp.Value is AST.ASTNumber num)
                    {
                        id = num.Value;
                    }
                    else if (kvp.Value is AST.ASTNumberArray arr)
                    {
                        if (arr.Values.Count != 1)
                        {
                            throw new ParsingException($"Multiple value choices for field [{idField}] on service {name.Value} is not allowed.");
                        }
                        id = arr.Values[0];
                    }
                    else
                    {
                        throw new ParsingException($"[{idField}] field on service {name.Value} is not a number or number array.");
                    }
                }
            }

            if (!id.HasValue)
            {
                throw new ParsingException($"Missing [{idField}] field on service {name.Value}.");
            }
            if (id.Value <= 0)
            {
                throw new ParsingException($"Service {name.Value} has to have a positive id.");
            }

            if (input is null)
            {
                throw new ParsingException($"Missing [{inputField}] field on service {name.Value}.");
            }
            if (output is null)
            {
                throw new ParsingException($"Missing [{outputField}] field on service {name.Value}.");
            }

            return new AST.ASTServiceDefinition(name.Value, input, output, id.Value);
        }
    }
}
