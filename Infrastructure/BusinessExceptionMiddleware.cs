using CinemaBooking.Domain;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace VegaCinema.Infrastructure;
public class BusinessExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITempDataDictionaryFactory tempData)
    {
        try { await next(context); }
        catch (BusinessException e)
        {
            if (context.Request.Headers.Accept.ToString().Contains("application/json")) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = e.Message }); }
            else
            {
                tempData.GetTempData(context)["Error"] = e.Message;
                var referer = context.Request.Headers.Referer.ToString();
                var path = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Authority == context.Request.Host.Value ? uri.PathAndQuery : "/";
                context.Response.Redirect(path);
            }
        }
    }
}
