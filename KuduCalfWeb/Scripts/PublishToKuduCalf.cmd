@echo on

SET KUDU_SYNC_CMD=D:\KuduService\wwwroot\Bin\Scripts\kudusync.cmd

REM First, call the original KuduSync command, passing all the params
call %KUDU_SYNC_CMD% %*

cd "%DEPLOYMENT_TARGET%"

echo Initialize the Calf repository if needed
git init

git config user.email "kudu"
git config user.name "kudu"

git add -A

echo Commit changes to the Calf repository
git commit -m"Kudu calf deployment"

echo Tell the Calf that there is something new to pull
curl %KUDUCALF_URL%/KuduCalf.ashx

rem curl http://kuducalf.cloudapp.net > nul

