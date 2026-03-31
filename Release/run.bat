@ECHO OFF
SET TargetWorld=
SET HeadlessArgs=

:ParseArgs
IF "%~1" == "" GOTO EndParseArgs
IF /I "%~1" == "-h" (
	SET HeadlessArgs=-batchmode
) ELSE IF /I "%~1" == "--headless" (
	SET HeadlessArgs=-batchmode
) ELSE IF /I "%~1" == "-w" (
	SET TargetWorld=%~2
	SHIFT
) ELSE IF /I "%~1" == "--world" (
	SET TargetWorld=%~2
	SHIFT
) ELSE IF "%TargetWorld%" == "" (
	SET TargetWorld=%~1
)
SHIFT
GOTO ParseArgs

:EndParseArgs

IF NOT DEFINED CLOISIM_WORLD_PATH (
	ECHO.
	ECHO CLOISIM_WORLD_PATH is empty
	CALL :ShowEnvironmentHelp
) ELSE IF NOT DEFINED CLOISIM_MODEL_PATH (
	ECHO.
	ECHO CLOISIM_MODEL_PATH is empty
	CALL :ShowEnvironmentHelp
) ELSE IF NOT DEFINED CLOISIM_FILES_PATH (
	ECHO.
	ECHO CLOISIM_FILES_PATH is empty
	CALL :ShowEnvironmentHelp
) ELSE IF "%TargetWorld%" == "" (
	ECHO.
	ECHO Pass the world file name as a 1st argument
	ECHO.
	ECHO   ex^) run.bat lg_seocho.world
	ECHO   ex^) run.bat --world lg_seocho.world
	ECHO   ex^) run.bat --headless --world lg_seocho.world
	ECHO.
) ELSE (
	ECHO.
	ECHO Environments for CLOiSim
	ECHO   CLOISIM_WORLD_PATH=%CLOISIM_WORLD_PATH%
	ECHO   CLOISIM_MODEL_PATH=%CLOISIM_MODEL_PATH%
	ECHO   CLOISIM_FILES_PATH=%CLOISIM_FILES_PATH%
	ECHO.
	CLOiSim.exe %HeadlessArgs% -world %TargetWorld%
)

EXIT /B %ERRORLEVEL%

:ShowEnvironmentHelp
	@ECHO OFF
	ECHO.
	ECHO Please set environment path before run the simulation.
	ECHO.
	ECHO   export CLOISIM_FILES_PATH=/home/Unity/cloisim/materials
	ECHO   export CLOISIM_MODEL_PATH=/home/Unity/cloisim/resource/models
	ECHO   export CLOISIM_WORLD_PATH=/home/Unity/cloisim/resource/worlds
	ECHO.
	ECHO Multiple path supports only for 'CLOSISIM_MODEL_PATH'
	ECHO   ex) export CLOISIM_MODEL_PATH=/A/B/models:/A/B/models:/models
	ECHO.
EXIT /B 0