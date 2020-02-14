#r "paket:
nuget FSharp.Core
nuget Fake.Core.Target
nuget Fake.DotNet.MSBuild
nuget Fake.DotNet.NuGet
nuget Newtonsoft.Json
nuget Microsoft.Web.Xdt //"

#load "./.fake/build.fsx/intellisense.fsx"

open System.Xml
open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.DotNet
open Microsoft.Web.XmlTransform
open Newtonsoft.Json.Linq

let BuildDirectory = "./build"

Target.create "Clean" (fun _ ->
    Shell.cleanDir BuildDirectory
)

Target.create "NuGet" (fun _ ->
    NuGet.Restore.RestoreMSSolutionPackages id "Transforms.sln"
)

Target.create "Build" (fun _ ->
    !! "Transforms/*.csproj"
    |> MSBuild.runRelease id BuildDirectory "Build"
    |> Trace.logItems "Build:"
)

Target.create "Transform.Xml" (fun _ ->

    let configPath = BuildDirectory @@ "log4net.config"
    let transformPath = BuildDirectory @@ "log4net.transform.config"

    //Apply the transform in memory
    let config = XmlDocument ()
    config.Load (configPath)

    use transform = new XmlTransformation (transformPath)
    let success = transform.Apply (config)

    if (not success) then
        failwith "Failed to apply XML transform"

    //Replace the original XML with the transformed version
    config.Save (configPath)

    //Remove the transform
    File.delete transformPath

)

Target.create "Transform.Json" (fun _ ->

    let readJson = 
        File.readAsString >> JObject.Parse

    let configPath = BuildDirectory @@ "appsettings.json"
    let transformPath = BuildDirectory @@ "appsettings.transform.json"

    //Apply the transform in memory
    let config = readJson configPath
    let transform = readJson transformPath

    let settings = JsonMergeSettings ()
    settings.MergeArrayHandling <- MergeArrayHandling.Union
    settings.MergeNullValueHandling <- MergeNullValueHandling.Merge

    config.Merge (transform)

    //Replace the original JSON with the transformed version
    File.writeString false configPath (string config)

    //Remove the transform
    File.delete transformPath

)

"Clean"
==> "NuGet"
==> "Build"
==> "Transform.Xml"
==> "Transform.Json"

Target.runOrDefault "Transform.Json"