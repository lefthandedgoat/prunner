namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("prunner")>]
[<assembly: AssemblyProductAttribute("prunner")>]
[<assembly: AssemblyDescriptionAttribute("Super simple f# parallel unit test library")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
