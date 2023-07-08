using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.Core.FileSystem;
using Kodoshi.CodeGenerator.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Kodoshi.CodeGenerator.CLI;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var config = ConfigurationReader.Read(args);
        if (config == null)
        {
            return;
        }
        var startup = config.StartupBuilder();
        var inputContext = await ReadInputContext(config);

        var serviceCollection = new ServiceCollection();
        startup.Register(serviceCollection);
        await using (var services = serviceCollection.BuildServiceProvider())
        await using (var scope = services.CreateAsyncScope())
        {
            var codeGenerator = scope.ServiceProvider.GetRequiredService<ICodeGenerator>();
            await codeGenerator.GenerateFromContext(inputContext, default);
        }
    }

    private static Task<InputContext> ReadInputContext(Configuration config)
    {
        var fooMsg = new MessageDefinition(
            new Identifier("Foo", "MyNmspc"),
            new MessageFieldDefinition[] {
                new MessageFieldDefinition(
                    new MessageReference(BuiltIns.Int32Model),
                    "Value",
                    1
                ),
                new MessageFieldDefinition(
                    new MessageReference(BuiltIns.UuidModel),
                    "Id",
                    2
                )
            }
        );
        var fooMsgRef = new MessageReference(fooMsg);

        var bazMsg = new MessageDefinition(
            new Identifier("Baz", "MyNmspc"),
            new MessageFieldDefinition[] {
                new MessageFieldDefinition(
                    new MessageReference(BuiltIns.StringModel),
                    "Text",
                    1
                ),
                new MessageFieldDefinition(
                    fooMsgRef,
                    "FooRef",
                    2
                ),
                new MessageFieldDefinition(
                    new MessageReference(BuiltIns.StringModel),
                    "Text2",
                    4
                ),
                new MessageFieldDefinition(
                    new MessageReference(BuiltIns.StringModel),
                    "Text3",
                    5
                ),
                new MessageFieldDefinition(
                    new MessageTemplateReference(
                        BuiltIns.ArrayModel,
                        new ModelReference[]
                        {
                            fooMsgRef,
                        }
                    ),
                    "FooRefArray",
                    17
                ),
            }
        );

        var genericArgument = new TemplateArgumentReference();
        var genericExample = new MessageTemplateDefinition(
            new Identifier("Example", "NewNmspc"),
            new[] { genericArgument },
            new MessageFieldDefinition[] {
                new MessageFieldDefinition(
                    new MessageReference(BuiltIns.Int64Model),
                    "Id",
                    1
                ),
                new MessageFieldDefinition(
                    genericArgument,
                    "Value",
                    2
                ),
                new MessageFieldDefinition(
                    new MessageTemplateReference(
                        BuiltIns.ArrayModel,
                        new ModelReference[]
                        {
                            new MessageTemplateReference(
                                BuiltIns.ArrayModel,
                                new ModelReference[]
                                {
                                    genericArgument,
                                }
                            ),
                        }
                    ),
                    "Arrr",
                    5
                ),
            }
        );

        var genericArgument2 = new TemplateArgumentReference();
        var genericArgument3 = new TemplateArgumentReference();
        var genericExample2 = new MessageTemplateDefinition(
            new Identifier("Example2", "NewNmspc"),
            new[] { genericArgument2, genericArgument3 },
            new MessageFieldDefinition[] {
                new MessageFieldDefinition(
                    new MessageReference(BuiltIns.Int64Model),
                    "Id",
                    1
                ),
                new MessageFieldDefinition(
                    genericArgument3,
                    "Value",
                    2
                ),
                 new MessageFieldDefinition(
                    new MessageTemplateReference(
                        BuiltIns.DictionaryModel,
                        new ModelReference[]
                        {
                            genericArgument2, genericArgument3
                        }
                    ),
                    "Mapp",
                    5
                ),
            }
        );
        
        var templateTag = new TemplateArgumentReference();
        var bazRef = new MessageReference(bazMsg);
        var genericTag = new TagTemplateDefinition(
            new Identifier("MainTaggy", ""),
            new [] { templateTag },
            new TagFieldDefinition[] {
                new TagFieldDefinition(null, "Empty", 0),
                new TagFieldDefinition(templateTag, "Value", 1),
                new TagFieldDefinition(new MessageTemplateReference(genericExample, new[]{ bazRef }), "Baz", 3),
            }
        );

        var simpleTag = new TagDefinition(
            new Identifier("SimpleTag", "Abcd"),
            new TagFieldDefinition[] {
                new TagFieldDefinition(bazRef, "ZeroValue", 0),
                new TagFieldDefinition(null, "Value", 1),
            }
        );


        var project = new Project(
            "TestProject",
            "1.2.3",
            new ModelDefinition[] {
                fooMsg,
                bazMsg,
                genericExample,
                genericExample2,
                genericTag,
                simpleTag,
            },
            new ServiceDefinition[]
            {
                new ServiceDefinition("Main", bazRef, new TagReference(simpleTag), 1),
            }
        );

        var folder = new FolderImpl("../testoutput");

        return Task.FromResult(new InputContext(
            project,
            folder,
            new Dictionary<string, string>()
        ));
    }

    private static Task Generate(Configuration config)
    {
        return Task.CompletedTask;
    }
}
