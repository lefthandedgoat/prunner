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

[types.fs] (https://github.com/lefthandedgoat/prunner/blob/master/src/prunner/types.fs)  
[actors.fs] (https://github.com/lefthandedgoat/prunner/blob/master/src/prunner/actors.fs)  
[test.fs] (https://github.com/lefthandedgoat/prunner/blob/master/src/prunner/test.fs)  
[printer.fs] (https://github.com/lefthandedgoat/prunner/blob/master/src/prunner/printer.fs)  

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

Output:

```
context: Test Context
context: Test Context2
Test: Skipped test
Skipped
Test: Todo test
Todo
Test: Test 3
I am test 3
A guid e5aeeb3b-1204-477b-b510-4c4c6c069efb
Passed
Test: Test 5
I am test 5
A guid 79765c67-c147-4fac-938e-d236212c51fa
Passed
Test: Test 1
I am test 1
A guid aace6635-4bd2-4d8d-856d-8768dc799058
Passed
Test: Test 7
I am test 7
A guid 1e3ced6e-3c4b-4e1b-8e7b-18a9fbd49d7a
Passed
Test: Skipped test 2
Skipped
Test: Todo test 2
Todo
Test: Test 4
I am test 4
A guid e659d390-373d-4728-8486-f420a2ed2358
Passed
Test: Test 8
I am test 8
A guid 065338de-a0d6-4ce1-904d-ec96e65e5941
Passed
Test: Test 10
I am test 10
Error: 
intentional mod error
Stack: 
  at Main+clo@16-2.Invoke (prunner.TestContext ctx) <0x79edd0 + 0x000a7> in <filename unknown>:0 
  at prunner.actors.runtest (prunner.Test test) <0x79cc88 + 0x00257> in <filename unknown>:0 
Test: Test 2
I am test 2
A guid 72b157dc-25fc-4ddb-9105-377ce6b176fb
Passed
Test: Test 6
I am test 6
A guid 8fa065ad-cd96-4dc8-be7d-d73c0f19fca5
Passed
Test: Test 9
I am test 9
A guid 1212d01c-351a-4f82-896a-f83671592abe
Passed
Test: Test 11
I am test 11
A guid 9965f4e9-bc83-4b33-a916-35fd333cc33a
Passed
context end: Test Context2
context end: Test Context

0 minutes 0 seconds to execute
10 passed
2 skipped
2 todo
1 failed
final failed count 1
```
