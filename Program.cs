using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuestPDF.Infrastructure; // Asegúrate de tener este using
using Rotativa.AspNetCore;
using SICOVWEB_MCA.Helpers;
using SICOVWEB_MCA.Models;

var builder = WebApplication.CreateBuilder(args);
// Agregar secretos de usuario
builder.Configuration.AddUserSecrets<Program>();
// Configurar licencia de QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// Agregar servicios de sesion
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Tiempo de expiración de la sesión
    options.Cookie.HttpOnly = true; // Cookie solo accesible por HTTP
    options.Cookie.IsEssential = true; // Cookie esencial para la aplicación
});

// Agregar servicios a el contenedor.
//builder.Services.AddControllersWithViews();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new ResponseCacheAttribute
    {
        NoStore = true,
        Location = ResponseCacheLocation.None
    });
});


//Agrega autenticación y autorización
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Login_Controlador/Login";
        options.AccessDeniedPath = "/Login_Controlador/AccesoDenegado";
    });


//Conexion localhost descomentar para conectar en local 
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("MySQLConnection"),
    new MySqlServerVersion(new Version(8, 0, 29))));


//Conexion para el sitio web en AZURE descomentar para conectar cuando se publican los cambios 

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//   options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
//       ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    
    app.UseHsts();
}

app.UseHttpsRedirection(); // Redirecciona a HTTPS
app.UseStaticFiles(); // Sirve archivos estáticos

app.UseRouting(); // Configura el enrutamiento




app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

app.UseMiddleware<ValidadorDatos>(); //Llamar al middleware de validacion de datos
//app.UseMiddleware<SICOVWEB_MCA.NoCacheMiddleware>();//Evita navegación con flechas "atrás" luego de logout
app.UseSession(); // Habilita el uso de sesiones

app.UseAuthentication(); // Habilita la autenticación
app.UseAuthorization(); // Habilita la autorización

app.MapControllerRoute( // Configura las rutas de los controladores
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


