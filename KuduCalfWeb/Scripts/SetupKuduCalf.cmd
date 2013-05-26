@echo off

set KUDU_URL=%1
set KUDUCALF_URL=%2

echo Copying script file to Kudu service
curl -k -X PUT -H "If-Match: *" --data-binary @PublishToKuduCalf.cmd %KUDU_URL%/vfs/KuduCalf/PublishToKuduCalf.cmd

echo Configuring Kudu service to publish to calf
curl -k -H "Content-Type: application/json" --data "{ KUDU_SYNC_CMD: '%%HOME%%\\..\\KuduCalf\\PublishToKuduCalf.cmd', KUDUCALF_URL: '%KUDUCALF_URL%' }" %KUDU_URL%/settings

echo Done.