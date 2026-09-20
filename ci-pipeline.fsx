#!/usr/bin/env -S dotnet fsi
// ci-pipeline.fsx — THIS SCRIPT IS THE CI PIPELINE for SageFs.Harmony (our build of Harmony that uses the
// patched MonoMod fork, so detours work on .NET 11). GitHub Actions (.github/workflows/ci.yml) only installs the
// SDKs and calls this; run the same thing locally:
//
//   dotnet fsi ci-pipeline.fsx
//
// Stages: build -> probe on .NET 10 -> probe on the newest .NET 11 -> pack -> (master push only) publish the
// package to GitHub Packages. It also runs weekly: a newer .NET 11 RC/GA can change the JIT interface and break
// detours, and a red weekly run is the signal to regenerate the layout in the MonoMod fork.

#r "nuget: Fun.Build, 1.2.0"

open System
open Fun.Build

let probeDll = "build/harmony-probe/bin/Release/net10.0/harmony-probe.dll"
let nupkgDir = "artifacts/nupkg"
let feed = "https://nuget.pkg.github.com/WillEhrendreich/index.json"

let onMasterPush =
  Environment.GetEnvironmentVariable "GITHUB_REF" = "refs/heads/master"
  && Environment.GetEnvironmentVariable "GITHUB_EVENT_NAME" = "push"

pipeline "sagefs-harmony" {
  description "Build, prove detours on .NET 10 and 11, pack, publish"

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

  stage "publish to GitHub Packages" {
    workingDir __SOURCE_DIRECTORY__
    run (fun ctx ->
      async {
        match onMasterPush with
        | false ->
          printfn "not a master push: skipping publish"
          return Ok()
        | true ->
          let token = Environment.GetEnvironmentVariable "GITHUB_TOKEN"
          return! ctx.RunCommand $"dotnet nuget push \"{nupkgDir}/*.nupkg\" --source {feed} --api-key {token} --skip-duplicate"
      })
  }

  runIfOnlySpecified false
}
