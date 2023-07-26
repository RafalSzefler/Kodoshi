[string] $scriptDir = $PSScriptRoot
[string] $realScriptDir = [IO.Path]::GetFullPath($scriptDir)

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

[string] $rid = "$(Get-Rid)".Trim()
[string] $kodoshiPath = [IO.Path]::GetFullPath([IO.Path]::Join($realScriptDir, "..", "..", "bin", $rid, "bin", "kodoshi"))
[string] $projectPath = [IO.Path]::GetFullPath([IO.Path]::Join($realScriptDir, "schema", "project.yaml"))
[string] $command = "$kodoshiPath -c Kodoshi.CodeGenerator.CSharp -p $projectPath"
echo $command
Invoke-Expression -Command $command