namespace PicDrawApp

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.Capacitor

[<JavaScript>]
module Client =
    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    let People =
        ListModel.FromSeq [
            "John"
            "Paul"
        ]

    let canvas = As<HTMLCanvasElement>(JS.Document.GetElementById("annotationCanvas"))
    let ctx = canvas.GetContext("2d")
     
    let loadImageOnCanvas (imagePath: string) =
        let img = 
            Elt.img [
                on.load (fun img _ ->
                    ctx.ClearRect(0.0, 0.0, canvas.Width |> float, canvas.Height |> float)
                    ctx.DrawImage(img, 0.0, 0.0, canvas.Width |> float, canvas.Height |> float)
                )
            ] []

        img.Dom.SetAttribute("src", imagePath)

    let takePicture() = promise {
        let! image = Capacitor.Camera.GetPhoto(Camera.ImageOptions(
            resultType = Camera.CameraResultType.Uri,
            Source = Camera.CameraSource.Camera,
            Quality = 90
        ))
        image.WebPath |> loadImageOnCanvas
    } 

    let MouseUpAndOutAction (isDrawing) = 
        Var.Set isDrawing <| false

    let saveAndShareImage () = promise {
        let imageData = canvas.ToDataURL("image/png")

        let! savedImage = Capacitor.Filesystem.WriteFile(Filesystem.WriteFileOptions(
            Path = "annotated_image.png",
            Data = imageData,
            Directory = Filesystem.Directory.Documents
        ))

        Capacitor.Share.Share(Share.ShareOptions(
            Title = "Check out my annotated picture!",
            Text = "Here is an image I created using PicNote!",
            Url = savedImage.Uri,
            DialogTitle = "Share your creation"
        )) |> ignore
    }

    [<SPAEntryPoint>]
    let Main () =
        let newName = Var.Create ""
        let isDrawing = Var.Create false
        let lastX, lastY = Var.Create 0.0, Var.Create 0.0

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
                    return! saveAndShareImage().Then(fun _ -> printfn "Successfully save and share").AsAsync()
                }
                |> Async.Start
            )
            .Doc()
        |> Doc.RunById "main"        
