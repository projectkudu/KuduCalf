
# TODO make this a PS module
Import-Module WebAdministration

function Enable-ScmDeploymentSync {
 [CmdLetBinding()]
    param(
      [Parameter(Mandatory = $true)]
      [string]
      $DeploymentTriggerUri,
      [Parameter(Mandatory = $false)]
      [string]
      $SitePublicUri,
      [Parameter(Mandatory = $false)]
      [string]
      $SiteName = "Default Web Site")
    begin
    {
        $ScmPrivateUri = Cleanup-ScmUri -uri $DeploymentTriggerUri
        $Credentials = CredentialsFromUri -rawUri $ScmPrivateUri
        if(-not ($SitePublicUri))
        {
            $SitePublicUri = Get-VipFromScmEndpoint -scmUri $ScmPrivateUri -Credentials $Credentials
            Write-Host "Auto detected site uri as: $SitePublicUri"
        }

        Write-Host "Updating SCM site with latest KuduCalfCmd"
        Invoke-ScmUpdateKuduCalfCmd -scmUri $ScmPrivateUri -zipPath (Get-KuduCalfCmdZipPath)
        
        Write-Host "Creating Kudu Calf Site"
        $calfSiteLocalUri = Enable-KuduCalfPublishingForSite -SiteName $SiteName
       
        Write-Host "Registering Kudu Calf Site"
        Invoke-KuduCalfWebInit -localUri $calfSiteLocalUri -scmUri $ScmPrivateUri -publicUri $SitePublicUri
    } 
}

function Get-VipFromScmEndpoint([string]$scmUri, [PSCredential]$Credentials)
{
  $resp = Invoke-WebRequest -Uri "$scmUri\Env.aspx" -Credential $Credentials -UseBasicParsing
  if($resp.RawContent -match "X-Forwarded-For=([^:]+)")
  {
    return "http://"+$Matches[1]+"/"
  }
}

function Invoke-KuduCalfWebInit([string]$localUri, [string]$scmUri, [string]$publicUri, [string]$privateUri)
{
  $ret = [KuduCalfWeb.Protocol]::KuduCalfWebInit($localUri, $scmUri, $publicUri, $privateUri);
   Write-Host $ret
}

function Invoke-KuduCalfWebUpdateNotfiy([string]$localUri)
{
   $ret = [KuduCalfWeb.Protocol]::KuduCalfWebUpdateNotify($localUri)
   Write-Host $ret
}

function Invoke-KuduCalfWebStatus([string]$localUri)
{
   $ret = [KuduCalfWeb.Protocol]::KuduCalfWebStatus($localUri)
   Write-Host $ret
}

function Invoke-ScmUpdateKuduCalfCmd([string]$scmUri, [string]$zipPath, [bool]$force =$false)
{
   $ret = [KuduCalfWeb.Protocol]::ScmUpdateKuduCalfCmd($scmUri, $zipPath, $force);
   Write-Host $ret;
}

function Invoke-KuduCalfCmdRegisterSite([string]$scmUri, [string]$siteId, [string]$publicUri)
{
   $ret = [KuduCalfWeb.Protocol]::KuduCalfCmdRegisterSite($scmUri, $siteId, $publicUri, $null);
   Write-Host $ret;
}

function Cleanup-ScmUri([string]$uri)
{
   # force inscure for test stamps because SSL certs are invalid on test stamps
   if($uri.Contains("antares-test.windows-int.net"))
   {
     $uri = $uri.Replace("https://","http://");
   }
   return $uri.Replace("/deploy","/");
}

function Get-AbsolutePath {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [string[]]
        $Path
    )

    process {
        $Path | ForEach-Object {
            $PSCmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath($_)
        }
    }
}

function Remove-FilesInPhysicalDir([string]$Path)
{
    Remove-Item -Path "$Path\*" -Recurse -Confirm
    if(Test-Path -Path "$Path\.git")
    {
        Remove-Item "$Path\.git\*" -Recurse -Confirm
    }
}

function Set-AclsOnDir([string]$Path)
{
    $colRights = [System.Security.AccessControl.FileSystemRights]"Read, Write, ExecuteFile"

    $InheritanceFlag = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit
    $PropagationFlag = [System.Security.AccessControl.PropagationFlags]::InheritOnly

    $objType =[System.Security.AccessControl.AccessControlType]::Allow 

    $objUser = New-Object System.Security.Principal.NTAccount("BUILTIN\IIS_IUSRS") 

    $objACE = New-Object System.Security.AccessControl.FileSystemAccessRule `
        ($objUser, $colRights, $InheritanceFlag, $PropagationFlag, $objType) 

    $objACL = (Get-Item $Path).GetAccessControl("Access")
    $objACL.AddAccessRule($objACE)
    Set-Acl -Path $Path -AclObject $objACL; 
}

function Get-KuduCalfSiteName([string]$Name)
{
    return "KuduCalf site for ($Name)"
}

function Enable-KuduCalfPublishingForSite([string]$SiteName = "Default Web Site")
{
   $calfWebAppPoolName = "KuduCalfWebAppPool"
   EnsureKuduCalfAppPoolExists -Name $calfWebAppPoolName
   $targetSite = Get-WebSite -Name $SiteName
   $calfSiteName = Get-KuduCalfSiteName -Name $SiteName
   [int]$calfSitePort = (Get-LocalPortForSite -Name $SiteName)
   $calfSitePhysicalPath = (Get-KuduCalfWebPath)
   $targetSitePhysicalPath = (New-PhysicalDirectory)
   # this host header is deliberate do not change.
   # the suffix is ".invalid" specifically so this site 
   # is not directly reachable by normal web browsers, but
   # reachable by the publishing site which crafts a custom host header.
   $calfSiteHostHeader = "kuducalf.invalid" 
   Write-Host "Creating Calf Site"
   $site = New-Website -Name $calfSiteName `
                          -Port $calfSitePort `
                          -HostHeader $calfSiteHostHeader `
                          -ApplicationPool $calfWebAppPoolName `
                          -PhysicalPath $calfSitePhysicalPath `
                          -Force
   $vdir = New-WebVirtualDirectory -Name "App" `
                           -Site $calfSiteName `
                           -PhysicalPath $targetSitePhysicalPath `
                           -Force

   $calf = Start-Website -Name $calfSiteName 
   Write-Host "Updating site ($SiteName) physical path to $targetSitePhysicalPath"
   Set-WebSitePhyicalDirectory -SiteName $SiteName -dir $targetSitePhysicalPath 
   Write-Host "Setting Acls"
   Set-AclsOnDir -Path $targetSitePhysicalPath
   Set-AclsOnDir -Path $calfSitePhysicalPath
   return "http://localhost:$calfSitePort/".Trim();
}

function Get-LocalPortForSite([string]$Name)
{
 [string]$bindingInformation = (Get-WebBinding -Name $Name -Protocol "http").bindingInformation;
 [int] $localPort =$bindingInformation.Split(':')[1];
  return $localPort;
}

# this is here so we an easily tweak the script code from the solution file.

function Get-RootPath()
{
    if($devMode) {
      return "$PSScriptRoot\..\Build\obj\Release\KuduCalf";   
    } else {
     return $PSScriptRoot;
    }
}

function Get-KuduCalfWebPath()
{
   return (Get-AbsolutePath -Path (Join-Path -Path (Get-RootPath) -ChildPath "KuduCalfWeb"))
}

function Get-KuduCalfCmdZipPath()
{
   return (Get-AbsolutePath -Path (Join-Path -Path (Get-RootPath) -ChildPath "KuduCalfCmd.zip"))
}

function Get-CredentialsFromUri([string]$rawUri)
{
  [System.Uri]$uri = New-Object System.Uri "$rawUri";
  if($uri.UserInfo -eq $null)
  {
    return $null;
  }
  $toks = $uri.UserInfo.Split(':');
  $username = $toks[0];
  $password = $toks[1] | ConvertTo-SecureString -AsPlainText -Force
  return New-Object System.Management.Automation.PSCredential($username,$password)

}

function New-PhysicalDirectory
{
    $appsPath = Join-Path -Path "c:\inetpub" -ChildPath "KuduCalfApps"
    $appPath =  Join-Path -Path $appsPath -ChildPath ([System.Guid]::NewGuid().ToString("N"))
    New-Item $appPath -ItemType directory

}

function EnsureKuduCalfAppPoolExists([string]$Name)
{
  $appPoolPath = "IIS:\AppPools\$Name"
  if(-not (Test-Path -Path $appPoolPath))
  {
     Write-Host "Creating App Pool $Name"
     $appPool = New-WebAppPool -Name $Name 
     $appPool = (Get-Item -Path $appPoolPath)
     Set-ItemProperty -Force -Path $appPoolPath -Name managedRuntimeVersion -Value "v4.0"
     Set-ItemProperty -Force -Path $appPoolPath -Name managedPipelineMode -Value "Integrated"

     ## need to reconsider running as admin long term.
     $processModel = $appPool.processModel;
     $processModel.identityType = "LocalSystem";
     Set-ItemProperty -Force -Path $appPoolPath -Name processModel -Value $processModel;
  }

}

function Set-WebSitePhyicalDirectory([string]$SiteName, [string]$dir)
{
  $sitePath = "IIS:\sites\$SiteName"
  $site = (Get-Item -Path $sitePath)
  Set-ItemProperty -Force -Path $sitePath -Name physicalPath -Value $dir
}

function Init()
{
    if($global:inited -eq $true)
    {
        return;
    }
$kuduCalfWebfDll = Join-Path -Path (Get-KuduCalfWebPath) -ChildPath "bin\KuduCalfWeb.dll"
Add-Type -LiteralPath $kuduCalfWebfDll
$global:inited = $true;
}

#$devMode = $true;
Init

if($devMode)
{
    function SmokeTest([string]$TestScmUri)
    {
       $TestCreds = CredentialsFromUri -rawUri $TestUri
       Update-KuduCalfCmd  -Credentials $TestCreds -scmUri $TestUri
       Write-Output [KuduCalfWeb.Protocol]::KuduCalfCmdRemote($TestUri,"get-help *");
       Write-Output [KuduCalfWeb.Protocol]::ScmUpdateKuduCalfCmd($TestUri, (Get-KuduCalfCmdZipPath));
    }
}