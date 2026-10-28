namespace Mariabot3000.Domain

type CommentId = CommentId of int64 with 
    static member tryParse (s:string) = 
        match System.Int64.TryParse s with
        | true, v -> Some(CommentId v)
        | _ -> None
type ThreadId  = ThreadId of string
type SteamId64 = SteamId64 of string   



type SteamProfile = {
    SteamId64: SteamId64
    Username: string option // <- if the user has set a custom url
    ProfilePath: string
}

type CommentAuthor = {
    steamId64: SteamId64 option
    Username: string option
    DisplayName: string
}

type Comment = {
    CommentId: CommentId
    ThreadId: ThreadId
    Target: SteamProfile
    Author: CommentAuthor
    text: string
    timestamp: string option
}
