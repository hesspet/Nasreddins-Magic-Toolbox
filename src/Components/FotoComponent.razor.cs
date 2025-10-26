using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Toolbox.Helpers;
using Toolbox.Models;

namespace Toolbox.Components;

public partial class FotoComponent : ComponentBase
{
    private const long DefaultMaxFileSize = 10 * 1024 * 1024;

    private ElementReference fileInput;
    private bool isBusy;
    private string? lastError;
    private string? previewImageDataUrl;
    private long effectiveMaxFileSize = DefaultMaxFileSize;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private TemporaryImageStore ImageStore { get; set; } = default!;

    /// <summary>
    ///     Raised when a photo has been captured and stored.
    /// </summary>
    [Parameter]
    public EventCallback<PhotoCaptureResult> OnPhotoCaptured { get; set; }

    /// <summary>
    ///     Raised when an error occurs while capturing the photo.
    /// </summary>
    [Parameter]
    public EventCallback<string?> OnError { get; set; }

    /// <summary>
    ///     Text displayed on the capture button.
    /// </summary>
    [Parameter]
    public string ButtonText { get; set; } = "Foto aufnehmen";

    /// <summary>
    ///     CSS classes applied to the capture button.
    /// </summary>
    [Parameter]
    public string ButtonCssClass { get; set; } = "btn btn-primary";

    /// <summary>
    ///     Accept attribute value for the file input element.
    /// </summary>
    [Parameter]
    public string Accept { get; set; } = "image/*";

    /// <summary>
    ///     Value for the HTML capture attribute (e.g. "environment" or "user").
    /// </summary>
    [Parameter]
    public string CaptureMode { get; set; } = "environment";

    /// <summary>
    ///     Maximum file size in bytes accepted by the component.
    /// </summary>
    [Parameter]
    public long MaxFileSize { get; set; } = DefaultMaxFileSize;

    /// <summary>
    ///     Determines whether the component is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    ///     Whether to show a preview of the captured image.
    /// </summary>
    [Parameter]
    public bool ShowPreview { get; set; } = true;

    /// <summary>
    ///     Text used for the visually hidden spinner description.
    /// </summary>
    [Parameter]
    public string LoadingText { get; set; } = "Foto wird gespeichert …";

    /// <summary>
    ///     Alternative text for the preview image.
    /// </summary>
    [Parameter]
    public string? PreviewAltText { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the component is currently processing a capture.
    /// </summary>
    private bool IsBusy => isBusy;

    private bool IsCaptureDisabled => Disabled || isBusy;

    private string? LastError => lastError;

    private string? PreviewImageDataUrl => previewImageDataUrl;

    private string PreviewAltTextValue => string.IsNullOrWhiteSpace(PreviewAltText)
        ? "Vorschau des aufgenommenen Fotos"
        : PreviewAltText!;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        effectiveMaxFileSize = MaxFileSize > 0
            ? MaxFileSize
            : DefaultMaxFileSize;
    }

    private async Task TriggerCaptureAsync()
    {
        if (IsCaptureDisabled)
        {
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync("fotoComponent.triggerCapture", fileInput);
        }
        catch (JSException jsException)
        {
            lastError = string.Format(CultureInfo.CurrentCulture, "Kamera konnte nicht geöffnet werden: {0}", jsException.Message);

            if (OnError.HasDelegate)
            {
                await OnError.InvokeAsync(lastError);
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandlePhotoSelected(InputFileChangeEventArgs args)
    {
        if (args?.FileCount is null or 0)
        {
            return;
        }

        var file = args.File;
        if (file is null)
        {
            return;
        }

        isBusy = true;
        lastError = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            await using var readStream = file.OpenReadStream(effectiveMaxFileSize);
            using var memoryStream = new MemoryStream();
            await readStream.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();

            var imageId = ImageStore.StoreImage(file.Name, file.ContentType, data);
            var result = new PhotoCaptureResult(imageId, file.Name, file.ContentType, data.LongLength);

            if (ShowPreview)
            {
                previewImageDataUrl = BuildDataUrl(file.ContentType, data);
            }
            else
            {
                previewImageDataUrl = null;
            }

            await OnPhotoCaptured.InvokeAsync(result);
        }
        catch (Exception ex)
        {
            previewImageDataUrl = null;
            lastError = string.Format(CultureInfo.CurrentCulture, "Fehler beim Speichern des Fotos: {0}", ex.Message);

            if (OnError.HasDelegate)
            {
                await OnError.InvokeAsync(lastError);
            }
        }
        finally
        {
            isBusy = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static string BuildDataUrl(string? contentType, byte[] data)
    {
        var type = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
        var base64 = Convert.ToBase64String(data);
        return $"data:{type};base64,{base64}";
    }
}
