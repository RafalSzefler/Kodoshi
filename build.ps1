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

function Build
{
    [string] $realScriptDir = [IO.Path]::GetFullPath($scriptDir)
    [string] $binPath = "$realScriptDir/bin"
    [string] $genPath = "$realScriptDir/bin/CodeGenerators"
    [string] $rid = "$(Get-Rid)".Trim()

    New-Item -ItemType Directory -Path $binPath -Force
    New-Item -ItemType Directory -Path $genPath -Force

    cd CommandLineInterface/Kodoshi.CodeGenerator.CLI
    dotnet publish -c Release -r $rid -p:PublishSingleFile=true --self-contained true
    Copy-Item -Path "bin/Release/net7.0/$rid/publish/*" -Recurse -Destination "$binPath"
    cd $realScriptDir/CodeGenerators
    $codeGeneratorFolders = Get-ChildItem -Path . -Directory -Force -ErrorAction SilentlyContinue | Select-Object FullName
    foreach ($folder in $codeGeneratorFolders)
    {
        $path = $folder.FullName
        $folderName = [IO.Path]::GetFileName($path)
        try
        {
            cd $path
            dotnet publish -c Release -r $rid
            New-Item -ItemType Directory -Path "$genPath/$folderName" -Force
            Copy-Item -Path "bin/Release/netstandard2.1/$rid/publish/*" -Recurse -Destination "$genPath/$folderName"
        }
        finally
        {
            cd $scriptDir/CodeGenerators
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
