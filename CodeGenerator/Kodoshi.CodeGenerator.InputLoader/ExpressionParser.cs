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

        [Production("message_field : type_reference name SYMBOL_EQUALS[d] NUMBER SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode MessageField(AST.ASTReference type, AST.ASTName name, Token<ExpressionToken> number)
            => new AST.ASTMessageFieldDefinition(name.Value, type, number.IntValue);

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
            return new AST.ASTMessageDefinition(
                name.Value,
                finalGenericArgs,
                messageFields.Cast<AST.ASTMessageFieldDefinition>().ToArray());
        }

        [Production("message_non_template : KEYWORD_MESSAGE[d] name SYMBOL_LEFT_CURLY_BRACKET[d] message_field+ SYMBOL_RIGHT_CURLY_BRACKET[d]")]
        public AST.ASTNode MessageNonTemplate(AST.ASTName name, List<AST.ASTNode> messageFields)
            => new AST.ASTMessageDefinition(
                name.Value,
                Array.Empty<AST.ASTReference>(),
                messageFields.Cast<AST.ASTMessageFieldDefinition>().ToArray());

        [Production("tag_field : name SYMBOL_EQUALS[d] NUMBER SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode TagField(AST.ASTName name, Token<ExpressionToken> number)
            => new AST.ASTTagFieldDefinition(name.Value, number.IntValue, null);

        [Production("tag_field : name SYMBOL_LEFT_ROUND_BRACKET[d] type_reference SYMBOL_RIGHT_ROUND_BRACKET[d] SYMBOL_EQUALS[d] NUMBER SYMBOL_SEMICOLON[d]")]
        public AST.ASTNode TagFieldWithData(AST.ASTName name, AST.ASTReference attachedType, Token<ExpressionToken> number)
            => new AST.ASTTagFieldDefinition(name.Value, number.IntValue, attachedType);

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
    }
}
