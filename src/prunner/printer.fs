[<AutoOpen>]
module prunner.printer

open System

(*
   Function
   Changing the color, printing, and resetting is a common pattern
   so I created a function for it.
*)
let colorWriteReset color message =
  Console.ForegroundColor <- color
  printfn "%s" message
  Console.ResetColor()

(*
   Function
   Printing an error was big enough and complex enough to extract it
   to a function, even though it is only called one time.
*)
let printError (ex : Exception) =
  colorWriteReset color.Red "Error: "
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
        colorWriteReset color.DarkGreen (":line" + line)
      else
        colorWriteReset color.DarkGreen trace
    Console.ResetColor())
