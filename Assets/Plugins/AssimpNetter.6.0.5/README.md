![alt text](https://raw.githubusercontent.com/Saalvage/AssimpNetter/master/logo.png "AssimpNet Logo")

**The latest release can be downloaded via [NuGet](https://www.nuget.org/packages/AssimpNetter/).**

[![Continuous Integration](https://github.com/Saalvage/AssimpNetter/actions/workflows/ci.yml/badge.svg)](https://github.com/Saalvage/AssimpNetter/actions/workflows/ci.yml)

## Introduction ##
This is a maintained, modern fork of [**AssimpNet**](https://bitbucket.org/Starnick/assimpnet/), the cross-platform .NET wrapper for the Open Asset Import Library (otherwise known as [Assimp](https://github.com/assimp/assimp)), which is a 3D model import-export library. The primary motivation is for this library to power (offline) content pipelines to import and process 3D models into your game engine's internal format, although the wrapper can be used at runtime to enable your users to import custom content. Please see the Assimp website for a full list of supported formats and features. Each version of the managed wrapper tries to maintain parity with the features of the native version.

P/Invoke is used to communicate with the C-API of the native library. The managed assembly is compiled as **AnyCpu** and the native binaries are loaded dynamically for either 32 or 64 bit x86, as well as ARM64 applications.

The library is split between two parts, a low level and a high level. The intent is to give as much freedom as possible to the developer to work with the native library from managed code.

### Low level ###

* Native methods are exposed via the AssimpLibrary singleton.
* Structures corresponding to unmanaged structures are prefixed with the name **Ai** and generally contain IntPtrs to the unmanaged data.
* Located in the *Assimp.Unmanaged* namespace.

### High level ###

* Replicates the native library's C++ API, but in a way that is more familiar to C# developers.
* Marshaling to and from managed memory handled automatically, all you need to worry about is processing your data.
* Located in the *Assimp* namespace.

## Supported Frameworks ##

The library supports **.NET >= 4.7.2** and **.NET Standard 2.1**.

## Supported Platforms ##

The NuGet package supports the following Operating Systems and Architectures out of the box (located in the *runtimes* folder, under [RID](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)-specific folders):

* **Windows**
	* x86, x64 (Tested on Windows 10), ARM64
* **Linux**
	* x64, ARM64 (Tested on Ubuntu 24.04 Noble Numbat)
* **MacOS**
	* x64 (Tested on MacOS 13.7 Ventura), ARM64 (Tested on MacOS 14.7 Sonoma)

You may have to build and provide your own native binaries for a target platform that is not listed. If the library does not support a platform you are targeting, please let us know or contribute an implementation! The logic to dynamically load the native library is abstracted, so new platform implementations can easily be added.

All included binaries are built transparently using GitHub Actions on [this fork](https://github.com/Saalvage/assimp/).

## Questions & Contributing ##

All contributions are welcome! Simply raise an issue or open a pull request with your issue/question or contribution!

## Unity Users ##

There existed support for a Unity plugin, however, it is currently in a non-functional state. Pull requests to restore this support would be welcomed!

## Licensing ##

The library is licensed under the [MIT](https://opensource.org/licenses/MIT) license. This means you're free to modify the source and use the library in whatever way you want, as long as you attribute the original authors. The native library is licensed under the [3-Clause BSD](https://opensource.org/licenses/BSD-3-Clause) license. Please be kind enough to include the licensing text file (it contains both licenses).

## More Projects by the Original Author ##

[TeximpNet](https://bitbucket.org/Starnick/teximpnet) - A wrapper for the Nvidia Texture Tools and FreeImage libraries.

[Tesla Graphics Engine](https://bitbucket.org/Starnick/tesla3d) - A 3D rendering engine written in C# and the primary driver for developing AssimpNet.
