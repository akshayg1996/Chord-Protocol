#time "on"
#r "nuget: Akka.Remote, 1.4.25"
#r "nuget: Akka, 1.4.25"
#r "nuget: Akka.FSharp, 1.4.25"
#r "nuget: Akka.Serialization.Hyperion, 1.4.25"
open System
open Akka.FSharp
open Akka.Actor


let r = System.Random()
let system = System.create "my-system" (Configuration.load())

type Record =
    {
        InfoString: string
        Key: int
    }


type NodeInfo = 
    {
        NodeKey: int
        NodeRef: IActorRef
    }
    

type SetupInfo =
    {
        SelfKey: int
        SNodeInfo: NodeInfo
        PNodeInfo: NodeInfo
        FNodeInfo: List<NodeInfo>
    }


type FixFingerInfo =
    {
        FingerKey: int
        FingerIndex: int
        FingerFrom: IActorRef
        IsReturn: bool
        TargetIndex: int
        TargetNode: IActorRef
    }


type InfoToJoin =
    {
        Preceding: NodeInfo
        Successor: NodeInfo
    }


type UpdatePreceding =
    {
        UPreceding: NodeInfo
    }


type queryMessage =
    {
        Source: IActorRef
        Key: int
        Hop: int
    }


let sha1 (s:string) =
    let b = System.Text.Encoding.ASCII.GetBytes(s)
    let bsha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(b)
    let mutable output = 0
    for i in 0 .. (bsha1.Length - 1) do
        output <- output + (int bsha1.[i])
    if output < 0 then
        output <- 0 - output
    output


// [<EntryPoint>]
// let main argv =
let args : string array = fsi.CommandLineArgs |>  Array.tail
let mutable numNodes = 256
let mutable numRequests = 8
let mutable m = 10
if args.Length > 0 then
    numNodes <- int args.[0]
    numRequests <- int args.[1]
    m <- 0
    let mutable n = numNodes
    while n > 0 do
        m <- m + 1
        n <- (n >>> 1)


let maxNum = 1 <<< m
printfn "Task with %d nodes, %d requests, allocated max key of chord ring: %d" numNodes numRequests maxNum

let chordActor (mailbox: Actor<_>) =
    let mutable Key = 0
    let mutable successor = {NodeKey = 0; NodeRef = mailbox.Self}
    let mutable preceding = {NodeKey = 0; NodeRef = mailbox.Self}
    let mutable fingerTable = []
    let mutable fingerArray = [||]
    let index = 0
    let recordArray = 
        [| 
            for i in 0 .. 16 do
                yield {InfoString = ""; Key = -1}
        |]

    let closestPrecedingNode id =
        //printfn "%d %d %d" Key preceding.NodeKey successor.NodeKey
        //System.Threading.Thread.Sleep(500)
        let mutable tmp = -1
        for i = m-1 downto 0 do
            let idd = if id < Key then id + maxNum else id
            let a = if fingerTable.[i].NodeKey < Key then fingerTable.[i].NodeKey + maxNum else fingerTable.[i].NodeKey
            //printfn "%d %d" a idd
            if tmp = -1 && a > Key && a < idd then
                tmp <- i
        //printfn "%d" tmp
        if tmp = -1 then
            mailbox.Self
        else 
            fingerTable.[tmp].NodeRef
       


    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? int as id -> 
        (*
            if id > Key && id <= successor.NodeKey then
                printfn "found! %d %d" Key successor.NodeKey
        *)
            let target = closestPrecedingNode id
            if (target) = mailbox.Self then
                printfn "found! %d %d" Key successor.NodeKey

            else
                target <! id
        | :? queryMessage as q ->
            let target = closestPrecedingNode q.Key
            if (target) = mailbox.Self then
                printfn "found! %d %d" Key successor.NodeKey
                q.Source <! q
            else
                target <! {Key = q.Key; Hop = q.Hop + 1; Source = q.Source}
        | :? SetupInfo as info ->
            Key <- info.SelfKey
            successor <- info.SNodeInfo
            preceding <- info.PNodeInfo
            fingerTable <- info.FNodeInfo
        | :? string as x -> 
            printfn "%A" x
        | :? NodeInfo as info ->
            let target = closestPrecedingNode info.NodeKey
            if target = mailbox.Self then
                printfn "found! %d %d" Key successor.NodeKey
                let itj = {Preceding = {NodeKey = Key; NodeRef = mailbox.Self}; Successor = {NodeKey = successor.NodeKey; NodeRef = successor.NodeRef}}
                info.NodeRef <! itj
                successor.NodeRef <! {UPreceding = info}
                successor <- info
                for i = 0 to m - 1 do
                    successor.NodeRef <! {FingerKey = Key + (1 <<< i);FingerIndex = i; FingerFrom = mailbox.Self; IsReturn = false;TargetIndex = -1; TargetNode = mailbox.Self}
            else
                target <! info
        | :? UpdatePreceding as u ->
            preceding <- u.UPreceding
        | :? InfoToJoin as itj ->
            preceding <- itj.Preceding
            successor <- itj.Successor
            for i = 0 to m - 1 do
                successor.NodeRef <! {FingerKey = Key + (1 <<< i);FingerIndex = i; FingerFrom = mailbox.Self; IsReturn = false;TargetIndex = -1; TargetNode = mailbox.Self}
        | :? FixFingerInfo as fi ->
            if fi.IsReturn = false then
                let target = closestPrecedingNode fi.FingerKey
                if target = mailbox.Self then
                    printfn "found! %d %d" Key successor.NodeKey
                    fi.FingerFrom <! {FingerFrom = fi.FingerFrom; FingerKey = fi.FingerKey; FingerIndex = fi.FingerIndex; IsReturn = true; TargetIndex = successor.NodeKey; TargetNode = successor.NodeRef}
                else
                    target <! fi
            else
                Array.set fingerArray fi.FingerIndex {NodeKey = fi.TargetIndex; NodeRef = fi.TargetNode}
        | _ -> ()
            
        return! loop()
    }
    loop()

let rands = [
    for i in 1 .. numNodes do
        yield (r.Next() % maxNum)
]
let numList = List.sort rands

let actorList = [
    for i in 0 .. (numNodes - 1) do
        let name = "Actor" + i.ToString()
        let temp = spawn system name chordActor
        yield temp
]

for i in 0 .. (numNodes - 1) do
    let FList = [
        for j in 0 .. (m-1) do
            let tmp = 1 <<< j
            let mutable mark = -1;
            for k in 1 .. numNodes do
                let a = if i + k >= numNodes then numList.[(i+k) % numNodes] + maxNum else numList.[(i+k) % numNodes]
                if a >= tmp + numList.[i] && mark = -1 then
                    mark <- (i+k) % numNodes
            let temp = {NodeKey = numList.[mark]; NodeRef = actorList.[mark]}
            yield temp
    ]
     
    let myInfo = {SelfKey = numList.[i]; SNodeInfo = {NodeKey = numList.[(i+1) % numNodes]; NodeRef = actorList.[(i+1) % numNodes]}; PNodeInfo = {NodeKey = numList.[(i+numNodes-1) % numNodes]; NodeRef = actorList.[(i+numNodes-1) % numNodes]}; FNodeInfo = FList}
    actorList.[i] <! myInfo


System.Threading.Thread.Sleep(1000)


let inputStringList = [
    for i in 1 .. numNodes do
        let requestList = [
            for j in 1 .. numRequests do
                let l = r.Next() % 100 + 1
                let mutable s = ""
                for k in 0 .. l do
                    s <- s + char(r.Next() % 95 + 32).ToString()
                yield s
        ]
        yield requestList
]


let dispatcher = 
    spawn system "dispatcher" 
        (fun mailbox ->
            let mutable counter = 0
            let mutable total = 0
            let rec loop() = actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? string as s ->
                    if s = "start" then
                        for i in 0 .. (numNodes - 1) do
                            for j in 0 .. (numRequests - 1) do
                                
                                actorList.[i] <! {Key = (sha1 inputStringList.[i].[j]) % maxNum; Hop = 0; Source = mailbox.Self}
                | :? queryMessage as q ->
                    total <- total + q.Hop
                    counter <- counter + 1
                    if counter = numNodes * numRequests then
                        printfn "Average Hops: %f" ((float total) / (float counter))
                        printfn "Total Number of Hops: %f" ((float total))
                | _ -> ()

                return! loop()
            }
            loop()
        )


printfn "Start!"
System.Threading.Thread.Sleep(500)
//actorList.[23] <! 87
dispatcher <! "start"

    
System.Console.ReadLine() |> ignore




//0// return an integer exit code