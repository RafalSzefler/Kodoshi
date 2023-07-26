[string] $currentWorkingDir = (Get-Location).Path
[string] $scriptDir = $PSScriptRoot

function Get-Rid
{
    [string] $rid;
    if ($IsWindows)
    {
        $rid = "win";
    }
    elseif ($IsMacOS)
    {
        $rid = "osx"
    }
    elseif ($IsLinux)
    {
        $rid = "linux"
    }
    else
    {
        throw "Invalid OS"
    }
    return "$rid-$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLower())"
}

function CreateCommand
{
    [string] $targetName
    [string] $commandFileName
    [string] $content
    [string] $additionalCommand
    if ($IsLinux)
    {
        $commandFileName = "kodoshi"
        $content = @"
#!/usr/bin/bash
SCRIPT_DIR=$\( cd -- "$\( dirname -- "$\{BASH_SOURCE[0]\}" \)" &> /dev/null && pwd \)
SCRIPT_DIR/Kodoshi.CodeGenerator.CLI "$@"
"@
        $additionalCommand = "chmod a+x kodoshi"
    }
    elseif ($IsWindows)
    {
        $commandFileName = "kodoshi.cmd"
        $content = @"
@echo off
setlocal
"%~dp0\Kodoshi.CodeGenerator.CLI.exe" %*
endlocal
"@
    }
    else
    {
        throw "Invalid OS"
    }

    New-Item $commandFileName
    Set-Content $commandFileName ($content.Trim())
    if ($additionalCommand)
    {
        Invoke-Expression -Command $additionalCommand
    }
}

function Build
{
    [string] $realScriptDir = [IO.Path]::GetFullPath($scriptDir)
    [string] $rid = "$(Get-Rid)".Trim()
    [string] $binPath = [IO.Path]::Join($realScriptDir, "bin", $rid)
    [string] $genPath = [IO.Path]::Join($binPath, "CodeGenerators")

    Remove-Item -Recurse -Force $binPath -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $binPath -Force
    New-Item -ItemType Directory -Path $genPath -Force

    Set-Location ([IO.Path]::Join($realScriptDir, "CommandLineInterface", "Kodoshi.CodeGenerator.CLI"))
    dotnet publish -c Release -r $rid -p:PublishSingleFile=true --self-contained true
    Copy-Item -Path ([IO.Path]::Join("bin", "Release", "net7.0", $rid, "publish", "*")) -Recurse -Destination $binPath

    Set-Location $binPath
    CreateCommand

    Set-Location ([IO.Path]::Join($realScriptDir, "CodeGenerators"))
    $codeGeneratorFolders = Get-ChildItem -Path . -Directory -Force -ErrorAction SilentlyContinue | Select-Object FullName
    foreach ($folder in $codeGeneratorFolders)
    {
        $path = $folder.FullName
        $folderName = [IO.Path]::GetFileName($path)
        try
        {
            Set-Location $path
            dotnet publish -c Release -r $rid
            $targetPath = [IO.Path]::Join($genPath, $folderName)
            New-Item -ItemType Directory -Path $targetPath -Force
            Copy-Item -Path ([IO.Path]::Join("bin", "Release", "netstandard2.1", $rid, "publish", "*")) -Recurse -Destination $targetPath
        }
        finally
        {
            Set-Location ([IO.Path]::Join($realScriptDir, "CodeGenerators"))
        }
    }
}


function Main
{
    try
    {
        Set-Location $scriptDir
        Build
    }
    finally
    {
        Set-Location $currentWorkingDir
    }
}

Main
