#!/bin/bash

ShowEnvironmentHelp()
{
  echo ""
  echo "Please set environment path before run the simulation."
  echo "  export CLOISIM_FILES_PATH=/home/Unity/cloisim/materials"
  echo "  export CLOISIM_MODEL_PATH=/home/Unity/cloisim/resource/models"
  echo "  export CLOISIM_WORLD_PATH=/home/Unity/cloisim/resource/worlds"
  echo ""
  echo "Multiple path supports only for 'CLOSISIM_MODEL_PATH'"
  echo "  ex) export CLOISIM_MODEL_PATH=/A/B/models:/A/B/models:/models"
  echo ""
}

PrintWorldList()
{
  echo -e "<Possible all world list in CLOISIM_WORLD_PATH>\n"

  IFS=':'
  read -ra newarr <<< "$CLOISIM_WORLD_PATH"

  for val in "${newarr[@]}";
  do
    echo "[$val]"
    for file in "$val"/*.world; do
      if [ -f "$file" ]; then
        echo " -> `basename $file`"
      fi
    done
  done
}

WorldValidationCheck()
{
  local targetWorldFilename=$1
  # echo "WorldValidationCheck=$1"

  IFS=':'
  read -ra newarr <<< "$CLOISIM_WORLD_PATH"

  for val in "${newarr[@]}";
  do
    for file in "$val"/*.world; do
      # echo $file
      if [[ "$targetWorldFilename" == `basename $file` ]]; then
        # echo " -> $targetWorldFilename exist"
        true
        return
      fi
    done
  done

  # echo " '$targetWorldFilename' NOT exist!! check path or world file name"
  false
  return
}

InstallApps()
{
  local APPS=( "$@" )

  for APP in "${APPS[@]}"; do
    if ! ( dpkg -l | grep -q $APP ); then
      sudo apt update -y -qq
      sudo apt install -y -qq $APP
      if [ $? -eq 0 ]; then
        echo " => $APP has been successfully installed."
      else
        echo "Failed to install [$APP]."
      fi
    # else
    #   echo "$APP is installed."
    fi
  done
}

if [ -z "$CLOISIM_WORLD_PATH" ]; then
  echo "  'CLOISIM_WORLD_PATH' is empty"
  ShowEnvironmentHelp
elif [ -z "$CLOISIM_MODEL_PATH" ]; then
  echo "  'CLOISIM_MODEL_PATH' is empty"
  ShowEnvironmentHelp
elif [ -z "$CLOISIM_FILES_PATH" ]; then
  echo "  'CLOISIM_FILES_PATH' is empty"
  ShowEnvironmentHelp
fi

echo "[Environments for CLOiSim]"
echo " CLOISIM_WORLD_PATH=$CLOISIM_WORLD_PATH"
echo " CLOISIM_MODEL_PATH=$CLOISIM_MODEL_PATH"
echo " CLOISIM_FILES_PATH=$CLOISIM_FILES_PATH"
echo ""

if [ $# -eq 0  ]; then
  echo -e "  Pass the world file name as a 1st argument"
  echo -e "     ex) ./run.sh lg_seocho.world"
  echo -e "\n OR with options"
  echo -e "     ex) ./run.sh --world"
  echo -e "     ex) ./run.sh --world empty.world --headless"
  echo -e "     ex) ./run.sh --headless --world empty.world --capture-screen"
  echo -e "\n <Possible world file list>\n"
  PrintWorldList
else
  displaySize="1024x768"
  serverNumber="99"
  targetWorld=""
  enableHeadless=false
  captureScreen=false

  if [ $# -eq 1 ]; then
    targetWorld=$1
    # targetWorld=${1:-"empty.world"}
  else

    while [[ $# -gt 0 ]]; do
      case "$1" in
        -h|--headless)
          echo "Option --headless was specified!!"
          enableHeadless=true
          shift ;;
        -w|--world)
          if [[ -n $2 && $2 != -* ]]; then
            echo "Option --world has value: $2"
            targetWorld=$2
            shift 2
          else
            echo "world file required"
            PrintWorldList
            exit 1
          fi ;;
        -n|--server-number)
          if [[ -n $2 && $2 != -* ]]; then
            echo "Option --server-number has value: $2"
            serverNumber=$2
            shift 2
          else
            echo "Server number required.  ex) 99, 88, 100"
            exit 1
          fi ;;
        -c|--capture-screen)
          echo "Option --capture-screen was specified!!"
          captureScreen=true
          shift ;;
        *)
          echo "Unknown option: $1"
          echo "Possible options: -h|--headless, -w|--world, -n|--server-number, -c|--capture-screen"
          exit 1 ;;
      esac
    done
  fi

  if [ -z "$targetWorld" ]; then
    echo -e "\n  Pass the world file name as a 1st argument"
    echo -e "   ex) ./run.sh lg_seocho.world"
    echo -e "   ex) ./run.sh --world lg_seocho.world"
    echo -e "\n\n"
    PrintWorldList
  else
    if WorldValidationCheck $targetWorld ; then
      if $enableHeadless ; then
        headlessApps=("libglu1" "xvfb" "libxcursor1")
        InstallApps "${headlessApps[@]}"
        Xvfb :${serverNumber} -screen 0 ${displaySize}x24:32 -nolisten tcp &
        export DISPLAY=:$serverNumber
      fi

      if $captureScreen ; then
	  	captureApps=("ffmpeg")
        InstallApps "${captureApps[@]}"
        captureFileName=capture_$(date '+%Y%m%d%H%M%S').mp4
        echo "Start screen capture: "${captureFileName}
        ffmpeg -y  -an -video_size ${displaySize} -framerate 10 -threads 4 -f x11grab -i :${serverNumber} -vcodec rawvideo ${captureFileName} &
      fi

      ./CLOiSim.x86_64 -world $targetWorld

      if $captureScreen ; then
        pkill -SIGINT -f ffmpeg
      fi
    else
      echo -e "\n Invalid world file name or World file NOT exist.\n"
      PrintWorldList
    fi
  fi
fi
