namespace PicDrawApp

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.Capacitor
open System.Collections.Generic
open WebSharper.TouchEvents

[<JavaScript>]
module Client =
    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    // This is happening to early in the process - by making this a function, you can delay when this is evaluated.
    // What's happening here is that the thing you are attaching the events to is within a children template node
    // Meaning, this is going to be replaced when you run the Doc.RunById
    let canvas() = As<HTMLCanvasElement>(JS.Document.GetElementById("annotationCanvas"))
    // Make this a function instead - you want to request the fresh context for the operations within the function handlers
    //let ctx = canvas.GetContext("2d")
    let getContext (e: Dom.EventTarget) = As<HTMLCanvasElement>(e).GetContext("2d")

     
    let loadImageOnCanvas (imagePath: string) =
        let img = 
            Elt.img [
                on.load (fun img e ->
                    let canvas = canvas()
                    let ctx = canvas.GetContext "2d"
                    ctx.ClearRect(0.0, 0.0, canvas.Width |> float, canvas.Height |> float)
                    ctx.DrawImage(img, 0.0, 0.0, canvas.Width |> float, canvas.Height |> float)
                )
            ] []

        img.Dom.SetAttribute("src", imagePath)

    let takePicture() = promise {
        let! image = Capacitor.Camera.GetPhoto(Camera.ImageOptions(
            resultType = Camera.CameraResultType.Uri,
            Source = Camera.CameraSource.CAMERA,
            Quality = 90
        ))
        image.WebPath |> loadImageOnCanvas
    } 

    let MouseUpAndOutAction (isDrawing) = 
        Var.Set isDrawing <| false
            
    
    let saveAndShareImage () = promise {
        let date = new Date()
        let canvas = canvas()
        let fileName = $"{date.GetTime()}_image.png"
        let imageData = canvas.ToDataURL("image/png")
        let! savedImage = Capacitor.Filesystem.WriteFile(Filesystem.WriteFileOptions(
            Path = fileName,
            Data = imageData,                                
            Directory = Filesystem.Directory.DOCUMENTS

        ))

        Capacitor.Share.Share(Share.ShareOptions(
            Title = "Check out my annotated picture!",
            Text = "Here is an image I created using PicNote!",
            Url = savedImage.Uri,
            DialogTitle = "Share your creation"
        )) |> ignore

        return savedImage
    }    

    [<SPAEntryPoint>]
    let Main () =
        let isDrawing = Var.Create false
        let lastX, lastY = Var.Create 0.0, Var.Create 0.0

        (*let touchStartEvent () = 
            let touchEvent = new TouchEvent("touchstart")
            
            touchEvent.PreventDefault()
            Var.Set isDrawing <| true
            let touch = touchEvent.Touches[0]
            let rect = canvas.GetBoundingClientRect()
            Var.Set lastX <| touch.ClientX - rect.Left
            Var.Set lastY <| touch.ClientX - rect.Top *)

        

        IndexTemplate.PicNote()
            .CaptureBtn(fun _ -> 
                async {
                    return! takePicture().Then(fun _ -> printfn "Succesfully take or choose a picture").AsAsync()
                }
                |> Async.Start
            )
            .canvasMouseDown(fun e ->
                Var.Set isDrawing <| true
                Var.Set lastX <| e.Event.OffsetX
                Var.Set lastY <| e.Event.OffsetY
            )
            .canvasMouseUp(fun _ -> 
                MouseUpAndOutAction(isDrawing)
            )
            .canvasMouseOut(fun _ ->
                MouseUpAndOutAction(isDrawing)
            )
            .canvasMouseMove(fun e -> 
                let ctx = getContext e.Target
                if isDrawing.Value then
                    ctx.StrokeStyle <- "#FF0000" 
                    ctx.LineWidth <- 2.0 
                    ctx.BeginPath()
                    ctx.MoveTo(lastX.Value, lastY.Value)
                    ctx.LineTo(e.Event.OffsetX, e.Event.OffsetY)
                    ctx.Stroke()
                    Var.Set lastX <| e.Event.OffsetX
                    Var.Set lastY <| e.Event.OffsetY
            )
            .SaveShareBtn(fun _ -> 
                async {
                    return! saveAndShareImage().Then(fun image -> printfn $"Saved Image URL: {image.Uri}").AsAsync()
                }
                |> Async.Start
            )
            .canvasInit(fun () ->
                // Added this ws-onafterrender
                // The function body here is getting executed after the templating engine did it's thing, so accessing the canvas here will get the correct element
                let canvas = canvas()
                canvas.AddEventListener("touchstart", fun (e: Dom.Event) -> 
                    let touchEvent = e |> As<TouchEvent>
                    printfn("touch start")
                    touchEvent.PreventDefault()
                    Var.Set isDrawing <| true
                    let touch = touchEvent.Touches[0]
                    let rect = canvas.GetBoundingClientRect()
                    Var.Set lastX <| touch.ClientX - rect.Left
                    Var.Set lastY <| touch.ClientX - rect.Top 
                )

                canvas.AddEventListener("touchmove", fun (e: Dom.Event) -> 
                    let touchEvent = e |> As<TouchEvent>
                    let ctx = getContext e.Target
                    printfn("touch move")
                    touchEvent.PreventDefault()
                    let touch = touchEvent.Touches[0]
                    let rect = canvas.GetBoundingClientRect()
                    let offsetX = touch.ClientX - rect.Left
                    let offsetY = touch.ClientX - rect.Top
                    if isDrawing.Value then
                        ctx.StrokeStyle <- "#FF0000" 
                        ctx.LineWidth <- 2.0 
                        ctx.BeginPath()
                        ctx.MoveTo(lastX.Value, lastY.Value)
                        ctx.LineTo(offsetX, offsetY)
                        ctx.Stroke()
                        Var.Set lastX <| offsetX
                        Var.Set lastY <| offsetY
                )

                canvas.AddEventListener("touchend", fun (e: Dom.Event) -> 
                    let touchEvent = e |> As<TouchEvent>
                    printfn("touch end")
                    touchEvent.PreventDefault()
                    Var.Set isDrawing <| false
                )
            )
            .Doc()
        |> Doc.RunById "main"        
