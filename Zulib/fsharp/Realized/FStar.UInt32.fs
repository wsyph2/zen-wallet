module FStar.UInt32

module Checked = FSharp.Core.Operators.Checked
open FStar.Pervasives.Native

type uint32 = System.UInt32
type t = uint32
type t' = t

let n = Prims.parse_int "32"
let v (x:uint32) : FStar.UInt.uint_t<'a> = FStar.UInt.UInt (Prims.parse_int (x.ToString()))

let zero = 0u
let one = 1u
let ones = 4294967295u

let add (a:uint32) (b:uint32) : uint32 = a + b
let add_mod a b = (add a b) &&& 4294967295u
let checked_add a b : option<uint32> = try Some (Checked.(+) a b) with | _ -> None

let sub (a:uint32) (b:uint32) : uint32 = a - b
let sub_mod a b = (sub a b) &&& 4294967295u
let checked_sub a b : option<uint32> = try Some (Checked.(-) a b) with | _ -> None

let mul (a:uint32) (b:uint32) : uint32 = a * b
let mul_mod a b = (mul a b) &&& 4294967295u
let checked_mul a b : option<uint32> = try Some (Checked.(*) a b) with | _ -> None

let div (a:uint32) (b:uint32) : uint32 = a / b
let checked_div a b : option<uint32> = try Some (a / b) with | _ -> None

let rem (a:uint32) (b:uint32) : uint32 = a % b


//let int_to_uint32 (x:Prims.int) = int_of_string (Prims.to_string x) % 4294967296

let uint_to_t s : uint32 = uint32.Parse(s.ToString())

(* Comparison operators *)

let eq (a:uint32) (b:uint32) : bool = a = b
let gt (a:uint32) (b:uint32) : bool = a > b
let gte (a:uint32) (b:uint32) : bool = a >= b
let lt (a:uint32) (b:uint32) : bool = a < b
let lte (a:uint32) (b:uint32) : bool =  a <= b

(* Infix notations *)

let op_Plus_Hat = add
let op_Plus_Percent_Hat = add_mod
let op_Plus_Question_Hat = checked_add
let op_Subtraction_Hat = sub
let op_Subtraction_Percent_Hat = sub_mod
let op_Subtraction_Question_Hat = checked_sub
let op_Star_Hat = mul
let op_Star_Percent_Hat = mul_mod
let op_Star_Question_Hat = checked_mul
let op_Slash_Hat = div
let op_Slash_Question_Hat = checked_div
let op_Percent_Hat = rem
let op_Equals_Hat = eq
let op_Greater_Hat = gt
let op_Greater_Equals_Hat = gte
let op_Less_Hat = lt
let op_Less_Equals_Hat = lte

let to_string s = s.ToString()
let of_string s : uint32 = uint32.Parse s
