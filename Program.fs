module Main

open System
open System.Collections.Generic
open prunner

//demo
context "Test Context"

"Skipped test" &&! fun _ -> ()

"Todo test" &&& todo

[1..11]
|> List.iter (fun i ->
  sprintf "Test %i" i &&& fun ctx ->
    ctx.printfn "I am test %i" i
    if i % 10 = 0 then failwith "intentional mod error"
    ctx.printfn "A guid %A" (Guid.NewGuid()))

context "Test Context2"

"Skipped test 2" &&! fun _ -> ()

"Todo test 2" &&& todo

let maxDOP = 100
let failedTest = run maxDOP
printfn "final failed count %A" failedTest
