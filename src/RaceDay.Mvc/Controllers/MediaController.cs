using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>
/// Serves event banners and profile pictures to the browser. Each request is passed to the API's
/// GET /api/images/{container}/{file}, which reads the image from Azure Blob Storage.
/// The MVC app never talks to Azure itself.
/// </summary>
public class MediaController : Controller
{
    private readonly RaceDayApiClient _api;

    public MediaController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet("media/{container}/{fileName}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Image(string container, string fileName, CancellationToken cancellationToken)
    {
        var response = await _api.GetImageAsync(container, fileName, cancellationToken);
        if (response is null)
        {
            // A missing image shouldn't break the page: send a neutral placeholder instead.
            return Redirect("~/img/placeholder.svg");
        }

        HttpContext.Response.RegisterForDispose(response);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return File(await response.Content.ReadAsStreamAsync(cancellationToken), contentType);
    }
}
