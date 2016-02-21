[<AutoOpen>]
module prunner.actors

open System
open System.Collections.Generic
open prunner

(*
   Function as constructor, Pattern Matching, Destructuring,
   Tuples, Function within a function (printMessages)

   This function will create an actor that recieves Reporter messages.
   Some messages are directly printed like Context Start.  Other messages
   like Print are queued up for a test, and once the test Pass/Fail
   all of the messages will be printed as a group.

   Destructuring can be seen here `Reporter.TestStart(description, id)`
   TestStart takes a tuple of a string and a guid, and it is destructured
   into its two pieces, instead of left whole as a tuple.
*)
let newReporter () : actor<Reporter> =
  let messages = new Dictionary<Guid, (color * string) list>()
  let printMessages id = messages.[id] |> List.rev |> List.iter (fun (color, message) -> colorWriteReset color message)
  actor.Start(fun self ->
    let rec loop passed failed skipped todo =
      async {
        let! msg = self.Receive ()
        match msg with
        | ContextStart description ->
            let message = sprintf "context: %s" description
            colorWriteReset color.DarkYellow message
            return! loop passed failed skipped todo
        | ContextEnd description ->
            let message = sprintf "context end: %s" description
            colorWriteReset color.DarkYellow message
            return! loop passed failed skipped todo
        | Reporter.TestStart(description, id) ->
            let message = sprintf "Test: %s" description
            messages.Add(id, [color.DarkCyan, message])
            return! loop passed failed skipped todo
        | Reporter.Print(message, id) ->
            messages.[id] <- (color.Black, message)::messages.[id] //prepend new message
            return! loop passed failed skipped todo
        | Reporter.Pass id ->
            printMessages id
            colorWriteReset color.Green "Passed"
            return! loop (passed + 1) failed skipped todo
        | Reporter.Fail(id, ex) ->
            printMessages id
            printError ex
            return! loop passed (failed + 1) skipped todo
        | Reporter.Skip id ->
            printMessages id
            colorWriteReset color.Yellow "Skipped"
            return! loop passed failed (skipped + 1) todo
        | Reporter.Todo id ->
            printMessages id
            colorWriteReset color.Yellow "Todo"
            return! loop passed failed skipped (todo + 1)
        | Reporter.RunOver (minutes, seconds, replyChannel) ->
            printfn ""
            printfn "%i minutes %i seconds to execute" minutes seconds
            colorWriteReset color.Green (sprintf "%i passed" passed)
            colorWriteReset color.Yellow (sprintf "%i skipped" skipped)
            colorWriteReset color.Yellow (sprintf "%i todo" todo)
            colorWriteReset color.Red (sprintf "%i failed" failed)
            replyChannel.Reply failed
            return! loop passed failed skipped todo
      }
    loop 0 0 0 0)

(*
   Value
   We only need one reporter.
*)
let reporter = newReporter()

(*
   Private Function
   The logic to run a test is big enough to extract out of the
   worker actor and into a private helper function.
*)
let private runtest (test : Test) =
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

(*
   Function as constructor and Pattern Matching
   Workers wait until they recieve a message to run and then they
   run the test that was passed in during construction
   After they call 'return ()' which will kill the actor.
*)
let newWorker (manager : actor<Manager>) (suite:Suite) test : actor<Worker> =
  actor.Start(fun self ->
    let rec loop () =
      async {
        let! msg = self.Receive ()
        match msg with
        | Worker.Run ->
            runtest test
            manager.Post(Manager.WorkerDone(suite.Context, self))
            return ()
      }
    loop ())

(*
   Function as constructor and Pattern Matching
   The most complex of the actors, and where the majority of
   real logic is.
   Manager.Initialize:
     Create a list of workers for each suite/test
     Creata a list of workers for each suite/wip
     Loop with workers for regular test if there are no wips, otherwise with wips.
   Manager.Start:
     Take in a replyChannel for future use and pass it as part of the loop.
     Make a post to self with maxDOP which says how many workers should be
     working at the same time.  With 1 it will act like a traditional test runner
     but with more it will pull X workers off the list and tell them to Run.
   Manager.Run:
     If told to run 0 then no-op.
     Otherwise look at the workers list, and there are none left then no-op.
     If there are some, take the head and tell him to work.
     Call Run on self again, incase there are more workers who need to work
     to satisfy maxDOP.
     Add the worker you told to work to the pendingWorkers list so we know it is busy.
   Manager.WorkerDone:
     Called from a worker once it is done with its work.
     Create a new pendingWorkers list, removing the one that just finished.
     Check to see if there are any pendingWorkers or other workers for this suite.
     If there are not, tell the reporter that the suite is done.
     If there are no workers and no pending workers then all the tests have been run
     and we can notify the reporter and retrieve the number of tests and return it.
*)
let newManager maxDOP : actor<Manager> =
  let sw = System.Diagnostics.Stopwatch.StartNew()
  let contexts = new HashSet<string>()
  actor.Start(fun self ->
    let rec loop workers pendingWorkers replyChannel =
      async {
        let! msg = self.Receive ()
        match msg with
        | Manager.Initialize (suites) ->
            let wipWorkers = suites |> List.map (fun suite -> suite.Wips |> List.map (fun test -> suite, newWorker self suite test)) |> List.concat |> List.rev
            let workers = suites |> List.map (fun suite -> suite.Tests |> List.map (fun test -> suite, newWorker self suite test)) |> List.concat |> List.rev
            if wipWorkers.IsEmpty then
              return! loop workers pendingWorkers replyChannel
            else
              return! loop wipWorkers pendingWorkers replyChannel
        | Manager.Start(replyChannel) ->
            self.Post(Manager.Run maxDOP)
            return! loop workers pendingWorkers (Some replyChannel)
        | Manager.Run(count) ->
            if count = 0 then
              return! loop workers pendingWorkers replyChannel
            else
              match workers with
              | [] -> return! loop workers pendingWorkers replyChannel
              | (suite, head) :: tail ->
                if not <| contexts.Contains(suite.Context) then
                  contexts.Add(suite.Context) |> ignore
                  reporter.Post(Reporter.ContextStart suite.Context)
                let pendingWorkers = (suite,head)::pendingWorkers
                head.Post(Worker.Run)
                self.Post(Manager.Run(count - 1))
                return! loop tail pendingWorkers replyChannel
        | Manager.WorkerDone(suiteContext, doneWorker) ->
            self.Post(Manager.Run(1))
            let pendingWorkers = pendingWorkers |> List.filter (fun (_, pendingWorker) -> pendingWorker <> doneWorker)
            let workersForSuite = workers |> List.filter (fun (suite,_) -> suiteContext = suite.Context)
            let pendingWorkersForSuite = pendingWorkers |> List.filter (fun (suite,_) -> suiteContext = suite.Context)
            if workersForSuite.IsEmpty && pendingWorkersForSuite.IsEmpty then
              reporter.Post(Reporter.ContextEnd suiteContext)
            if pendingWorkers.IsEmpty && workers.IsEmpty then
              let failed = reporter.PostAndReply(fun replyChannel -> Reporter.RunOver(int sw.Elapsed.TotalMinutes, int sw.Elapsed.TotalSeconds, replyChannel))
              replyChannel.Value.Reply failed
              return! loop workers pendingWorkers replyChannel
            else
              return! loop workers pendingWorkers replyChannel
      }
    loop [] [] None)

(*
   Function
   Take in the max degree of parallelism and pass that to the manager.
   Wait for the run to finish and return the number of tests that failed
   which can be used as an exit code.
*)
let run maxDOP =
  let manager = newManager maxDOP
  manager.Post(Manager.Initialize(suites))
  manager.PostAndReply(fun replyChannel -> Manager.Start replyChannel)
