using sly.lexer;

namespace Kodoshi.CodeGenerator.InputLoader
{
    internal enum ExpressionToken
    {
        [Lexeme("\\s+", isSkippable: true)]
        WHITESPACE = 0,

        [Lexeme("message\\s+template")]
        KEYWORD_MESSAGE_TEMPLATE = 100,

        [Lexeme("message")]
        KEYWORD_MESSAGE = 101,

        [Lexeme("tag\\s+template")]
        KEYWORD_TAG_TEMPLATE = 102,

        [Lexeme("tag")]
        KEYWORD_TAG = 103,

        [Lexeme("namespace")]
        KEYWORD_NAMESPACE = 104,

        [Lexeme("service")]
        KEYWORD_SERVICE = 105,

        [Lexeme(";")]
        SYMBOL_SEMICOLON = 200,

        [Lexeme("=")]
        SYMBOL_EQUALS = 201,

        [Lexeme("{")]
        SYMBOL_LEFT_CURLY_BRACKET = 202,

        [Lexeme("}")]
        SYMBOL_RIGHT_CURLY_BRACKET = 203,

        [Lexeme("\\(")]
        SYMBOL_LEFT_ROUND_BRACKET = 204,

        [Lexeme("\\)")]
        SYMBOL_RIGHT_ROUND_BRACKET = 205,

        [Lexeme("<")]
        SYMBOL_LEFT_ANGLE_BRACKET = 206,

        [Lexeme(">")]
        SYMBOL_RIGHT_ANGLE_BRACKET = 207,

        [Lexeme("\\.")]
        SYMBOL_DOT = 208,

        [Lexeme(",")]
        SYMBOL_COMMA = 209,

        [Lexeme("@")]
        SYMBOL_AT = 210,

        [Lexeme("[0-9]+")]
        NUMBER = 300,

        [Lexeme("[a-zA-Z][a-zA-Z0-9_]*")]
        IDENTIFIER = 301,
    }
}
