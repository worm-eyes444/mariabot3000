open Microsoft.Playwright
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.IO
open System.Threading.Tasks
open Mariabot3000.Domain

let zip3 (s1: seq<'T1>) (s2: seq<'T2>) (s3: seq<'T3>) = 
    Seq.zip (Seq.zip s1 s2) s3
    |> Seq.map (fun ((a, b), c) -> (a,b,c))


let testing = false
let waitPost = if testing then 10 else 120
let waitPoll = if testing then 10 else 60




//ai stuff
let askOpenRouter (apiKey: string) (question: string) = async {
    use client = new HttpClient()

    client.DefaultRequestHeaders.Authorization <-
        AuthenticationHeaderValue("Bearer", apiKey)

    let body =
        {|
            model = "deepseek/deepseek-v4.1-flash"
            messages =
                [|
                    {|
                        role = "user"
                        content = question
                    |}
                |]
        |}

    let json = JsonSerializer.Serialize(body)

    use content =
        new StringContent(json, Encoding.UTF8, "application/json")

    let! response =
        client.PostAsync(
            "https://openrouter.ai/api/v1/chat/completions",
            content
        )
        |> Async.AwaitTask

    let! text =
        response.Content.ReadAsStringAsync()
        |> Async.AwaitTask

    // OpenRouter reports failures as { "error": { "message": "...", "code": ... } }
    let errorDetail (body: string) =
        try
            use doc = JsonDocument.Parse body
            match doc.RootElement.TryGetProperty "error" with
            | true, err ->
                match err.TryGetProperty "message" with
                | true, msg -> msg.GetString()
                | _ -> body
            | _ -> body
        with _ -> body

    if response.IsSuccessStatusCode then
        return Ok text
    else
        let code = int response.StatusCode
        let hint =
            if code = 401 || code = 403 then
                sprintf "Invalid OpenRouter API key (HTTP %d). Check the key in .auth/LLMkey." code
            elif code = 402 then
                "OpenRouter account has no credits left (HTTP 402)."
            elif code = 429 then
                "OpenRouter rate limit reached (HTTP 429), try again later."
            else
                sprintf "OpenRouter request failed (HTTP %d %s)." code response.ReasonPhrase
        return Error(sprintf "%s\n%s" hint (errorDetail text))
}

// Reads the OpenRouter key from .auth/LLMkey, returning a readable error if it is missing.
let readApiKey () =
    let keyPath =
        Path.Combine(Directory.GetCurrentDirectory(), ".auth", "LLMkey")
    if File.Exists keyPath then
        Ok((File.ReadAllText keyPath).Trim())
    else
        Error(sprintf "No OpenRouter key file found. Create %s and paste your API key into it." keyPath)

let LLMapikey =
    match readApiKey() with
    | Ok apikey -> apikey
    | Error message ->  
    eprintfn "ERROR: %s" message
    exit 1


type Message = {
    role: string
    content: string
}

type Choice = {
    message: Message
}

type Response = {
    choices: Choice array
}




[<EntryPoint>]
let main _ =
    printfn "Starting..."

    task {
        printfn "Creating Playwright..."
        use! playwright = Playwright.CreateAsync()

        printfn "Launching Chromium..."
        let userDataDir = Path.Combine(Directory.GetCurrentDirectory(), ".auth/steam_profile")
        let! browser =
            playwright.Chromium.LaunchPersistentContextAsync(
                userDataDir,
                BrowserTypeLaunchPersistentContextOptions(
                    Headless = not testing
                )
            )
        let authDir =
            Path.Combine(Directory.GetCurrentDirectory(), ".auth")
        Directory.CreateDirectory(authDir) |> ignore
        let authfile =
            Path.Combine(authDir, "authfile")
                
        printfn "Creating page..."
        let! page = browser.NewPageAsync()
        printfn "Opening steam"
        let! _ = page.GotoAsync "https://store.steampowered.com/"

        printfn "Steam open"

        if File.Exists(authfile) = false then
            let loginBTN = 
                page.GetByRole(
                    AriaRole.Link,
                    PageGetByRoleOptions(Name = "sign in")
                ).First
            printfn "Finding search box..."
            do! loginBTN.ClickAsync()
            printfn "PLEASE LOG IN"
        else
            let! _ = page.GotoAsync "https://steamcommunity.com/"
            printfn "logged"
        
        

        //wait for the dropdown menu to appear then continue automation
        let accPull = page.Locator("#account_pulldown")
        do! accPull .WaitForAsync(
            LocatorWaitForOptions(State = WaitForSelectorState.Attached,
            Timeout = float32 3000.
            )
        )
        let file = File.Create(authfile)
        let! username = accPull.InnerTextAsync()
        printfn "%s" username
        


        let personalityFile = 
            Path.Combine(Directory.GetCurrentDirectory(), "personality.txt")
        let mutable personality = File.ReadAllText personalityFile
        let personality = personality.Replace("{{char}}", username).Replace("{{user}}", "OTHER STEAM USERS")
        
        printfn "%s" personality
        
        
        //get the id so we can go to the notification screen
        do! page.WaitForTimeoutAsync(float32 1000.)
        do! accPull.ClickAsync()
        do! page.GetByRole(
            AriaRole.Link,
            PageGetByRoleOptions(Name = "View my profile")
            ).First.ClickAsync()
        let currentUrl = page.Url

        let steampath (profileUrl:string) =
            //let steamId = (profileUrl.Split "/")[4]
            let steamId = (((currentUrl.Split "/")[4]).Split"?")[0]
            let urlPart = (profileUrl.Split "/")[3]
            (steamId, urlPart)

        let profileLinkConstructor ((steamId:string),(urlPart:string)) urlPath:string = 
            let notifUrl = sprintf "https://steamcommunity.com/%s/%s/%s" urlPart steamId urlPath
            notifUrl
            
        
        let notifLink profileUrl:string =
            profileUrl
            |> steampath
            |> fun x -> profileLinkConstructor x "notifications"

        let profileLink profileUrl:string =
            profileUrl
            |> steampath
            |> fun x -> profileLinkConstructor x ""

        let commentsLink profileUrl:string =
            profileUrl
            |> steampath
            |> fun x -> profileLinkConstructor x "allcomments"





        printfn "%s" currentUrl
        let steamId = (currentUrl.Split "/")[4]
        let urlPart = (currentUrl.Split "/")[3]
        printfn "%s" steamId
        

        let keepGoing = true
        while keepGoing do
        // change this depending on id of profile
            let notifUrl = sprintf "https://steamcommunity.com/%s/%s/notifications" urlPart steamId
            let! _ = page.GotoAsync notifUrl
            do! page.WaitForTimeoutAsync(float32 1000.)



            // printfn "Press Enter to respond to the last comment notification."
            // System.Console.ReadLine() |> ignore
            
            do! page.WaitForTimeoutAsync(float32 2000.)
            //go to the newest comment
            let commentNotifs = 
                page.Locator("a[href*='/comment/']").Filter(
                    LocatorFilterOptions(
                        HasText = "Comment"
                    )
                )
                
            let! notifCount = commentNotifs.CountAsync()

            let commentsNotifsList =
                [ 0 .. notifCount - 1 ]
                |> List.map commentNotifs.Nth 



            let notifBox (notif:ILocator) =
                notif.Locator("div").Nth(2)
            
            let notifBoxClasses (notifBox:ILocator) =
                notifBox.GetAttributeAsync("class")

            let notifBoxClassCount (notifBoxClasses:string) =
                notifBoxClasses.Split([| ' ' |])
                |> Array.length
           

            let notifRead (notif:ILocator) = 
                 task {
                    let! classes =
                        notif
                        |> notifBox
                        |> notifBoxClasses
                    let classCount = 
                        classes
                        |> notifBoxClassCount
                    return classCount <= 1
                }
            
            let unreadNotif (notif:ILocator) =
                task {
                    let! isRead = notifRead notif
                    return
                        if isRead then
                            None
                        else
                            Some notif
                }
            
            let! unreadNotifs = 
                commentsNotifsList
                |> List.map unreadNotif
                |> Task.WhenAll
            
            //let commentNotif = commentNotifs.Nth 0
            let commentNotifOpt = 
                unreadNotifs
                |> Array.choose id // changes the somes to object adn nones to nothing at all
                |> Array.tryHead

 
            printfn "\n"
            printfn "\n"
                
            match commentNotifOpt with
            | Some commentNotif ->
                printfn "notif has not been read"

                do! page.WaitForTimeoutAsync(float32 2000.)
                do! commentNotif.ClickAsync()

                //mabye go to the comment section specifically 
                let currentUrl = page.Url
                printfn "%s" currentUrl
                let csSteamId = (((currentUrl.Split "/")[4]).Split"?")[0]
                printfn "%s" csSteamId
                let csUrlPart = (currentUrl.Split "/")[3]


                let commentSectionUrl = sprintf "https://steamcommunity.com/%s/%s/allcomments" csUrlPart csSteamId
                let! _ = page.GotoAsync commentSectionUrl


                do! page.WaitForTimeoutAsync(float32 3000.)
                //gather last comment(s)
                let lastComment = page.Locator("div.commentthread_comment_text").First
                let! lastCommentText = lastComment.InnerTextAsync()
                
                //allcoments
                let comments = page.Locator("div.commentthread_comment_text")
                let! commentsText = comments.AllInnerTextsAsync()
                
                //gtting all names
                let names = page.Locator("a.commentthread_author_link")
                let! namesText = names.AllInnerTextsAsync()

                //getting timestamps
                let timesStamps = page.Locator("div.commentthread_comment_timestamp")
                let! timeStampsCount = timesStamps.CountAsync()

                let timesStampsList =
                    [ 0 .. timeStampsCount/2 - 1 ]
                    |> List.map timesStamps.Nth 
                    |> List.indexed 
                    |> List.choose ( fun (i,x)->
                    if i % 2 = 1 then Some x else None)

                let timeStamp (timeStamp:ILocator) =
                    task{
                       let! x = timeStamp.GetAttributeAsync("title") 
                       return x
                    }

                let! timeStampsText = 
                    timesStampsList
                    |> List.map (fun x -> timeStamp x)
                    |> Task.WhenAll 


                
                
                let commentsNamesTextCompiled = 
                    zip3 namesText commentsText timeStampsText
                    |> Seq.map (fun(x,y,z) -> sprintf "[%s] %s: \'%s\' "z x y)
                    |> String.concat "\n" 

                printfn "%s" commentsNamesTextCompiled
                

               // this will take a datapannel and a target, create a comment object
                let readComment (target:SteamProfile) (threadId:ThreadId) (container:ILocator) = 
                    task {
                        let! rawid = container.GetAttributeAsync "id"
                        match rawid |> Option.ofObj |> Option.bind CommentId.tryParse with
                        | None -> 
                            return Error "Comment container has no id"
                        | Some commentId ->
                            let authorLink = container.Locator("a.commentthread_author_link").First
                            let txtNode = container.Locator("div.commentthread_comment_text").First
                            let timestamp = container.Locator(".commentthread_comment_timestamp").First
                            
                            //checking if its the right container that only contains once comment (it happeens more than you think)
                            let! count = container.Locator("div.commentthread_comment_text").CountAsync()
                            if count <> 1 then
                                return Error (sprintf "there are %d texts in your container" count)
                            else
                                let! author = authorLink.InnerTextAsync()
                                let! body = txtNode.InnerTextAsync()
                                let! ts = timestamp.GetAttributeAsync("title")
                                return Ok { CommentId = commentId
                                            ThreadId = threadId
                                            Target = target
                                            Author = { steamId64 = None; Username = None; DisplayName = author }
                                            text = body
                                            timestamp = Option.ofObj ts }
                    }

    
                 //go to comment box, write text, press comment button
                let commentBox = 
                    page.GetByPlaceholder("Add a comment")
                do! commentBox.ClickAsync()
                
    
                let aiQuery = sprintf "you are a %s.
                personality = %s
                This is a conversation that is happening in steam comments. These are the last messages in the room ordered newest to oldest: \n %s. \n keep it around 30 to 100 words. type a message toadd to the conversation. The most recent message is: %s, you MAY respond to it, or carry on with a previous conversation. 
                Write in all lowercase (unless angry as mentioned erlier). 
                do not use emojis.
                If all the messages recently have been too calm, point out issues with other users or try to covertly start an argument.
                If people are argumentetive, get angry but then back out/say sorry.
                Avoid persuasive, therapeutic, or motivational framing by default.
                Context improves accuracy only; it does not define identity, intent, or belief." username personality commentsNamesTextCompiled lastCommentText
                printfn "%s" aiQuery
                let response =
                    try
                        askOpenRouter LLMapikey aiQuery
                        |> Async.RunSynchronously
                    with ex ->
                        Error(sprintf "request failed: %s" ex.Message)

                match response with
                | Error message ->
                    eprintfn "OpenRouter error: %s" message
                | Ok text ->
                    printfn "%s" text
                    let result = JsonSerializer.Deserialize<Response>(text)
                    let answer = result.choices[0].message.content
                    let comment = sprintf "%s" answer
                    printfn "\n\n%s" comment
                    do! commentBox.FillAsync comment
                    do! page.WaitForTimeoutAsync(float32 1000.)
                    do! commentBox.ClickAsync()
                    do! page.WaitForTimeoutAsync(float32 1000.)
                    do! page.GetByText("Post Comment").ClickAsync()
                    do! page.WaitForTimeoutAsync(float32 3000.)
                //have to return to the notifications otherwise will count new ones as read
                let notifUrl = sprintf "https://steamcommunity.com/%s/%s/notifications" urlPart steamId
                let! _ = page.GotoAsync notifUrl
                do! page.WaitForTimeoutAsync(float32 (1000 * waitPost))

            | None -> 
                do! page.WaitForTimeoutAsync(float32 (1000 * waitPoll))


        //return to the comments notif page
        



        printfn "Press Enter to Close"
        System.Console.ReadLine() |> ignore
        do! browser.CloseAsync()
    }
    |> Async.AwaitTask
    |> Async.RunSynchronously

    0
