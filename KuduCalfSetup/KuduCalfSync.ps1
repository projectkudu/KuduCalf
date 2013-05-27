
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
            Write-Host "Autodetected site uri as: $SitePublicUri"
        }

        Write-Host "Updating KuduCalfCmd"
        Update-KuduCalfCmd  -Credentials $Credentials -scmUri $ScmPrivateUri
        $calfSiteLocalUri = Enable-KuduCalfPublishingForSite -SiteName $SiteName
        Write-Host "Requesting Init"
        Invoke-KuduCalfInit -localUri $calfSiteLocalUri -scmUri $ScmPrivateUri
        $siteId = Get-SiteSubscriptionId -SiteName $SiteName
        Set-SiteSubscription -scmUri $ScmPrivateUri -Credentials $Credentials -siteUri $SitePublicUri -siteId $siteId
    } 
}

function Invoke-KuduCalfInit([string]$localUri, [string]$scmUri)
{
   $ret = [KuduCalfRequest]::Init($localUri,$scmUri + "Git/site/wwwroot/")
   Write-Host $ret
}

function Invoke-KuduCalfFetch([string]$localUri)
{
   $ret = [KuduCalfRequest]::Fetch($localUri)
   Write-Host $ret
}

function Invoke-KuduCalfStatus([string]$localUri)
{
   $ret = [KuduCalfRequest]::Status($localUri)
   Write-Host $ret
}

function Get-VipFromScmEndpoint([string]$scmUri, [PSCredential]$Credentials)
{
  $resp = Invoke-WebRequest -Uri "$scmUri\Env.aspx" -Credential $Credentials
  if($resp.RawContent -match "X-Forwarded-For=([^:]+)")
  {
    return "http://"+$Matches[1]+"/"
  }
}

function Set-SiteSubscription ([string]$scmUri, [PSCredential]$Credentials, [string]$siteUri, [string]$siteId)
{
 $Command = "%HOME%\KuduCalfCmd\KuduCalfCmd.exe New-Subscriber -S $siteId -u $siteUri"
 Invoke-ScmRemoteCommand -Uri $scmUri -Credentials $Credentials -Command $Command
}

function Update-KuduCalfCmd ([string]$scmUri, [PSCredential]$Credentials)
 {
    if($Credentials -eq $null)
    {
        $Credentials = (Get-Credential) 
    }
    $zipPath = (Get-KuduCalfCmdZipPath)
    Invoke-RestMethod -Method Put -Uri "$scmUri/zip/site" -InFile $zipPath -Credential $Credentials 
    Set-KuduSyncCmd -scmUri $scmUri -Credentials $Credentials -value "%HOME%\KuduCalfCmd\KuduCalfCmd.exe KuduSync" 
    Invoke-ScmRemoteCommand -Uri $scmUri -Credentials $Credentials -Command "%HOME%\KuduCalfCmd\KuduCalfCmd.exe Initialize-SyncState"
}

function Set-KuduSyncCmd ([string]$scmUri, [PSCredential]$Credentials, [string]$value) {
     $body = (New-Object PSObject -Property @{ KUDU_SYNC_CMD = $value} | ConvertTo-Json)
     $ret = (Invoke-RestMethod -Method POST -Uri "$scmUri/settings" -Body $body -Credential $Credentials -ContentType "application/json")   
}

function Invoke-ScmRemoteCommand {
 [CmdLetBinding()]
    param(
      [Parameter(Mandatory = $true)]
      [string]
      $Uri,
      [Parameter(Mandatory = $false)]
      [PSCredential]
      $Credentials,
      [Parameter(Mandatory = $true)]
      [string]
      $Command)
    process {
     $body = (New-Object PSObject -Property @{ command = $Command; dir = "."} | ConvertTo-Json)
     $ret = (Invoke-RestMethod -Method POST -Uri "$Uri/command" -Body $body -Credential $Credentials -ContentType "application/json")  
     Write-Host $ret.Error
     Write-Host $ret.Output
    }
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

function Enable-KuduCalfPublishingForSite([string]$SiteName = "Default Web Site")
{
   $calfWebAppPoolName = "KuduCalfWebAppPool"
   EnsureKuduCalfAppPoolExists -Name $calfWebAppPoolName
   $targetSite = Get-WebSite -Name $SiteName
   $calfSiteName = "KuduCalf site for ($SiteName)" 
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

function Get-SiteSubscriptionId([string] $SiteName)
{
    $siteId = (Get-WebSite -Name $SiteName).Id;
    $machineId = (Get-MachineId);
    return "site"+$siteId+"."+$machineId;
}

function Get-MachineId()
{
    return [System.Net.Dns]::GetHostEntry([System.Net.Dns]::GetHostName()).HostName;
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
$code = @"
using System;
using System.Net;
static public class KuduCalfRequest
{
    private static string GetText(System.IO.Stream strm)
    {
        using (var rd = new System.IO.StreamReader(strm))
        {
            return rd.ReadToEnd();
        }
    }
    private static string GetResponse(HttpWebRequest req)
    {
        try
        {
            var resp = (HttpWebResponse)req.GetResponse();
            var detail = GetText(resp.GetResponseStream());
            return String.Format("{0}\n{1}", resp.StatusCode, detail);
        }
        catch (WebException we)
        {
            var resp = (HttpWebResponse)we.Response;
            var detail = GetText(resp.GetResponseStream());
            return String.Format("Status-Code: {0}\n{1}", resp.StatusCode, detail);
        }
    }
    public static string Init(string s, string repoUri)
    {
        var req = (HttpWebRequest)WebRequest.Create(s + "KuduCalf.ashx?comp=init");
        req.Host = "kuducalf.invalid";
        req.Method = "POST";
        var strm = req.GetRequestStream();
        using (var txtwr = new System.IO.StreamWriter(strm))
        {
            txtwr.WriteLine(repoUri);
        }
        strm.Close();
        return GetResponse(req);
    }
    public static string Fetch(string s)
    {
        var req = (HttpWebRequest)WebRequest.Create(s + "KuduCalf.ashx?comp=fetch");
        req.Host = "kuducalf.invalid";
        req.Method = "POST";
        req.GetRequestStream().Close();
        return GetResponse(req);
    }

    public static string Status(string s)
    {
        var req = (HttpWebRequest)WebRequest.Create(s + "KuduCalf.ashx");
        req.Host = "kuducalf.invalid";
        req.Method = "GET";
        return GetResponse(req);
    }
}
"@
Add-Type $code -Language CSharp
$global:inited = $true;
}
#$devMode = $true;
Init