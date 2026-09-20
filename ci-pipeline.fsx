#!/usr/bin/env -S dotnet fsi
// ci-pipeline.fsx — THIS SCRIPT IS THE CI PIPELINE for SageFs.Harmony (our build of Harmony that uses the
// patched MonoMod fork, so detours work on .NET 11). GitHub Actions (.github/workflows/ci.yml) only installs the
// SDKs and calls this; run the same thing locally:
//
//   dotnet fsi ci-pipeline.fsx
//
// Stages: build -> probe on .NET 10 -> probe on the newest .NET 11 -> pack. SageFs consumes this repo by cloning it
// at a pinned commit and packing it into a local folder feed (its ci-pipeline.fsx), so nothing is published. It also runs weekly: a newer .NET 11 RC/GA can change the JIT interface and break
// detours, and a red weekly run is the signal to regenerate the layout in the MonoMod fork.

#r "nuget: Fun.Build, 1.2.0"

open Fun.Build

let probeDll = "build/harmony-probe/bin/Release/net10.0/harmony-probe.dll"
let nupkgDir = "artifacts/nupkg"

pipeline "sagefs-harmony" {
  description "Build, prove detours on .NET 10 and 11, pack"

  stage "build" {
    workingDir __SOURCE_DIRECTORY__
    run "dotnet build Lib.Harmony/Lib.Harmony.csproj -c Release"
  }

  stage "build probe" {
    workingDir __SOURCE_DIRECTORY__
    run "dotnet build build/harmony-probe -c Release"
  }

  stage "patch on .NET 10" {
    workingDir __SOURCE_DIRECTORY__
    run $"dotnet {probeDll} 10"
  }

  stage "patch on newest .NET 11" {
    workingDir __SOURCE_DIRECTORY__
    envVars [ "DOTNET_ROLL_FORWARD", "LatestMajor"; "DOTNET_ROLL_FORWARD_TO_PRERELEASE", "1" ]
    run $"dotnet {probeDll} 11"
  }

  stage "pack" {
    workingDir __SOURCE_DIRECTORY__
    run $"dotnet pack Lib.Harmony/Lib.Harmony.csproj -c Release --no-build -o {nupkgDir}"
  }

  runIfOnlySpecified false
}
