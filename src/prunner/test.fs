[<AutoOpen>]
module prunner.test

open System
open prunner

let private last = function
  | hd :: _ -> hd
  | [] -> failwith "Empty list."

let private ng() = Guid.NewGuid()

let mutable suites = [new Suite()]
let mutable todo = fun (_:TestContext) -> ()
let mutable skipped = fun (_:TestContext) -> ()

let context c =
  if (last suites).Context = "" then
    (last suites).Context <- c
  else
    let s = new Suite()
    s.Context <- c
    suites <- s::suites

let ( &&& ) description f =
    (last suites).Tests <- { Description = description; Func = f; Id = ng() }::(last suites).Tests
let ( &&&& ) description f =
    (last suites).Wips <- { Description = description; Func = f; Id = ng() }::(last suites).Wips
let ( &&! ) description _ =
    (last suites).Tests <- { Description = description; Func = skipped; Id = ng() }::(last suites).Tests
