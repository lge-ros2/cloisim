# Scripts for Release

## Environment Setup

Before running CLOiSim, export the required environment variables:

```bash
export CLOISIM_FILES_PATH=/path/to/cloisim/materials
export CLOISIM_MODEL_PATH=/path/to/models:/other/models
export CLOISIM_WORLD_PATH=/path/to/worlds
```

## Auto-Completion (Optional)

### Linux (Bash)

Install tab-completion for `./run.sh` options and world file names (one-time):

```bash
./run.sh --install-completion
```

Open a new terminal and Tab will auto-complete options and `.world` file names.

#### example

```bash
$ ./run.sh -- (tab)
--capture-screen      --install-completion  
--headless            --world               

$ ./run.sh --world (tab)
empty.world                    slg.world
house.world                    small_house_with_actors.world
hump.world                     small_house_with_nr.world
lawn_ground.world              small_house_with_r7.world
lg_seocho_with_actors.world    small_house.world
lg_seocho.world                woorizip.world
logistics_multi.world          yangjae.world
mini_logistics.world
```

### Windows (PowerShell)

Install tab-completion for `run.bat` in PowerShell (one-time):

```bat
run.bat --install-completion
```

Open a new PowerShell terminal and Tab will auto-complete options and `.world` file names.
Note: Auto-completion is not supported in cmd.exe.

## Running the Simulator

### Linux

```bash
./run.sh lg_seocho.world
./run.sh --world lg_seocho.world --headless
```

### Windows

```bat
run.bat lg_seocho.world
run.bat --world lg_seocho.world --headless
```


