# std::hash\<string\>\(\)

## for Linux

```shell
mkdir build
cd build
cmake ../
make
make install
rm *
```

## for Windows

```shell
sudo apt-get install mingw-w64-x86-64-dev
mkdir build
cd build
cmake ../ -D CMAKE_CXX_COMPILER=x86_64-w64-mingw32-g++
make
make install
rm *
```
