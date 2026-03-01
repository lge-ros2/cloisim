export CLOISIM_ROOT=/home/nav/cloisim
export CLOISIM_RESOURCES_PATH=${CLOISIM_ROOT}/sample_resources
export CLOISIM_RESOURCES_ROOT_PATH=/home/nav/cloisim
export CLOISIM_MODEL_PATH=${CLOISIM_ROOT}/cloi_resources:${CLOISIM_ROOT}/world_resources/smallhouse/models:${CLOISIM_ROOT}/gazebo_models:${CLOISIM_ROOT}/world_resources/models
export CLOISIM_FILES_PATH=${CLOISIM_ROOT}/world_resources/smallhouse:${CLOISIM_ROOT}/world_resources/meta-home
export CLOISIM_WORLD_PATH=${CLOISIM_ROOT}/world_resources/smallhouse/worlds:${CLOISIM_ROOT}/world_resources/meta-home/worlds


_cloisim_run_completion()
{
  local cur prev opts
  COMPREPLY=()
  cur="${COMP_WORDS[COMP_CWORD]}"
  prev="${COMP_WORDS[COMP_CWORD-1]}"
  opts="--headless --world --server-number --capture-screen -h -w -n -c"

  case "${prev}" in
    --world|-w)
      local world_files=""
      IFS=':' read -ra ADDR <<< "$CLOISIM_WORLD_PATH"
      for dir in "${ADDR[@]}"; do
        if [[ -d "$dir" ]]; then
          for file in "$dir"/*.world; do
            if [[ -f "$file" ]]; then
              world_files+="$(basename "$file") "
            fi
          done
        fi
      done
      COMPREPLY=( $(compgen -W "${world_files}" -- ${cur}) )
      return 0
      ;;
    --server-number|-n)
      return 0
      ;;
    *)
      ;;
  esac

  if [[ ${cur} == -* ]] ; then
    COMPREPLY=( $(compgen -W "${opts}" -- ${cur}) )
    return 0
  fi

  local world_files=""
  IFS=':' read -ra ADDR <<< "$CLOISIM_WORLD_PATH"
  for dir in "${ADDR[@]}"; do
    if [[ -d "$dir" ]]; then
      for file in "$dir"/*.world; do
        if [[ -f "$file" ]]; then
          world_files+="$(basename "$file") "
        fi
      done
    fi
  done
  COMPREPLY=( $(compgen -W "${world_files}" -- ${cur}) )
}

complete -F _cloisim_run_completion ./run.sh
complete -F _cloisim_run_completion run.sh
