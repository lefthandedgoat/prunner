[<AutoOpen>]
module prunner.test

open System
open prunner

(*
   Pattern Matching
   A very powerful feature.  Think case/switch statement but better.
   Particullary useful in conjunction with DUs, Lists, and Records;
   pattern matching will guide you towards success by telling when
   you have redundant or missing cases using compiler warnings.
   Read more: http://fsharpforfunandprofit.com/posts/match-expression/

   This example is basic.  It takes a list (of anything) and gets the
   first item (head).  Its called last, because technically the head
   is the last item added to the list becase we are prepending values.
   There are better examples of Pattern matching in actors.fs

   Last is head because we prepend, and we prepend because lists in F#
   are immutable.  They are singly linked lists.  Appending is o(n) and
   can quickly get expensive as you have more tests.  Prepending is a good
   trick, because it is o(1) but requires you to reverse your list before
   using if you care about order.

   Head/Tail programming againsts lists is a really powerful way of thinking.
   Read more: https://mitpress.mit.edu/books/little-schemer
*)
let private last = function
  | head :: _ -> head
  | [] -> failwith "Empty list."

(*
  Function and unit
  This is a basic function.
  In F# you generally dont use variables, you use values.  Values aren't
  variables because they can't vary.  They are immutable by default.
  Read more: http://fsharpforfunandprofit.com/posts/defining-functions/
  Read more: https://msdn.microsoft.com/en-us/library/dd483472.aspx

  Basic value:
  let name = "prunner"
  Basic function:
  let helloName name = printfn "Hello %s" name
  Functions are values, but they differ because they accept arguments.

  In the below example, `ng` is a function becaues it takes argument `()`
  () is a special value in F# called unit.  It is simplar to void in other languages.
  `ng` would be a normal value if it did not take an argument.  It would contain a
  single guid and never change.  Since we want a new guid each time we call it
  we parameterize it.  Since we dont need a real value as the parameter we use ()/unit.
*)
let private ng() = Guid.NewGuid()

(*
   Mutable Value
   Things in F# are generally immutable (interop maintains behaviour from interroped library).
   suites is a mutable list of Suite.  It is mutable because I want to add things to it.
   Cheesy but it works.
*)
let mutable suites = [new Suite()]

(*
   Todo and skip are special placeholder functions.  They are mutable because
   they have to in order for reference equality semantics to work.
   They are used to signify that a test should be skipped or marked as todo.
   See the example project for usage.
*)
let mutable todo = fun (_:TestContext) -> ()
let mutable skipped = fun (_:TestContext) -> ()

(*
   Function
   context simple creates a new empty Suite with the name provided
   Think of it as a constructor for Suite.
*)
let context c =
  if (last suites).Context = "" then
    (last suites).Context <- c
  else
    let s = new Suite()
    s.Context <- c
    suites <- s::suites

(*
   Infix operators
   Prefix = before
   Postfix/Suffix = after
   Infix = middle
   Infix operators are a cool feature of F#.  They allow you to create a function
   that is invoked by placing it between its two paremeters.
   Read more: https://en.wikibooks.org/wiki/F_Sharp_Programming/Operator_Overloading#Infix_operators

   It is easy to abuse them so be careful.  I think the general guidance is to not use them.
   I use them in prunner because its scope is very narrow, there are only 3, and it helps
   with syntax.

   Regular Test
   "the description" &&& fun ctx -> printfn "the body"
   Wip Test
   "wip description" &&&& fun ctx -> printfn "wip body"
   Skip Test
   "skip description" &&! fun ctx -> printfn "skip body"
   Todo Test
   "todo description" &&& todo
*)
let ( &&& ) description f =
    (last suites).Tests <- { Description = description; Func = f; Id = ng() }::(last suites).Tests
let ( &&&& ) description f =
    (last suites).Wips <- { Description = description; Func = f; Id = ng() }::(last suites).Wips
let ( &&! ) description _ =
    (last suites).Tests <- { Description = description; Func = skipped; Id = ng() }::(last suites).Tests
