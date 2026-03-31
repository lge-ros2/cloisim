#!/bin/bash
set -eu

PREFIX_PATH=/opt/resources

map_and_mount_paths() {
  local raw="$1"
  local joined=""
  local joined_mount=""
  local -n output_env="$2"
  local -n output_mounts="$3"

  IFS=':' read -r -a arr <<< "$raw"
  for p in "${arr[@]}"; do
    [[ -z "$p" ]] && continue
    joined=("$PREFIX_PATH$p:${joined}")
    joined_mount=("-v $p:$PREFIX_PATH$p:ro ${joined_mount}")
  done

  output_env=${joined%:}
  output_mounts=${joined_mount}
}

CLOISIM_RESOURCES_PATH="${CLOISIM_RESOURCES_PATH:-}"

ENV_MODEL_ARGS=""
ENV_WORLD_ARGS=""
ENV_FILES_ARGS=""

if [[ -n "$CLOISIM_RESOURCES_PATH" ]]; then
  ENV_MODEL_ARGS="-v ${CLOISIM_RESOURCES_PATH}/models:${PREFIX_PATH}/models:ro"
  ENV_WORLD_ARGS="-v ${CLOISIM_RESOURCES_PATH}/worlds:${PREFIX_PATH}/worlds:ro"
  ENV_FILES_ARGS="-v ${CLOISIM_RESOURCES_PATH}/materials:${PREFIX_PATH}/materials:ro"
fi

out_env=""
out_mounts=""

if [[ -n "${CLOISIM_MODEL_PATH:-}" ]]; then
  map_and_mount_paths $CLOISIM_MODEL_PATH out_env out_mounts
  ENV_MODEL_ARGS="-e CLOISIM_MODEL_PATH=$out_env $out_mounts"
fi

if [[ -n "${CLOISIM_WORLD_PATH:-}" ]]; then
  map_and_mount_paths $CLOISIM_WORLD_PATH out_env out_mounts
  ENV_WORLD_ARGS="-e CLOISIM_WORLD_PATH=$out_env $out_mounts"
fi

if [[ -n "${CLOISIM_FILES_PATH:-}" ]]; then
  map_and_mount_paths $CLOISIM_FILES_PATH out_env out_mounts
  ENV_FILES_ARGS="-e CLOISIM_FILES_PATH=$out_env $out_mounts"
fi

# echo "OUTPUT="$ENV_MODEL_ARGS
# echo "OUTPUT="$ENV_WORLD_ARGS
# echo "OUTPUT="$ENV_FILES_ARGS

docker run -it --rm --init --net=host --ipc=host --gpus device=0 \
  -v /tmp/cloisim/unity3d:/root/.config/unity3d \
  -v /usr/share/fonts/:/usr/share/fonts/:ro \
  $ENV_MODEL_ARGS \
  $ENV_WORLD_ARGS \
  $ENV_FILES_ARGS \
  --entrypoint xvfb-run \
  cloisim \
  -a -s "-screen 0 1280x1024x24 -nolisten local" ./run.sh --headless -w $@
