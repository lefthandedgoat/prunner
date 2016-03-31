module Main

open System
open System.Collections.Generic
open prunner

//demo
context "Test Context"

"Skipped test" &&! fun _ -> ()

"Todo test" &&& todo

let random = System.Random()
[1..100]
|> List.iter (fun i ->
  sprintf "Test %i" i &&& fun ctx ->
    ctx.printfn "I am test %i" i
    let sleepTime =
      if i % 10 = 0 then
        40 * 1000
      else
        random.Next(1, 5) * 100

    //ctx.sleep sleepTime
    System.Threading.Thread.Sleep sleepTime
    if i % 10 = 0 then failwith "intentional mod error"
    ctx.printfn "A guid %A" (Guid.NewGuid())
    1 == 1
    "cat" != "dog")

context "Test Context2"

"Skipped test 2" &&! fun _ -> ()

"Todo test 2" &&& todo

let maxDOP = 12
let failedTest = run maxDOP
printfn "final failed count %A" failedTest
