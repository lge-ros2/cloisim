#!/bin/bash

GenerateCompletion()
{
  cat << 'COMPLETION_EOF'
_cloisim_run_completion()
{
  local cur prev opts
  COMPREPLY=()
  cur="${COMP_WORDS[COMP_CWORD]}"
  prev="${COMP_WORDS[COMP_CWORD-1]}"
  opts="--headless --world --capture-screen --install-completion -h -w -c"

  case "${prev}" in
    --world|-w)
      local world_files=""
      IFS=':' read -ra ADDR <<< "${CLOISIM_WORLD_PATH}"
      for dir in "${ADDR[@]}"; do
        if [[ -d "${dir}" ]]; then
          for file in "${dir}"/*.world; do
            if [[ -f "${file}" ]]; then
              world_files+="$(basename "${file}") "
            fi
          done
        fi
      done
      COMPREPLY=( $(compgen -W "${world_files}" -- "${cur}") )
      return 0
      ;;
    *)
      ;;
  esac

  if [[ "${cur}" == -* ]]; then
    COMPREPLY=( $(compgen -W "${opts}" -- "${cur}") )
    return 0
  fi

  local world_files=""
  IFS=':' read -ra ADDR <<< "${CLOISIM_WORLD_PATH}"
  for dir in "${ADDR[@]}"; do
    if [[ -d "${dir}" ]]; then
      for file in "${dir}"/*.world; do
        if [[ -f "${file}" ]]; then
          world_files+="$(basename "${file}") "
        fi
      done
    fi
  done
  COMPREPLY=( $(compgen -W "${world_files}" -- "${cur}") )
}

complete -F _cloisim_run_completion ./run.sh
complete -F _cloisim_run_completion run.sh
COMPLETION_EOF
}

InstallCompletion()
{
  local COMP_DIR="${HOME}/.local/share/bash-completion/completions"
  local COMP_FILE="${COMP_DIR}/run.sh"

  mkdir -p "${COMP_DIR}"
  GenerateCompletion > "${COMP_FILE}"

  echo "Bash completion installed to ${COMP_FILE}"
  echo "Open a new terminal to enable auto-completion."
}

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
  echo -e "\n\n<Possible all world list in CLOISIM_WORLD_PATH>\n"

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
  echo -e "\n Install bash auto-completion (one-time):"
  echo -e "     ./run.sh --install-completion"
  echo -e "\n <Possible world file list>\n"
  PrintWorldList
else
  targetWorld=""
  enableHeadless=false
  captureScreen=false

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
      -c|--capture-screen)
        echo "Option --capture-screen was specified!!"
        captureScreen=true
        shift ;;
      --install-completion)
        InstallCompletion
        exit 0
        ;;
      *)
        if [ -z "$targetWorld" ]; then
          targetWorld=$1
          shift
        else
          echo "Unknown option: $1"
          echo "Possible options: -h|--headless, -w|--world, -c|--capture-screen, --install-completion"
          exit 1
        fi ;;
    esac
  done

  if [ -z "$targetWorld" ]; then
    echo -e "\n  Pass the world file name as a 1st argument"
    echo -e "   ex) ./run.sh lg_seocho.world"
    echo -e "   ex) ./run.sh --world lg_seocho.world"
    echo -e "\n\n"
    PrintWorldList
  else
    if WorldValidationCheck $targetWorld ; then
      headlessArgs=()
      if $enableHeadless ; then
        headlessArgs=("-batchmode")
      fi

      captureArgs=()
      if $captureScreen ; then
        captureApps=("ffmpeg")
        InstallApps "${captureApps[@]}"
        captureFileName=simulation_$(date '+%Y%m%d%H%M%S')
        captureArgs=("-capture" "${captureFileName}")
      fi

      CollectCrashDump()
      {
        local EXIT_CODE=$1
        local TIMESTAMP=$(date '+%Y%m%d_%H%M%S')
        local DUMP_DIR="./CrashDumps/crash_shell_${TIMESTAMP}"

        echo ""
        echo "========================================"
        echo " CLOiSim exited with code: ${EXIT_CODE}"
        echo " Collecting crash dump..."
        echo "========================================"

        mkdir -p "${DUMP_DIR}"

        # 1. Copy Unity CrashDumps (generated by CrashReporter.cs)
        if [[ -d "./CrashDumps" ]]; then
          for d in ./CrashDumps/crash_2*; do
            if [[ -d "$d" && "$d" != "${DUMP_DIR}" ]]; then
              cp -r "$d" "${DUMP_DIR}/$(basename "$d")"
            fi
          done
        fi

        # 2. Collect core dump if available
        local CORE_FILE=$(find . -maxdepth 1 -name 'core*' -newer ./CLOiSim.x86_64 -type f 2>/dev/null | head -1)
        if [[ -n "${CORE_FILE}" ]]; then
          mv "${CORE_FILE}" "${DUMP_DIR}/"
        fi

        # 3. Save environment and system info
        {
          echo "Exit Code         : ${EXIT_CODE}"
          echo "Crash Time        : $(date '+%Y-%m-%d %H:%M:%S')"
          echo "World File        : ${targetWorld}"
          echo "Headless          : ${enableHeadless}"
          echo "OS                : $(uname -a)"
          echo "GPU               : $(lspci 2>/dev/null | grep -i vga || echo 'N/A')"
          echo "CLOISIM_FILES_PATH: ${CLOISIM_FILES_PATH}"
          echo "CLOISIM_MODEL_PATH: ${CLOISIM_MODEL_PATH}"
          echo "CLOISIM_WORLD_PATH: ${CLOISIM_WORLD_PATH}"
        } > "${DUMP_DIR}/env_info.txt"

        # 4. Package into tar.gz
        local ARCHIVE="./CrashDumps/crash_${TIMESTAMP}.tar.gz"
        tar -czf "${ARCHIVE}" -C "./CrashDumps" "crash_shell_${TIMESTAMP}"

        echo ""
        echo " Crash dump saved: ${ARCHIVE}"
        echo " Dump directory  : ${DUMP_DIR}"
        echo "========================================"
      }

      ./CLOiSim.x86_64 "${headlessArgs[@]}" -world "$targetWorld" "${captureArgs[@]}"
      CLOISIM_EXIT_CODE=$?

      if [[ ${CLOISIM_EXIT_CODE} -ne 0 ]]; then
        CollectCrashDump ${CLOISIM_EXIT_CODE}
      fi
    else
      echo -e "\n Invalid world file name or World file NOT exist.\n"
      PrintWorldList
    fi
  fi
fi
