
# TODO make this a PS module
param(
      [Parameter(Mandatory = $true)]
      [string]
      $DeploymentTriggerUri,
      [Parameter(Mandatory = $false)]
      [string]
      $SiteName = "Default Web Site")
. $PSScriptRoot\KuduCalfSync.ps1
Enable-ScmDeploymentSync -DeploymentTriggerUri $DeploymentTriggerUri -SiteName $SiteName

