using System;
using System.Collections.Generic;
using Kodoshi.CodeGenerator.Entities;

namespace Kodoshi.CodeGenerator;

public static class BuiltIns
{
    public static string Namespace { get; } = "_System";
    public static IReadOnlyDictionary<Identifier, ModelDefinition>
        AllModels { get; }
    
    public static IReadOnlyDictionary<Identifier, Identifier> Aliases { get; }
    public static MessageTemplateDefinition ArrayModel { get; }
    public static MessageTemplateDefinition MapModel { get; }
    public static MessageDefinition VoidModel { get; }
    public static MessageDefinition BoolModel { get; }
    public static MessageDefinition Int8Model { get; }
    public static MessageDefinition Int16Model { get; }
    public static MessageDefinition Int32Model { get; }
    public static MessageDefinition Int64Model { get; }
    public static MessageDefinition UInt8Model { get; }
    public static MessageDefinition UInt16Model { get; }
    public static MessageDefinition UInt32Model { get; }
    public static MessageDefinition UInt64Model { get; }
    public static MessageDefinition Float32Model { get; }
    public static MessageDefinition Float64Model { get; }
    public static MessageDefinition StringModel { get; }
    public static MessageDefinition UuidModel { get; }
    

    static BuiltIns()
    {
        var result = new Dictionary<Identifier, ModelDefinition>(16);

        var array = new MessageTemplateDefinition(
            new Identifier("array", Namespace),
            new [] { new TemplateArgumentReference() },
            Array.Empty<MessageFieldDefinition>());
        ArrayModel = array;
        result[array.FullName] = array;

        var map = new MessageTemplateDefinition(
            new Identifier("map", Namespace),
            new [] { new TemplateArgumentReference(), new TemplateArgumentReference() },
            Array.Empty<MessageFieldDefinition>());
        MapModel = map;
        result[map.FullName] = map;

        MessageDefinition NewModel(string name)
        {
            var def = new MessageDefinition(
                new Identifier(name, Namespace),
                Array.Empty<MessageFieldDefinition>());
            result[def.FullName] = def;
            return def;
        }

        var @void = NewModel("void");
        VoidModel = @void;

        var @bool = NewModel("bool");
        BoolModel = @bool;

        var @int8 = NewModel("int8");
        Int8Model = @int8;

        var @int16 = NewModel("int16");
        Int16Model = @int16;

        var @int32 = NewModel("int32");
        Int32Model = @int32;

        var @int64 = NewModel("int64");
        Int64Model = @int64;

        var @uint8 = NewModel("uint8");
        UInt8Model = @uint8;

        var @uint16 = NewModel("uint16");
        UInt16Model = @uint16;

        var @uint32 = NewModel("uint32");
        UInt32Model = @uint32;

        var @uint64 = NewModel("uint64");
        UInt64Model = @uint64;

        var @float32 = NewModel("float32");
        Float32Model = @float32;

        var @float64 = NewModel("float64");
        Float64Model = @float64;

        var @string = NewModel("string");
        StringModel = @string;

        var @uuid = NewModel("uuid");
        UuidModel = @uuid;

        result.TrimExcess();
        AllModels = result;

        var aliases = new Dictionary<Identifier, Identifier>();
        aliases[new Identifier("byte", null)] = @uint8.FullName;
        aliases.TrimExcess();
        Aliases = aliases;
    }
}
