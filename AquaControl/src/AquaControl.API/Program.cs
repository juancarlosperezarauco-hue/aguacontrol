using AquaControl.Application;
using AquaControl.Domain;
using AquaControl.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder=WebApplication.CreateBuilder(args);
var sandbox=builder.Environment.IsDevelopment()&&builder.Configuration.GetValue<bool>("Payments:Sandbox");
var connection=builder.Configuration.GetConnectionString("Aqua")??"Server=lpc:localhost;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true";
if(!builder.Environment.IsDevelopment()&&builder.Configuration.GetConnectionString("Aqua")==null)throw new InvalidOperationException("Configure ConnectionStrings:Aqua en producción.");
builder.Services.AddDbContext<AquaDb>(o=>o.UseSqlServer(connection,s=>s.UseNetTopologySuite()));builder.Services.AddScoped<IData>(s=>s.GetRequiredService<AquaDb>());builder.Services.AddScoped<AquaService>();builder.Services.AddScoped<CatalogService>();builder.Services.AddScoped<CommercialImportService>();builder.Services.AddScoped<IGeoReference,GeoReference>();builder.Services.AddScoped<SecurityService>();builder.Services.AddScoped<GeoService>();builder.Services.AddSingleton<IPaymentGateway>(new PaymentGateway(sandbox));
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.SameSite=SameSiteMode.Strict;});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.Cookie.Name="Aqua.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;o.ExpireTimeSpan=TimeSpan.FromHours(2);o.SlidingExpiration=true;o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};o.Events.OnValidatePrincipal=async c=>{var db=c.HttpContext.RequestServices.GetRequiredService<AquaDb>();var id=int.Parse(c.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);var user=await db.Set<User>().FindAsync(id);if(user==null||!user.Active||user.SecurityVersion.ToString()!=c.Principal!.FindFirstValue("sv"))c.RejectPrincipal();};});
builder.Services.AddAuthorization();builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("login",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.PropertyNamingPolicy=JsonNamingPolicy.CamelCase);
var app=builder.Build();
if(args.Contains("--init")||args.Contains("--schema")||args.Contains("--import-sig")||args.Contains("--validate-sig")){
 using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AquaDb>();
 if(args.Contains("--schema")){Console.WriteLine(db.Database.GenerateCreateScript());return;}
 if(args.Contains("--init")){if(!connection.Contains("AquaControl",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Inicialización requiere una base AquaControl nueva, nunca VisorDatosSIG.");await db.Database.EnsureCreatedAsync();await scope.ServiceProvider.GetRequiredService<SecurityService>().Seed(Environment.GetEnvironmentVariable("AQUA_BOOTSTRAP_PASSWORD"));Console.WriteLine("AquaControl inicializado.");}
 if(args.Contains("--import-sig")){var folder=Environment.GetEnvironmentVariable("AQUA_SIG_PATH")??throw new InvalidOperationException("Defina AQUA_SIG_PATH.");await scope.ServiceProvider.GetRequiredService<GeoService>().Import(folder);}
 if(args.Contains("--validate-sig")){var path=Environment.GetEnvironmentVariable("AQUA_SIG_REPORT")??throw new InvalidOperationException("Defina AQUA_SIG_REPORT.");var report=await scope.ServiceProvider.GetRequiredService<GeoService>().Validate();await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));Console.WriteLine("Informe SIG: "+Path.GetFullPath(path));}
 return;
}
if(!app.Environment.IsDevelopment()){app.UseHsts();app.UseHttpsRedirection();}
app.Use(async(c,next)=>{c.Response.Headers["X-Content-Type-Options"]="nosniff";c.Response.Headers["Referrer-Policy"]="same-origin";c.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https://*.tile.openstreetmap.org; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";try{await next();}catch(Exception ex){if(c.Response.HasStarted)throw;var code=ex switch{BusinessException=>400,UnauthorizedAccessException=>403,DbUpdateConcurrencyException=>409,DbUpdateException=>409,AntiforgeryValidationException=>400,JsonException=>400,_=>500};if(code==500)app.Logger.LogError(ex,"Error {Trace}",c.TraceIdentifier);c.Response.StatusCode=code;await c.Response.WriteAsJsonAsync(new{error=code==500?"Error interno. Consulte el identificador en bitácora.":ex is DbUpdateException?"Conflicto de integridad: registro duplicado, referencia inválida o cambio concurrente.":ex.Message,trace=c.TraceIdentifier});}});
app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
app.Use(async(c,next)=>{if(c.Request.Path.StartsWithSegments("/api")&&c.Request.Method is not ("GET" or "HEAD" or "OPTIONS")){await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);}if(c.User.Identity?.IsAuthenticated==true&&c.Request.Path.StartsWithSegments("/api")&&!c.Request.Path.StartsWithSegments("/api/auth")){var db=c.RequestServices.GetRequiredService<AquaDb>();var user=await db.Set<User>().FindAsync(int.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!));if(user?.MustChangePassword==true){c.Response.StatusCode=403;await c.Response.WriteAsJsonAsync(new{error="Debe cambiar su contraseña antes de continuar."});return;}}await next();});
var webRoot=Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath,"../../Web"));if(!Directory.Exists(webRoot))webRoot=Path.Combine(AppContext.BaseDirectory,"wwwroot");app.UseDefaultFiles(new DefaultFilesOptions{FileProvider=new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot)});app.UseStaticFiles(new StaticFileOptions{FileProvider=new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot)});
app.MapGet("/api/auth/csrf",(HttpContext c,IAntiforgery f)=>Results.Ok(new{token=f.GetAndStoreTokens(c).RequestToken}));
app.MapPost("/api/auth/login",async(LoginRequest r,HttpContext c,SecurityService security)=>{var user=await security.Login(r.Login,r.Password);if(user==null)return Results.Json(new{error="Usuario o contraseña incorrectos, o cuenta bloqueada."},statusCode:401);var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.Name),new Claim("sv",user.SecurityVersion.ToString())},CookieAuthenticationDefaults.AuthenticationScheme);await c.SignInAsync(new ClaimsPrincipal(identity));return Results.Ok(new{user.Name,user.MustChangePassword});}).RequireRateLimiting("login");
app.MapPost("/api/auth/logout",async(HttpContext c)=>{await c.SignOutAsync();return Results.Ok();}).RequireAuthorization();
app.MapGet("/api/auth/me",async(HttpContext c,AquaService s,SecurityService sec,AquaDb db)=>{var a=await Api.Actor(c,s);var user=await db.Set<User>().FindAsync(a.Id);return Results.Ok(new{a.Id,name=c.User.Identity!.Name,permissions=a.Permissions,sandbox,mustChangePassword=user!.MustChangePassword});}).RequireAuthorization();
app.MapPost("/api/auth/password",async(PasswordRequest r,HttpContext c,AquaService s,SecurityService sec)=>{await sec.ChangePassword(await Api.Actor(c,s),r.Current,r.Next);await c.SignOutAsync();return Results.Ok();}).RequireAuthorization();
Api.Map(app,sandbox);
app.Run();
record LoginRequest(string Login,string Password);
record PasswordRequest(string Current,string Next);
