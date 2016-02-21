[<AutoOpen>]
module prunner.types

open System

type actor<'t> = MailboxProcessor<'t>
type color = System.ConsoleColor

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

type TestContext (testId:Guid, reporter : actor<Reporter>) = class
  member x.TestId = testId
  member x.printfn fmtStr = Printf.kprintf (fun msg -> reporter.Post(Print(msg, x.TestId))) fmtStr
end

type Test =
  {
    Description : string
    Func : TestContext -> unit
    Id : Guid
  }

type Suite () = class
  member val Context : string = "" with get, set
  member val Tests : Test list = [] with get, set
  member val Wips : Test list = [] with get, set
end

type Worker =
  | Run

type Manager =
  | Initialize of Suite list
  | Start of AsyncReplyChannel<int>
  | Run of count:int
  | WorkerDone of suite:string * worker:actor<Worker>
