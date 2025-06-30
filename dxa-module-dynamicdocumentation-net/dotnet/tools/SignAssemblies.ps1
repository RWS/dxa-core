[CmdletBinding(SupportsShouldProcess=$true, PositionalBinding=$false, DefaultParametersetName='None')]
param (   
    #List of assemblies to sign
    [Parameter(Mandatory=$true, HelpMessage="List of assemblies to sign")]
    [string[]]$assemblies
)

$signingUrl = "http://assemblysigner.tridion.global.sdl.corp/GetSigned?file="
$auth = "Basic Z2xvYmFsXHNydi1jbWJ1aWxkOnNydl90cmlkaW9uX2Nt"

foreach($asm in $assemblies)
{
    if(Test-Path $asm) 
    {
        $filename = Split-Path $asm -leaf
        $url = "$signingUrl$filename"
        Write-Host "Signing assembly $asm"
   
        Invoke-WebRequest -Method 'POST' -Uri "$url" -Headers @{'Authorization' = "$auth"; 'cache-control' = 'no-cache'} -InFile "$asm" -OutFile "$asm"
    }
    else
    {
        Write-Host "$asm does not exist.. skipping"
    }
}
