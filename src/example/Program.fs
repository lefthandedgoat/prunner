module Main

open System
open System.Collections.Generic
open prunner

//demo
context "Test Context"

let delimeters = ["\n"; ","]

let add2 numbers delimeters =
  if numbers = "" then
    0
  else
    let mutable numbers = numbers
    delimeters |> List.iter (fun delimeter -> numbers <- numbers.Replace(delimeter, ","))
    let numbers =
      numbers.Split(',')
      |> List.ofArray
      |> List.map int
      |> List.filter (fun number -> number <= 1000)
    let negatives = numbers |> List.filter (fun number -> number < 0)
    if negatives = [] then
      numbers |> List.sum
    else
      failwith <| sprintf "These numbers are negative and are not allowed %A" negatives

let add numbers = add2 numbers delimeters

"1 - empty string equals 0" &&& fun ctx ->
  add "" == 0

"1 - '1' string equals 1" &&& fun ctx ->
  add "1" == 1

"1 - '1,2' string equals 3" &&& fun ctx ->
  add "1,2" == 3

"2 - '1,2,3' string equals 6" &&& fun ctx ->
  add "1,2,3" == 6

"2 - '1,2,3,4,5' string equals 15" &&& fun ctx ->
  add "1,2,3,4,5" == 15

"3 - '1\n2,3' string equals 15" &&& fun ctx ->
  add "1\n2,3" == 6

"4 - '1;2' string equals 3" &&& fun ctx ->
  add2 "1;2" [";"] == 3

"5 - '-1;2' fails" &&& fun ctx ->
  add2 "-1;2" [";"] == 3

"5 - '-1;-2' fails" &&& fun ctx ->
  add2 "-1;-2" [";"] == 3

"6 - '2;1001' string equals 2" &&& fun ctx ->
  add2 "2;1001" [";"] == 2

"7 - '2***3***4' string equals 9" &&& fun ctx ->
  add2 "2***3***4" ["***"] == 9

"8 - '1*2%3' string equals 6" &&& fun ctx ->
  add2 "1*2%3" ["*"; "%"] == 6

"9 - '1***2%4' string equals 7" &&& fun ctx ->
  add2 "1***2%4" ["***"; "%"] == 7

let maxDOP = 1
let failedTest = run maxDOP
printfn "final failed count %A" failedTest
