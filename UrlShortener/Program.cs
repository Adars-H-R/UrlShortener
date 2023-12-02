using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ILiteDatabase, LiteDatabase>(_ => new LiteDatabase("shorturl.db"));
var app = builder.Build();
app.MapPost("/url", ShortnerDelegate);
app.MapFallback(RedirectDelegate);
app.Map("/", ctx =>
{
    ctx.Response.ContentType = "text/html";
    return ctx.Response.SendFileAsync("htmlpage.html");
});
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



await app.RunAsync();


static async Task ShortnerDelegate(HttpContext context)
{
    var shortUrlDto = await context.Request.ReadFromJsonAsync<ShortUrlDto>() ?? new ShortUrlDto();
    if (!Uri.TryCreate(shortUrlDto.Url, UriKind.Absolute, out Uri validUrl))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid Url");
        return;
    }

    var db = context.RequestServices.GetRequiredService<ILiteDatabase>();
    var entity = db.GetCollection<ShortUrl>(BsonAutoId.Int64);
    var shortUrl = new ShortUrl(shortUrlDto.Url);
    entity.Insert(shortUrl);
    var response = $"{context.Request.Scheme}://{context.Request.Host}/{shortUrl.Chunk}";
    await context.Response.WriteAsJsonAsync(new { url = response });
}

static async Task RedirectDelegate(HttpContext context)
{
    var db = context.RequestServices.GetRequiredService<ILiteDatabase>();
    var collection = db.GetCollection<ShortUrl>();
    var chunk = context.Request.Path.ToUriComponent().Trim('/');
    var dbId = BitConverter.ToInt64(WebEncoders.Base64UrlDecode(chunk));
    var actualUrl = collection.FindById(dbId);
    context.Response.Redirect(actualUrl?.Url ?? "/");
    await Task.CompletedTask;
}

public class ShortUrl
{
    public ShortUrl(string url)
    {
        Url = url;
    }
    public long Id { get; set; }
    public string Url { get; set; }
    public string Chunk => WebEncoders.Base64UrlEncode(BitConverter.GetBytes(Id));
}

public class ShortUrlDto
{
    public string Url { get; set; }
}