cd ../../CodeGenerators/Kodoshi.CodeGenerator.CSharp
dotnet publish -c Release
cd ../../CommandLineInterface/Kodoshi.CodeGenerator.CLI
dotnet publish -c Release
cd ../../OutputTests/CSharp

New-Item -ItemType Directory -Path "bin" -Force
New-Item -ItemType Directory -Path "bin/CodeGenerators" -Force
New-Item -ItemType Directory -Path "bin/CodeGenerators/Kodoshi.CodeGenerator.CSharp" -Force
Copy-Item -Path "../../CodeGenerators/Kodoshi.CodeGenerator.CSharp/bin/Release/netstandard2.1/publish/*.dll" -Destination “bin/CodeGenerators/Kodoshi.CodeGenerator.CSharp”
Copy-Item -Path "../../CodeGenerators/Kodoshi.CodeGenerator.CSharp/bin/Release/netstandard2.1/publish/*.json" -Destination “bin/CodeGenerators/Kodoshi.CodeGenerator.CSharp”
Copy-Item -Path "../../CommandLineInterface/Kodoshi.CodeGenerator.CLI/bin/Release/net7.0/publish/*.dll" -Destination “bin”
Copy-Item -Path "../../CommandLineInterface/Kodoshi.CodeGenerator.CLI/bin/Release/net7.0/publish/*.json" -Destination “bin”
Copy-Item -Path "../../CommandLineInterface/Kodoshi.CodeGenerator.CLI/bin/Release/net7.0/publish/*.exe" -Destination “bin”
./bin/Kodoshi.CodeGenerator.CLI.exe -c Kodoshi.CodeGenerator.CSharp -p ./schema/project.yaml