[<AutoOpen>]
module prunner.types

open System

(*
   Type Aliases
   You can use these to make your code more terse,
   to rename concepts and make them more familiar like actor/MailboxProcessor,
   or to give meaning to simple types like int and string.
   ex: type Name = string
   Read more: http://fsharpforfunandprofit.com/posts/type-abbreviations/

   actors are the abstraction we will use for parallelism/concurrency in prunner.
   You can think of them as Alan Kay style objects (that send and recieve messages)
   or light weight threads.  They sit on the thread pool and you can have many (1M+) easily.
   prunner uses 3 actors described below
   Read more: https://en.wikipedia.org/wiki/Actor_model
*)
type actor<'t> = MailboxProcessor<'t>
type color = System.ConsoleColor

(*
   Discriminated Unions
   Also known as Tagged Unions or Sum Types, abbreviated as DUs
   DUs are a really useful. Think of them as `enums` but better.
   Not only do you have a list of associated 'cases', each case
   can have additonal associated data.
   ex:
   type Role =
     | Developer
     | QA
     | Manger of numberOfReports:int
   The overarching type is Role, and its cases are Developer, QA, and Manager, which has
   extra data storing the number of people that report to them.
   Read more: http://fsharpforfunandprofit.com/posts/discriminated-unions/

   This DU exists as a contract for the messages that an actor can recieve
   The role of the reporter in prunner is to queue up messages to print to the console
   and print them at the correct time.  Parallel/concurrency does not play well with the console
   There is only one reporter.
*)
type Reporter =
  | ContextStart of description:string
  | ContextEnd of description:string
  | TestStart of description:string * id:Guid
  | Print of message:string * id:Guid
  | Skip of id:Guid
  | Todo of id:Guid
  | Pass of id:Guid
  | Fail of id:Guid * ex:Exception
  | RunOver of minutes:int * seconds:int * AsyncReplyChannel<int>

(*
   Classes
   Not much different than other languages, just syntactically different
   You can have constructors, properties, methods, member variables etc.
   I personally don't use classes very often.

   Test context just holds some data about test, and is passed into a test's body
   allowing the test to print to the reporter
*)
type TestContext (testId:Guid, reporter : actor<Reporter>) = class
  member x.TestId = testId
  member x.printfn fmtStr = Printf.kprintf (fun msg -> reporter.Post(Print(msg, x.TestId))) fmtStr
end

(*
   Records
   Records are really great for storing data.  They have lots of interesting
   properties.  They have a single default constructor that takes all properties
   They are immutable.  Their structural/value equality is created for you, so
   comparing two records with the same values will return true.  They can also have methods
   Most of the code I write uses Records and DUs as the main data types.
   Read more: http://fsharpforfunandprofit.com/posts/records/

   The Test record holds information about a test, its description,
   its function body and an Id.
*)
type Test =
  {
    Description : string
    Func : TestContext -> unit
    Id : Guid
  }

(*
  Class
  The Suite class holds a Context name and a list (empty list syntax is [])
  of Tests and Wips. Wip stands for Work In Progress.  There are times when you
  want to run a single test you are workong on, and prunner provides a mechanism for that.
  All three properties are class getter/setters.
*)
type Suite () = class
  member val Context : string = "" with get, set
  member val Tests : Test list = [] with get, set
  member val Wips : Test list = [] with get, set
end

(*
   DU
   The Worker DU contains the one message that a worker actor consumes.
   Run simply tells the workers that it is time for them to run their test and
   report the results.  One worker is constructed for each test, but a worker
   will wait to run until he is instructed to.
   There are many workers.
*)
type Worker =
  | Run

(*
   DU
   The Manager DU manages all of the workers.
   It recieves a list of suites to run via Initialize.
   It recieves a reply channel on Start, so that it can send final results to the
   consumer of the manager, once the run is over.
   The Run command tells the manager how many workers its needs to instruct to work.
   WorkerDone is called by a worker when they are done so that the manager
   knows that it can have another worker run, or if all workers are done,
   tell the reporter that the run is over.
   There is only one manager.
*)
type Manager =
  | Initialize of Suite list
  | Start of AsyncReplyChannel<int>
  | Run of count:int
  | WorkerDone of suite:string * worker:actor<Worker>
