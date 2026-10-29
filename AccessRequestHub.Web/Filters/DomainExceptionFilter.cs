using AccessRequestHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AccessRequestHub.Web.Filters;

/// <summary>
/// Memetakan exception domain ke halaman error dengan status code yang benar (SPECS 3.8).
/// 400 dan 409 ditangani di controller karena butuh re-render halaman detail; filter ini
/// menangani 403 dan 404, dan sisanya dibiarkan menjadi 500.
/// </summary>
public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ForbiddenException forbidden:
                context.Result = ErrorPage(context, "Forbidden", 403, forbidden.Message);
                break;

            case NotFoundException notFound:
                context.Result = ErrorPage(context, "NotFound", 404, notFound.Message);
                break;
        }
    }

    private static ViewResult ErrorPage(ExceptionContext context, string view, int statusCode, string message)
    {
        context.HttpContext.Response.StatusCode = statusCode;
        context.ExceptionHandled = true;

        return new ViewResult
        {
            ViewName = $"~/Views/Shared/{view}.cshtml",
            StatusCode = statusCode,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), context.ModelState)
            {
                ["Message"] = message,
                ["StatusCode"] = statusCode
            }
        };
    }
}
