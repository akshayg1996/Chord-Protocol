#time "on"
#r "nuget: Akka.Remote, 1.4.25"
#r "nuget: Akka, 1.4.25"
#r "nuget: Akka.FSharp, 1.4.25"
#r "nuget: Akka.Serialization.Hyperion, 1.4.25"
open System
open Akka.FSharp
open Akka.Actor


let r = System.Random()
let system = System.create "system" (Configuration.load())


type QueryMsg =
    {
        Source: IActorRef
        k: int
        Hop: int
    }

type FixfngrInfo =
    {
        TargetIndex: int
        TargetNode: IActorRef
        FingerFrom: IActorRef
        FingerKey: int
        FingerIndex: int
        IsReturn: bool
    }

type InfoNd = 
    {
        Ndkey: int
        NdRef: IActorRef
    }

type InfoToJoin =
    {
        Before: InfoNd
        Next: InfoNd
    }


type UpdateBefore =
    {
        UBefore: InfoNd
    }

    
type SetupInfo =
    {
        SelfKey: int
        BInfoNd: InfoNd
        NInfoNd: InfoNd
        TableInfoNd: List<InfoNd>
    }


let shafunc (s:string) =
    let b = System.Text.Encoding.ASCII.GetBytes(s)
    let bshafunc = System.Security.Cryptography.SHA1.Create().ComputeHash(b)
    let mutable res = 0
    for i in 0 .. (bshafunc.Length - 1) do
        res <- res + (int bshafunc.[i])
    if res < 0 then
        res <- 0 - res
    res

let args : string array = fsi.CommandLineArgs |>  Array.tail
let mutable numNds = 200
let mutable numRqsts = 6
let mutable m = 10
if args.Length > 0 then
    numNds <- int args.[0]
    numRqsts <- int args.[1]
    m <- 0
    let mutable n = numNds
    while n > 0 do
        m <- m + 1
        n <- (n >>> 1)


let maxN = 1 <<< m

let chrngActor (mailbox: Actor<_>) =
    let mutable fingerTable = []
    let mutable fingerArray = [||]
    let mutable k = 0
    let mutable next = {Ndkey = 0; NdRef = mailbox.Self}
    let mutable before = {Ndkey = 0; NdRef = mailbox.Self}
    let closestbeforeNd id =

        let mutable temp = -1
        for i = m-1 downto 0 do
            let a = if fingerTable.[i].Ndkey < k then fingerTable.[i].Ndkey + maxN else fingerTable.[i].Ndkey
            let id1 = if id < k then id + maxN else id
            if temp = -1 && a > k && a < id1 then
                temp <- i
        if temp = -1 then
            mailbox.Self
        else 
            fingerTable.[temp].NdRef
       
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match box message with
        | :? int as id -> 
            let target = closestbeforeNd id
            if (target) = mailbox.Self then
                  printfn ""
            else
                target <! id

        | :? QueryMsg as q ->
            let target = closestbeforeNd q.k
            if (target) = mailbox.Self then

                  q.Source <! q
            else
                target <! {k = q.k; Hop = q.Hop + 1; Source = q.Source}

        | :? InfoToJoin as itj ->
            before <- itj.Before
            next <- itj.Next
            for i = 0 to m - 1 do
                next.NdRef <! {FingerKey = k + (1 <<< i);FingerIndex = i; FingerFrom = mailbox.Self; IsReturn = false;TargetIndex = -1; TargetNode = mailbox.Self}
        
        | :? SetupInfo as info ->
            k <- info.SelfKey
            next <- info.BInfoNd
            before <- info.NInfoNd
            fingerTable <- info.TableInfoNd

        | :? InfoNd as info ->
            let target = closestbeforeNd info.Ndkey
            if target = mailbox.Self then
                let itj = {Before = {Ndkey = k; NdRef = mailbox.Self}; Next = {Ndkey = next.Ndkey; NdRef = next.NdRef}}
                info.NdRef <! itj
                next.NdRef <! {UBefore = info}
                next <- info
                for i = 0 to m - 1 do
                    next.NdRef <! {FingerKey = k + (1 <<< i);FingerIndex = i; FingerFrom = mailbox.Self; IsReturn = false;TargetIndex = -1; TargetNode = mailbox.Self}
            else
                target <! info
        
        | :? FixfngrInfo as fi ->
            if fi.IsReturn = false then
                let target = closestbeforeNd fi.FingerKey
                if target = mailbox.Self then
                    fi.FingerFrom <! {FingerFrom = fi.FingerFrom; FingerKey = fi.FingerKey; FingerIndex = fi.FingerIndex; IsReturn = true; TargetIndex = next.Ndkey; TargetNode = next.NdRef}
                else
                    target <! fi
            else
                Array.set fingerArray fi.FingerIndex {Ndkey = fi.TargetIndex; NdRef = fi.TargetNode}
        
        | :? string as x -> 
            printfn "%A" x

        | :? UpdateBefore as u ->
            before <- u.UBefore
       
        | _ -> ()
            
        return! loop()
    }
    loop()

let rndnumlst = [
    for i in 1 .. numNds do
        yield (r.Next() % maxN)
]
let numlst = List.sort rndnumlst

let chrdactrlist = [
    for i in 0 .. (numNds - 1) do
        let name = "Actor" + i.ToString()
        let temp = spawn system name chrngActor
        yield temp
]

for i in 0 .. (numNds - 1) do
    let FList = [
        for j in 0 .. (m-1) do
            let mutable mrk = -1;
            let temp = 1 <<< j
            for k in 1 .. numNds do
                let a = if i + k >= numNds then numlst.[(i+k) % numNds] + maxN else numlst.[(i+k) % numNds]
                if a >= temp + numlst.[i] && mrk = -1 then
                    mrk <- (i+k) % numNds
            let temp = {Ndkey = numlst.[mrk]; NdRef = chrdactrlist.[mrk]}
            yield temp
    ]
     
    let myInfo = {SelfKey = numlst.[i]; BInfoNd = {Ndkey = numlst.[(i+1) % numNds]; NdRef = chrdactrlist.[(i+1) % numNds]}; NInfoNd = {Ndkey = numlst.[(i+numNds-1) % numNds]; NdRef = chrdactrlist.[(i+numNds-1) % numNds]}; TableInfoNd = FList}
    chrdactrlist.[i] <! myInfo


let inpStrList = [
    for i in 1 .. numNds do
        let rqstList = [
            for j in 1 .. numRqsts do
                let mutable str = ""
                let l = r.Next() % 100 + 1
                for k in 0 .. l do
                    str <- str + char(r.Next() % 95 + 32).ToString()
                yield str
        ]
        yield rqstList
]


let BossActor = 
    spawn system "BossActor" 
        (fun mailbox ->
            let mutable tot = 0
            let mutable cntr = 0
            let rec loop() = actor {
                let! message = mailbox.Receive()
                match box message with
                | :? string as s ->
                    if s = "begin" then
                        for i in 0 .. (numNds - 1) do
                            for j in 0 .. (numRqsts - 1) do
                                chrdactrlist.[i] <! {k = (shafunc inpStrList.[i].[j]) % maxN; Hop = 0; Source = mailbox.Self}

                | :? QueryMsg as q ->
                    tot <- tot + q.Hop
                    cntr <- cntr + 1
                    if cntr = numNds * numRqsts then
                        printfn "Average Hops: %f" ((float tot) / (float cntr))
                        printfn "Total number of hops: %f" ((float tot))
                | _ -> ()

                return! loop()
            }
            loop()
        )


printfn "Boss started"
BossActor <! "begin"
    
System.Console.ReadLine() |> ignore


