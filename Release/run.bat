@ECHO OFF
SET TargetWorld=%1

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
	ECHO > run.bat lg_seocho.world
	ECHO. 
) ELSE (
	ECHO.
	ECHO Environments for CLOiSim
	ECHO   CLOISIM_WORLD_PATH=%CLOISIM_WORLD_PATH%
	ECHO   CLOISIM_MODEL_PATH=%CLOISIM_MODEL_PATH%
	ECHO   CLOISIM_FILES_PATH=%CLOISIM_FILES_PATH%
	ECHO.
	CLOiSim.exe -world %TargetWorld%
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