using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SICOVWEB_MCA.Models;
using System.Net.Mail;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore.Internal;


namespace SICOVWEB_MCA.Controllers

{
    public class Login_Controlador : Controller
    {
        public bool SesionActiva { get; set; } = false;

        public int UsuarioActivoId { get; set; }

        public string UsuarioActivoNombre { get; set; }

        public string UsuarioActivoTipo { get; set; }

        public int UsuarioActivoEmpleadoId { get; set; }

        private readonly ApplicationDbContext _context;//Permiso de solo lectura al contexto DB
        private readonly IConfiguration _configuration; // Permiso de solo lectura a la configuración
        public Login_Controlador(ApplicationDbContext context , IConfiguration configuration)//Constructor usa el contexto como parametro
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        [Authorize]  
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult PrincipalAdmin()
        {
            // La cookie [Authorize] ya garantiza autenticación
            // Cargar sesión por si no existe (caso de refresh de página)
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null)
            {
                // Si no hay sesión pero hay cookie, recrear la sesión
                var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(usuarioIdClaim) && int.TryParse(usuarioIdClaim, out int usuarioId))
                {
                    HttpContext.Session.SetInt32("IdUsuario", usuarioId);
                    HttpContext.Session.SetString("UsuarioNombre", User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario");
                    HttpContext.Session.SetString("UsuarioRol", User.FindFirst(ClaimTypes.Role)?.Value ?? "");
                    HttpContext.Session.SetInt32("EmpleadoId", int.Parse(User.FindFirst(ClaimTypes.Sid)?.Value ?? "0"));
                }
                else
                {
                    return RedirectToAction("Logout");
                }
            }

            CargarMetricasUsuario();
            CargarMetricasDashboard(); // Cargar las métricas del dashboard

            return View();
        }

        // Metodo para volver a la vista de inicio
        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult VolverInicio()
        {
            CargarMetricasUsuario();
            CargarMetricasDashboard(); // Cargar las métricas del dashboard
            return View("~/Views/Shared/PrincipalAdmin.cshtml");
        }

        // Método para mostrar la vista para Registrar un usuario nuevo
        [HttpGet]
        [Authorize(Roles = "admin")] // Asegura que solo los administradores puedan acceder
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Registrar()
        {
            return View("~/Views/Home/Registrar.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> LoginAsync()
        {
            try
            {
                // Solicitar valores del formulario
                string CorreoUsuario = Request.Form["CorreoUsuario"];
                string Contrasena = Request.Form["Contrasena"];
                string token = Request.Form["g-recaptcha-response"];

                //Verificar el captcha descomentar antes de publicar en Azurewebsites
                bool captchaValido = await VerificarCaptchaAsync(token);
                if (!captchaValido)
                {
                    //ViewBag.ErrorCaptcha = "Captcha inválido. Por favor, inténtelo de nuevo.";
                    TempData["MensajeAlertFalla"] = "Captcha inválido. Por favor, inténtelo de nuevo.";
                    return View("~/Views/Home/Index.cshtml");
                }

                // Verificar las credenciales del usuario
                var usuario = _context.Usuarios.FirstOrDefault(u => u.CorreoUsuario == CorreoUsuario);

                if (usuario != null)
                {
                    // Asignar el ID y nombre del usuario activo
                    UsuarioActivoId = usuario.Id;
                    // Encontrar el nombre + Apellido_Paterno + Apellido_Materno del empleado relacionado al usuarioId
                    var empleado = _context.Empleados.FirstOrDefault(e => e.Id == usuario.EmpleadoId);
                    if (empleado != null)
                    {
                        UsuarioActivoNombre = $"{empleado.Nombre} {empleado.Apellido_Paterno} {empleado.Apellido_Materno}";
                        UsuarioActivoEmpleadoId = empleado.Id; // Asignar el Id del empleado activo
                        UsuarioActivoTipo = usuario.TipoUsuario; // Asignar el tipo de usuario activo
                    }
                    else
                    {
                        UsuarioActivoNombre = "Usuario";
                    }

                    // Verificar si la contraseña es correcta
                    var hasher = new PasswordHasher<Usuario>();
                    var resultado = hasher.VerifyHashedPassword(usuario, usuario.Contrasena, Contrasena);

                    if (resultado == PasswordVerificationResult.Success)
                    {
                        // Crear lista de Claims
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, UsuarioActivoNombre),
                            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                            new Claim(ClaimTypes.Role, usuario.TipoUsuario),
                            new Claim(ClaimTypes.Sid , usuario.EmpleadoId.ToString())
                        };

                        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var principal = new ClaimsPrincipal(identity);

                        // Crear cookie de autenticación
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                                                
                        // GUARDAR LA SESIÓN AQUÍ
                        
                        HttpContext.Session.SetInt32("IdUsuario", usuario.Id);
                        HttpContext.Session.SetString("UsuarioNombre", UsuarioActivoNombre);
                        HttpContext.Session.SetString("UsuarioRol", usuario.TipoUsuario);
                        HttpContext.Session.SetInt32("EmpleadoId", usuario.EmpleadoId);
                        // =============================

                        TempData["MensajeAlertExito"] = "Bienvenido: " + UsuarioActivoNombre;

                        // Redirección según rol
                        //return RedirectToAction("PrincipalAdmin", "Login_Controlador");
                        if (usuario.TipoUsuario == "admin")
                        {                            
                            return RedirectToAction("PrincipalAdmin", "Login_Controlador");
                        }
                        else if (usuario.TipoUsuario == "normal" || usuario.TipoUsuario == "Proveedor")
                        {
                            return RedirectToAction("PrincipalAdmin", "Login_Controlador");
                        }
                    }

                }

                // Credenciales incorrectas                
                TempData["MensajeAlertFalla"] = "Credenciales incorrectas. Por favor, inténtelo de nuevo.";
                SesionActiva = false;
                return View("~/Views/Home/Index.cshtml");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMensaje = "Error al iniciar sesión: " + ex.Message;
                SesionActiva = false;
                return View("~/Views/Home/Index.cshtml");
            }
        }

        // Método para cerrar sesión
        public async Task<IActionResult> Logout()
        {
            // Cerrar la sesión y eliminar la cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Limpiar la sesión 
            HttpContext.Session.Clear();

            // Limpiar las variables de sesión
            SesionActiva = false;
            UsuarioActivoId = 0;
            UsuarioActivoNombre = string.Empty;
            UsuarioActivoTipo = string.Empty;
            // Limpiar TempData para evitar que se muestre el mensaje en la siguiente solicitud
            TempData.Clear();
            // Mostrar mensaje de cierre de sesión exitoso
            TempData["MensajeAlertExito"] = "Sesión cerrada correctamente.";
            // Redirigir al usuario a la página de inicio o de login
            return RedirectToAction("Login", "Login_Controlador");
        }

        // Método para mostrar la vista de inicio de sesión en caso de recibir una entrada inválida
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string entradaInvalida)
        {
            if (entradaInvalida == "true")
            {
                TempData["MostrarModal"] = "Se detectó una entrada inválida. Por seguridad, se descartaron los datos ingresados.";
            }
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("PrincipalAdmin", "Login_Controlador");
            }

            return View("~/Views/Home/Index.cshtml");
        }

        //Metodo para mostrar la vista de acceso denegado
        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult AccesoDenegado()
        {
            TempData["MensajeAlertFalla"] = "Acceso denegado. No tienes permisos para acceder a esta página.";
            return RedirectToAction("VolverInicio");
        }

        // Metodo para recuperar la contraseña
        public async Task<IActionResult> Recuperar(string CorreoUsuario)
        {
            // Verificar si el correo existe en la base de datos
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.CorreoUsuario == CorreoUsuario);


            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "El correo no está asociado a ningún usuario.");
                Console.WriteLine("El correo no está asociado a ningún usuario.");
                return View("~/Views/Home/Index.cshtml");
            }
            var hasher = new PasswordHasher<Usuario>();
            // Si el usuario existe, obtener la contraseña 
            string contrasenaDesencriptada = hasher.VerifyHashedPassword(usuario, usuario.Contrasena, usuario.Contrasena).ToString();


            // Obtener el nombre del empleado relacionado
            var empleado = await _context.Empleados.FirstOrDefaultAsync(e => e.Id == usuario.EmpleadoId);
            if (empleado == null)
            {
                ModelState.AddModelError(string.Empty, "No se encontró información del empleado asociado.");
                Console.WriteLine("No se encontró información del empleado asociado.");
                return View("~/Views/Home/Index.cshtml");
            }

            // Configurar el cliente SMTP
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("sicovwebsoporte@gmail.com", "jsbn bowq cnkn rjny"),
                EnableSsl = true,
            };

            // Crear el mensaje de correo
            var mailMessage = new MailMessage
            {
                From = new MailAddress("sicovwebsoporte@gmail.com"),
                Subject = "Recuperación de contraseña",
                Body = $"Hola {empleado.Nombre},\n\nTu contraseña es: {contrasenaDesencriptada}\n\nPor favor, cámbiala después de iniciar sesión.",
                IsBodyHtml = false,
            };
            mailMessage.To.Add(CorreoUsuario);

            try
            {
                // Enviar el correo
                await smtpClient.SendMailAsync(mailMessage);
                Console.WriteLine("Correo enviado");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error al enviar el correo: " + ex.Message);
                Console.WriteLine("Error al enviar el correo");
                Console.WriteLine("SMTP Error: " + ex.Message);
                return View("~/Views/Home/Index.cshtml");
            }

            return View("~/Views/Home/Index.cshtml");
        }

        // Método para verificar el captcha
        private async Task<bool> VerificarCaptchaAsync(string token)
        {
            // Obtener la clave secreta de reCAPTCHA v2 desde secrets          
            var secretKey = _configuration.GetSection("ReCaptcha").GetValue<string>("SecretKey");  // Clave secreta de reCAPTCHA v2 para localhost
                var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}", // URL de verificación del captcha
                null);

            var json = await response.Content.ReadAsStringAsync(); // Leer la respuesta como cadena
            var resultado = JsonSerializer.Deserialize<JsonElement>(json);
            return resultado.GetProperty("success").GetBoolean();// Verificar si la respuesta es exitosa
        }

        // Metodo para llenar datos del Dashboard
        private void CargarMetricasDashboard()
        {
            // Metricas SICOVWEB //
            // Total de cotizaciones en el sistema
            ViewBag.CotTotal = _context.Cotizaciones.Count();
            // Total de clientes en el sistema
            ViewBag.TotalClientes = _context.Clientes.Count();
            // Total de empleados en el sistema
            ViewBag.TotalEmpleados = _context.Empleados.Count();
            // Total de productos en el sistema
            ViewBag.TotalProductos = _context.Productos.Count();

            // Año actual
            var fechaInicioAnio = new DateTime(DateTime.Now.Year, 1, 1);
            var fechaFinAnio = fechaInicioAnio.AddYears(1).AddDays(-1);
            ViewBag.CotAnual = _context.Cotizaciones
                .Count(c => c.Fecha >= fechaInicioAnio && c.Fecha <= fechaFinAnio);

            // Mes actual
            var fechaInicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var fechaFinMes = fechaInicioMes.AddMonths(1).AddDays(-1);
            ViewBag.CotMensual = _context.Cotizaciones
                .Count(c => c.Fecha >= fechaInicioMes && c.Fecha <= fechaFinMes);

            // Cotizaciones vigentes
            var vigentes = _context.Cotizaciones
                .Count(c => c.FechaVigencia >= DateTime.Now);

            // Cotizaciones vencidas
            var vencidas = _context.Cotizaciones
                .Count(c => c.FechaVigencia < DateTime.Now);

            // Cotizaciones asignadas
            var asignadas = _context.Cotizaciones
                .Count(c => c.Estatus == "pendiente");

            // Cotizaciones cerradas
            var cerradas = _context.Cotizaciones
                .Count(c => c.Estatus == "aceptada");

            // 1. Datos de cotizaciones para grafica de pastel
            ViewBag.CotizacionesData = new int[] {
                vigentes, vencidas, asignadas, cerradas
            };


            // Productos con Stock bajo
            ViewBag.ProductosStockBajo = _context.Productos.Where(p => p.StockActual <= p.StockMinimo).ToList();

            // --- Datos para el gráfico ---
            var cotizacionesPorMes = _context.Cotizaciones
                .Where(c => c.Fecha >= fechaInicioAnio && c.Fecha <= fechaFinAnio)
                .GroupBy(c => c.Fecha.Month)
                .Select(g => new { Mes = g.Key, Cantidad = g.Count() })
                .ToList();

            // Crear un arreglo de 12 posiciones (enero a diciembre)
            int[] cantidades = new int[12];
            foreach (var item in cotizacionesPorMes)
            {
                cantidades[item.Mes - 1] = item.Cantidad;
            }

            ViewBag.CotizacionesPorMes = cantidades;

            // Productos con más ventas (sumatoria de cantidad vendida)
            var productosMasVendidos = _context.DetalleVentas
                .GroupBy(d => d.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    CantidadVendida = g.Sum(x => x.Cantidad)
                })
                .Join(_context.Productos,
                    dv => dv.IdProducto,
                    p => p.Id_producto,
                    (dv, p) => new
                    {
                        p.Nombre,
                        dv.CantidadVendida
                    })
                .OrderByDescending(x => x.CantidadVendida)
                .Take(10)
                .ToList();

            ViewBag.ProductosVendidosLabels = productosMasVendidos.Select(x => x.Nombre).ToArray();
            ViewBag.ProductosVendidosValues = productosMasVendidos.Select(x => x.CantidadVendida).ToArray();


            // Productos con mayor margen de ganancia
            var productosMargen = _context.Productos
                .OrderByDescending(p => p.Margen)
                .Take(5)
                .Select(p => new
                {
                    p.Nombre,
                    p.Margen
                })
                .ToList();

            ViewBag.ProductosMargenLabels = productosMargen.Select(x => x.Nombre).ToArray();
            ViewBag.ProductosMargenValues = productosMargen.Select(x => x.Margen).ToArray();
           


            // Clientes con más compras (se cuentan ventas)
            var clientesMasCompras = _context.Ventas
                .GroupBy(v => v.Id_Cliente)
                .Select(g => new
                {
                    IdCliente = g.Key,
                    Compras = g.Count()
                })
                .Join(_context.Clientes,
                    vc => vc.IdCliente,
                    c => c.Id_cliente,
                    (vc, c) => new
                    {
                        c.Razon_Social,
                        vc.Compras
                    })
                .OrderByDescending(x => x.Compras)
                .Take(10)
                .ToList();

            ViewBag.ClientesLabels = clientesMasCompras.Select(x => x.Razon_Social).ToArray();
            ViewBag.ClientesValues = clientesMasCompras.Select(x => x.Compras).ToArray();

            // Ultimos 5 clientes agregados seleccionar fecha de alta y Razon_Social
            var ultimosClientes = _context.Clientes
                .OrderByDescending(c => c.Fecha_Alta)
                .Take(5)
                .Select(c => new
                {
                    c.Razon_Social,
                    c.Fecha_Alta
                })
                .ToList();
            ViewBag.UltimosClientes = ultimosClientes;
            Console.WriteLine("Ultimos Clientes: " + ultimosClientes.Count);



            // Empleados con más ventas realizadas
            var empleadosMasVentas = _context.Ventas
                .GroupBy(v => v.IdEmpleado)
                .Select(g => new
                {
                    IdEmpleado = g.Key,
                    Ventas = g.Count()
                })
                .Join(_context.Empleados,
                    vv => vv.IdEmpleado,
                    e => e.Id,
                    (vv, e) => new
                    {
                        e.Nombre,
                        vv.Ventas
                    })
                .OrderByDescending(x => x.Ventas)
                .Take(3)
                .ToList();

            ViewBag.EmpleadosLabels = empleadosMasVentas.Select(x => x.Nombre).ToArray();
            ViewBag.EmpleadosValues = empleadosMasVentas.Select(x => x.Ventas).ToArray();
            

        }

        public void CargarMetricasUsuario()
        {
            int idEmpleado = int.Parse(User.FindFirst(ClaimTypes.Sid)?.Value);

            // Ventas realizadas por el usuario
            ViewBag.VentasRealizadas = _context.Ventas
                .Count(v => v.IdEmpleado == idEmpleado);

            // Cotizaciones totales realizadas por el usuario
            ViewBag.CotizacionesTotales = _context.Cotizaciones
                .Count(c => c.IdEmpleado2 == idEmpleado);

            // Cotizaciones aceptadas
            ViewBag.CotizacionesAceptadas = _context.Cotizaciones
                .Count(c => c.IdEmpleado2 == idEmpleado && c.Estatus == "aceptada");

            // Total vendido este mes
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var finMes = inicioMes.AddMonths(1).AddDays(-1);

            ViewBag.TotalVendidoMes = _context.Ventas
                .Where(v => v.IdEmpleado == idEmpleado &&
                            v.Fecha_Venta >= inicioMes &&
                            v.Fecha_Venta <= finMes)
                .Join(_context.DetalleVentas,
                      v => v.Id_venta,
                      d => d.Id_venta,
                      (v, d) => d.Subtotal)
                .Sum();

        }
    }
}

