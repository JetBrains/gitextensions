@echo off
echo Copying plugins
set config=%1
md Plugins\
echo Microsoft.TeamFoundation.WorkItemTracking.Client.DataStoreLoader.dll > exclude.txt
echo Microsoft.WITDataStore.dll >> exclude.txt
for /d %%I in ("%~p0\..\Plugins\*", "%~p0\..\Plugins\Statistics\*", "%~p0\..\Plugins\BuildServerIntegration\*") do (
    if exist "%%I\bin\%config%\" (
	echo Copying plugin dir "%%I"
        xcopy /y /r "%%I\bin\%config%\*.dll" Plugins\ /EXCLUDE:exclude.txt
        xcopy /y /r "%%I\bin\%config%\*.pdb" Plugins\
    )
)