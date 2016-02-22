# prunner
Super simple f# parallel unit test library

Why?  Integration tests at work were taking too long and I wanted a runner that could do parallel that was api compatible with canopy and correctly group console output.

The level of concurrency is passed in when you run the tests.  Level 1 would run like a normal single threaded runner.  Increase to your liking depending on how beefy your machine is!

Its only 4 files, with lots of comments so it doubles as a good learning resource for these cool F# features:
```
Actors
Parallelism/Concurrency
Discriminated Unions
Type Aliasing
Pattern Matching
Functions
Nested Functions
Values
Records
Tuples
Classes
```

Example:
```
context "Test Context"

"Skipped test" &&! fun _ -> ()

"Todo test" &&& todo

[1..11]
|> List.iter (fun i ->
  sprintf "Test %i" i &&& fun ctx ->
    ctx.printfn "I am test %i" i
    if i % 10 = 0 then failwith "intentional mod error"
    ctx.printfn "A guid %A" (Guid.NewGuid())
    1 == 1
    "cat" != "dog")

let maxDOP = 10
let failedTestCount = run maxDOP
printfn "final failed count %A" failedTestCount
```
