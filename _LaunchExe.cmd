@echo off
setlocal

set exename=%1
shift

if "%1" == "" goto EndArgLoop
set remainder=%1
shift
:ArgLoop
if "%1" == "" goto EndArgLoop
set remainder=%remainder% %1
shift
goto ArgLoop

:EndArgLoop

set runtimedir=%~dp0%exename%-Runtime
echo remainder="%remainder%"

:FindExeLoc
if exist %runtimedir%\CoreRun.exe (
  if exist %runtimedir%\%exename%.exe (
  goto :InvokeExe
  )
  set ERRORLEVEL=1
  echo Error, executable not found: %runtimedir%\%exename%.exe
  exit /b
)

:InvokeExe
echo Invoking: "%runtimedir%\CoreRun.exe %runtimedir%\%exename%.exe %remainder%"
%runtimedir%\CoreRun.exe %runtimedir%\%exename%.exe %remainder%
