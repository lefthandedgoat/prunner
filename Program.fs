module Main

open System
open System.Collections.Generic

type actor<'t> = MailboxProcessor<'t>

type Worker =
  | Run

type Reporter =
  | TestStart of description:string * id:Guid
  | Print of message:string * id:Guid
  | Skip of id:Guid
  | Todo of id:Guid
  | Pass of id:Guid
  | Fail of id:Guid * ex:Exception

let colorWriteReset color message =
  Console.ForegroundColor <- color
  printfn "%s" message
  Console.ResetColor()

let private printError (ex : Exception) =
  colorWriteReset ConsoleColor.Red "Error: "
  printfn "%s" ex.Message
  printfn "%s" "Stack: "
  ex.StackTrace.Split([| "\r\n"; "\n" |], StringSplitOptions.None)
  |> Array.iter (fun trace ->
    Console.ResetColor()
    if trace.Contains(".FSharp.") then
      printfn "%s" trace
    else
      if trace.Contains(":line") then
        let beginning = trace.Split([| ":line" |], StringSplitOptions.None).[0]
        let line = trace.Split([| ":line" |], StringSplitOptions.None).[1]
        printf "%s" beginning
        colorWriteReset ConsoleColor.DarkGreen (":line" + line)
      else
        colorWriteReset ConsoleColor.DarkGreen trace
    Console.ResetColor())

let newReporter () : actor<Reporter> =
  let dict = new Dictionary<Guid, (ConsoleColor * string) list>()
  let printMessages id = dict.[id] |> List.rev |> List.iter (fun (color, message) -> colorWriteReset color message)
  actor.Start(fun self ->
    let rec loop passed failed skipped todo =
      async {
        let! msg = self.Receive ()
        match msg with
        | Reporter.TestStart(description, id) ->
          let message = sprintf "Test: %s" description
          dict.Add(id, [ConsoleColor.DarkCyan, message])
          return! loop passed failed skipped todo
        | Reporter.Print(message, id) ->
          dict.[id] <- (ConsoleColor.Black, message)::dict.[id] //prepend new message
          return! loop passed failed skipped todo
        | Reporter.Pass(id) ->
          printMessages id
          colorWriteReset ConsoleColor.Green "Passed"
          return! loop (passed + 1) failed skipped todo
        | Reporter.Fail(id, ex) ->
          printMessages id
          printError ex
          return! loop passed (failed + 1) skipped todo
        | Reporter.Skip(id) ->
          printMessages id
          colorWriteReset ConsoleColor.Yellow "Skipped"
          return! loop passed failed (skipped + 1) todo
        | Reporter.Todo(id) ->
          printMessages id
          colorWriteReset ConsoleColor.Yellow "Todo"
          return! loop passed failed skipped (todo + 1)
      }
    loop 0 0 0 0)

type TestContext (testId:Guid, reporter : actor<Reporter>) = class
  member x.TestId = testId
  member x.printfn fmtStr = Printf.kprintf (fun msg -> reporter.Post(Print(msg, x.TestId))) fmtStr
end

type Test (description: string, func : (TestContext -> unit), id : Guid) =
  member x.Description = description
  member x.Func = func
  member x.Id = id

type Suite () = class
  member val Context : string = "" with get, set
  member val Tests : Test list = [] with get, set
  member val Wips : Test list = [] with get, set
end

type Manager =
  | Initialize of Suite list
  | Start
  | Run of count:int
  | WorkerDone

let private last = function
  | hd :: _ -> hd
  | [] -> failwith "Empty list."

let private ng() = Guid.NewGuid()

let private reporter = newReporter()
let mutable suites = [new Suite()]
let mutable todo = fun _ -> ()
let mutable skipped = fun _ -> ()

let context c =
  if (last suites).Context = null then
    (last suites).Context <- c
  else
    let s = new Suite()
    s.Context <- c
    suites <- s::suites

let ( &&& ) description f =
    (last suites).Tests <- Test(description, f, ng())::(last suites).Tests
let ( &&&& ) description f =
    (last suites).Wips <- Test(description, f, ng())::(last suites).Wips
let ( &&! ) description _ =
    (last suites).Tests <- Test(description, skipped, ng())::(last suites).Tests

let maxDOP = 30

let private runtest (suite : Suite) (test : Test) =
  reporter.Post(Reporter.TestStart(test.Description, test.Id))
  if System.Object.ReferenceEquals(test.Func, todo) then
    reporter.Post(Reporter.Todo test.Id)
  else if System.Object.ReferenceEquals(test.Func, skipped) then
    reporter.Post(Reporter.Skip test.Id)
  else
    try
      test.Func (TestContext(test.Id, reporter))
      reporter.Post(Reporter.Pass test.Id)
    with ex -> reporter.Post(Reporter.Fail(test.Id, ex))

let newWorker (manager : actor<Manager>) suite test : actor<Worker> =
  actor.Start(fun self ->
    let rec loop () =
      async {
        let! msg = self.Receive ()
        match msg with
        | Worker.Run ->
          runtest suite test
          manager.Post(Manager.WorkerDone)
          return ()
      }
    loop ())

let newManager () : actor<Manager> =
  let sw = System.Diagnostics.Stopwatch.StartNew()
  actor.Start(fun self ->
    let rec loop workers =
      async {
        let! msg = self.Receive ()
        match msg with
        | Manager.Initialize (suites) ->
          //build a worker per suite/test combo and give them their work
          let workers = suites |> List.map (fun suite -> suite.Tests |> List.map (fun test -> newWorker self suite test)) |> List.concat |> List.rev
          return! loop workers
        | Manager.Start ->
          //kick off the initial X workers
          self.Post(Manager.Run maxDOP)
          return! loop workers
        | Manager.Run(count) ->
          if count = 0 then
            return! loop workers
          else
            match workers with
            | [] -> printfn "Done no more tests! in %A" sw.Elapsed.TotalSeconds; return ()
            | head :: tail ->
              head.Post(Worker.Run)
              self.Post(Manager.Run(count - 1))
              return! loop tail
        | Manager.WorkerDone ->
          self.Post(Manager.Run(1))
          return! loop workers
      }
    loop [])

let run () =
  let stopWatch = Diagnostics.Stopwatch.StartNew
  // suites list is in reverse order and have to be reversed before running the tests
  suites <- List.rev suites
  let manager = newManager()
  manager.Post(Manager.Initialize(suites))
  manager.Post(Manager.Start)

//demo
context "Test Context"


"Skipped test" &&! fun ctx ->
  ()

"Todo test" &&& todo

[1..50]
|> List.iter (fun i ->
  sprintf "Test %i" i &&& fun ctx ->
    ctx.printfn "A guid %A" (ng())
    ctx.printfn "I am test %i" i
    if i % 10 = 0 then failwith "mod error"
    ctx.printfn "A guid %A" (ng()))

run()

System.Console.ReadKey() |> ignore
