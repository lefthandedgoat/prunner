module Main

open System
open System.Collections.Generic

type actor<'t> = MailboxProcessor<'t>

type Worker =
  | Run

type Reporter =
  | TestStart of id:Guid
  | Print of message:string * id:Guid
  | TestEnd of id:Guid

let newReporter () : actor<Reporter> =
  let dict = new Dictionary<Guid, string list>()
  actor.Start(fun self ->
    let rec loop () =
      async {
        let! msg = self.Receive ()
        match msg with
        | Reporter.TestStart(id) ->
          dict.Add(id, [])
          return! loop ()
        | Reporter.Print(message, id) ->
          dict.[id] <- message::dict.[id] //prepend new message
          return! loop ()
        | Reporter.TestEnd(id) ->
          dict.[id] |> List.rev |> List.iter (fun message -> printfn "%s" message)
          printfn "Passed"
          return! loop ()
      }
    loop ())

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
let mutable todo = fun () -> ()
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
  reporter.Post(Reporter.TestStart test.Id)
  //if System.Object.ReferenceEquals(test.Func, todo) then
  //  ()
  //  //reporter.todo ()
  //else if System.Object.ReferenceEquals(test.Func, skipped) then
  //  ()
  //  //skip test.Id
  //else
  //  ()
  test.Func (TestContext(test.Id, reporter))
    //tryTest test suite (suite.Before >> test.Func)
    //tryTest test suite (suite.After >> pass)

  reporter.Post(Reporter.TestEnd test.Id)

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
          let workers = suites |> List.map (fun suite -> suite.Tests |> List.map (fun test -> newWorker self suite test)) |> List.concat
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

[1..200]
|> List.iter (fun i ->
  sprintf "Test %i" i &&& fun ctx ->
    ctx.printfn "A guid %A" (ng())
    ctx.printfn "I am test %i" i
    ctx.printfn "A guid %A" (ng())
    ())

run()

System.Console.ReadKey() |> ignore
