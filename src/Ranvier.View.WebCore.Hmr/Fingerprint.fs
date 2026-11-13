/// A compilation-independent fingerprint of a delegate's code: IL with metadata tokens resolved to
/// names, closure fields, and user methods it calls. FSI and fsc emit different tokens and type prefixes for the
/// same source, so raw IL bytes cannot be compared; resolved names can.
module Ranvier.View.WebCore.Hmr.Fingerprint

open System
open System.Collections.Generic
open System.Reflection
open System.Reflection.Emit
open System.Text

let private oneByte = Array.zeroCreate<OpCode> 0x100
let private twoByte = Array.zeroCreate<OpCode> 0x100

do
    for f in typeof<OpCodes>.GetFields(BindingFlags.Public ||| BindingFlags.Static) do
        match f.GetValue null with
        | :? OpCode as op ->
            let v = uint16 op.Value

            if v < 0x100us then
                oneByte[int v] <- op
            elif (v &&& 0xff00us) = 0xfe00us then
                twoByte[int (v &&& 0xffus)] <- op
        | _ -> ()

/// Closure names carry their source line (counter@21-3); a line inserted above must not read as a code change.
let private closureLine = Text.RegularExpressions.Regex @"@\d+(-\d+)?"

let rec typeName (t: Type) : string =
    if isNull t then
        "null"
    elif t.IsGenericParameter then
        t.Name
    elif t.IsArray then
        typeName (t.GetElementType ()) + "[]"
    elif t.IsGenericType && not t.IsGenericTypeDefinition then
        let def = Reflect.normalizeTypeName (t.GetGenericTypeDefinition().FullName)
        let args = t.GetGenericArguments () |> Array.map typeName
        def + "<" + String.Join (",", args) + ">"
    else
        closureLine.Replace (Reflect.normalizeTypeName (if isNull t.FullName then t.Name else t.FullName), "@")

/// User code: the app's own assembly and FSI's dynamic interaction assemblies.
let mutable isUserAssembly: Assembly -> bool =
    fun a ->
        let n = a.GetName().Name

        n.StartsWith "FSI-ASSEMBLY"
        || n = (Assembly.GetEntryAssembly().GetName().Name)

let private isClosureType (t: Type) =
    t.Name.Contains "@"
    || t.IsDefined (typeof<Runtime.CompilerServices.CompilerGeneratedAttribute>, false)
    || (not (isNull t.BaseType)
        && t.BaseType.IsGenericType
        && t.BaseType.GetGenericTypeDefinition().FullName.StartsWith "Microsoft.FSharp.Core.FSharpFunc")

type private Walk =
    {
        Out: StringBuilder
        Seen: HashSet<MethodBase>
        mutable Budget: int
    }

let rec private ilOf (w: Walk) (m: MethodBase) =
    w.Out.Append('{').Append(typeName m.DeclaringType).Append("::").Append(m.Name)
    |> ignore

    if w.Seen.Add m && w.Budget > 0 then
        w.Budget <- w.Budget - 1

        let body =
            try
                m.GetMethodBody ()
            with _ ->
                null

        match body with
        | null -> w.Out.Append "<nobody>" |> ignore
        | body ->
            let il = body.GetILAsByteArray ()
            let md = m.Module

            let typeArgs =
                if m.DeclaringType.IsGenericType then
                    m.DeclaringType.GetGenericArguments ()
                else
                    null

            let methodArgs = if m.IsGenericMethod then m.GetGenericArguments () else null
            let mutable i = 0

            let i32 at =
                BitConverter.ToInt32 (il, at)

            // A nested lambda is constructed (newobj) or read from a static instance field, never called here:
            // its body is in the closure type's Invoke, which must count as part of this method's code.
            let nested (t: Type) (text: string) =
                if
                    not (isNull t)
                    && isUserAssembly t.Assembly
                    && isClosureType t
                then
                    let saved = w.Out.Length

                    for im in
                        t.GetMethods (
                            BindingFlags.Instance
                            ||| BindingFlags.Public
                            ||| BindingFlags.NonPublic
                            ||| BindingFlags.DeclaredOnly
                        ) do
                        if im.Name = "Invoke" then
                            ilOf w im

                    let bodies = w.Out.ToString (saved, w.Out.Length - saved)
                    w.Out.Length <- saved
                    text + bodies
                else
                    text

            let resolve (token: int) (kind: OperandType) =
                try
                    match kind with
                    | OperandType.InlineString -> "\"" + md.ResolveString token + "\""
                    | OperandType.InlineField ->
                        let f = md.ResolveField (token, typeArgs, methodArgs)
                        nested f.DeclaringType (typeName f.DeclaringType + "." + f.Name)
                    | OperandType.InlineType ->
                        let t = md.ResolveType (token, typeArgs, methodArgs)
                        nested t (typeName t)
                    | OperandType.InlineMethod ->
                        let callee = md.ResolveMethod (token, typeArgs, methodArgs)

                        if isUserAssembly callee.Module.Assembly then
                            let saved = w.Out.Length
                            ilOf w callee
                            let text = w.Out.ToString (saved, w.Out.Length - saved)
                            w.Out.Length <- saved
                            nested callee.DeclaringType text
                        else
                            typeName callee.DeclaringType
                            + "::"
                            + callee.Name
                            + "/"
                            + string (callee.GetParameters().Length)
                    | OperandType.InlineTok ->
                        let mm = md.ResolveMember (token, typeArgs, methodArgs)
                        typeName mm.DeclaringType + "." + mm.Name
                    | _ -> "sig"
                with _ ->
                    "?"

            while i < il.Length do
                let op =
                    if il[i] = 0xFEuy && i + 1 < il.Length then
                        i <- i + 2
                        twoByte[int il[i - 1]]
                    else
                        i <- i + 1
                        oneByte[int il[i - 1]]

                w.Out.Append(' ').Append(op.Name) |> ignore

                match op.OperandType with
                | OperandType.InlineNone -> ()
                | OperandType.ShortInlineBrTarget
                | OperandType.ShortInlineI
                | OperandType.ShortInlineVar ->
                    w.Out.Append(':').Append(il[i]) |> ignore
                    i <- i + 1
                | OperandType.InlineVar ->
                    w.Out.Append(':').Append(BitConverter.ToInt16 (il, i))
                    |> ignore

                    i <- i + 2
                | OperandType.InlineI
                | OperandType.InlineBrTarget
                | OperandType.ShortInlineR ->
                    w.Out.Append(':').Append(i32 i) |> ignore
                    i <- i + 4
                | OperandType.InlineI8
                | OperandType.InlineR ->
                    w.Out.Append(':').Append(BitConverter.ToInt64 (il, i))
                    |> ignore

                    i <- i + 8
                | OperandType.InlineSwitch ->
                    let n = i32 i
                    i <- i + 4 + 4 * n
                | kind ->
                    w.Out.Append(':').Append(resolve (i32 i) kind)
                    |> ignore

                    i <- i + 4

    w.Out.Append '}' |> ignore

let rec private objectOf (w: Walk) (depth: int) (o: obj) =
    match o with
    | null -> w.Out.Append "null" |> ignore
    | :? string as s -> w.Out.Append('"').Append(s).Append '"' |> ignore
    | :? Delegate as d ->
        w.Out.Append "D(" |> ignore
        ilOf w d.Method
        w.Out.Append '|' |> ignore
        objectOf w (depth + 1) d.Target
        w.Out.Append ')' |> ignore
    | _ ->
        let t = o.GetType ()

        if t.IsPrimitive || t.IsEnum then
            w.Out.Append (o) |> ignore
        elif depth < 6 && isClosureType t then
            w.Out.Append("F(").Append(typeName t) |> ignore

            for m in
                t.GetMethods (
                    BindingFlags.Instance
                    ||| BindingFlags.Public
                    ||| BindingFlags.NonPublic
                    ||| BindingFlags.DeclaredOnly
                ) do
                if m.Name = "Invoke" then
                    ilOf w m

            for f in
                t.GetFields (
                    BindingFlags.Instance
                    ||| BindingFlags.Public
                    ||| BindingFlags.NonPublic
                ) do
                w.Out.Append(' ').Append(f.Name).Append '='
                |> ignore

                objectOf w (depth + 1) (f.GetValue o)

            w.Out.Append ')' |> ignore
        else
            // Captured state (signals, components, records) is identified by type: its value is not code.
            w.Out.Append('<').Append(typeName t).Append '>'
            |> ignore

/// The fingerprint of a delegate (or any captured object): equal for the same source compiled twice.
let ofObject (o: obj) : string =
    let w =
        {
            Out = StringBuilder ()
            Seen = HashSet (HashIdentity.Reference)
            Budget = 64
        }

    objectOf w 0 o
    w.Out.ToString ()
