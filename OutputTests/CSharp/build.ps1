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

$rid = "$(Get-Rid)".Trim()
$path = [IO.Path]::GetFullPath("../../bin/$rid/Kodoshi.CodeGenerator.CLI.exe")
$command = "$path -c Kodoshi.CodeGenerator.CSharp -p ./schema/project.yaml"
Invoke-Expression -Command $command