using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using Rotativa.AspNetCore;
using SICOVWEB_MCA.Extensions;
using SICOVWEB_MCA.Helpers;
using SICOVWEB_MCA.Models;
using SICOVWEB_MCA.PDFDocs;

using SICOVWEB_MCA.Models.ViewModels;
using System.Net.NetworkInformation;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SICOVWEB_MCA.Controllers
{
    public class CotizacionesController : Controller
    {
        private readonly ApplicationDbContext _context;//Permiso de solo lectura al contexto DB

        public CotizacionesController(ApplicationDbContext context)//Constructor usa el contexto como parametro
        {
            _context = context;
        }
                
        [HttpGet] //Para mostrar vista CrearCotización
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CrearCotizacion()
        {
            // Cargar los datos necesarios para el formulario
            ViewBag.listaClientes = _context.Clientes.ToList(); // Lista de clientes
            ViewBag.listaContactos = _context.Contactos_cliente.ToList(); // Lista de contactos
            ViewBag.listaEmpleados = _context.Empleados.Select(e => new
            {
                Id = e.Id,
                NombreCompleto = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
            }).ToList();
            // Cargar lista de productos
            ViewBag.listaProductos = _context.Productos.ToList();

            return View();
        }


        // Metodo para crear una nueva cotización
        [HttpPost]
        [ValidateAntiForgeryToken] //Protege contra ataques CSRF
        [Authorize]
        public IActionResult CrearCotizacion(Cotizacion cotizacion)
        {
            //Asignar el EmpleadoId del usuario autenticado           
            cotizacion.IdEmpleado2 = User.GetEmpleadoId().Value;
            // Obtener el Contacto_cliente del formulario
            cotizacion.Contacto = _context.Contactos_cliente.FirstOrDefault(c => c.Id_contacto == cotizacion.IdContacto);


            try
            {
                if (ModelState.IsValid)
                {
                    _context.Cotizaciones.Add(cotizacion);
                    _context.SaveChanges();

                    TempData["MensajeAlertExito"] = "Cotización creada exitosamente.";
                    // Retornar la vista con la cotización recién creada                    
                    return RedirectToAction("VistaCotizacion", cotizacion);
                }
                else
                {
                    TempData["MensajeAlertFalla"] = "Error: Verifica los datos ingresados.";
                    // Recargar listas para la vista
                    ViewBag.listaClientes = _context.Clientes.ToList();
                    ViewBag.listaContactos = _context.Contactos_cliente.ToList();
                    ViewBag.listaEmpleados = _context.Empleados.Select(e => new
                    {
                        Id = e.Id,
                        NombreCompleto = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                    }).ToList();
                    // Cargar lista de productos
                    ViewBag.listaProductos = _context.Productos.ToList();
                    return View(cotizacion); // Devuelve la vista con el modelo para mostrar errores
                }
            }
            catch (Exception ex)
            {
                TempData["MensajeAlertFalla"] = "Error al crear la cotización: " + ex.Message;
                // Recargar listas
                ViewBag.listaClientes = _context.Clientes.ToList();

                ViewBag.listaContactos = _context.Contactos_cliente.ToList();
                ViewBag.listaEmpleados = _context.Empleados.Select(e => new
                {
                    Id = e.Id,
                    NombreCompleto = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                }).ToList();
                // Cargar lista de productos
                ViewBag.listaProductos = _context.Productos.ToList();

                return View(cotizacion);
            }
        }

        [HttpGet]
        public JsonResult GetContactosPorCliente(int idCliente)
        {
            var contactos = _context.Contactos_cliente
                .Where(c => c.Id_Cliente == idCliente)
                .Select(c => new { c.Id_contacto, NombreCompleto = c.Nombre + " " + c.Apellido_paterno + " " + c.Apellido_materno })
                .ToList();
            
            return Json(contactos);
        }

        public JsonResult GetProductos()
        {
            var productos = _context.Productos.Select(p => new {
                IdProducto = p.Id_producto,
                NombreProducto = p.Nombre,
                Descripcion = p.Descripcion,
                Unidad = p.Unidad,
                PrecioVenta = p.Precio_Venta
            }).ToList();

            return Json(productos);
        }

        // Metodo Consultar cotizaciones con paginación
        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Consultar(
    int? idCotizacion,
    int? clienteId,
    int? empleadoId,
    DateTime? fechaInicio,
    DateTime? fechaFin,
    string estatus,
    int pagina = 1)
        {
            int tamanioPagina = 10; // registros por página

            // Cargar listas para filtros desplegables
            ViewBag.Clientes = _context.Clientes
                .Select(c => new SelectListItem
                {
                    Value = c.Id_cliente.ToString(),
                    Text = c.Razon_Social
                })
                .ToList();

            ViewBag.Empleados = _context.Empleados
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                })
                .ToList();

            // Construir consulta base
            var query = _context.Cotizaciones
                .Include(c => c.Cliente)
                .Include(c => c.Empleado)
                .AsQueryable();

            // Aplicar filtros dinámicos
            if (idCotizacion.HasValue)
                query = query.Where(c => c.IdCotizacion == idCotizacion);

            if (clienteId.HasValue)
                query = query.Where(c => c.IdCliente == clienteId);

            if (empleadoId.HasValue)
                query = query.Where(c => c.IdEmpleado2 == empleadoId);

            if (fechaInicio.HasValue)
                query = query.Where(c => c.Fecha >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(c => c.Fecha <= fechaFin.Value);

            if (!string.IsNullOrEmpty(estatus))
                query = query.Where(c => c.Estatus == estatus);

            query = query.OrderByDescending(c => c.Fecha);

            // Aplicar paginación
            var listaPaginada = await Paginacion<Cotizacion>.CrearAsync(query, pagina, tamanioPagina);

            // Mantener los filtros seleccionados al cambiar de página
            ViewBag.Filtros = new
            {
                idCotizacion,
                clienteId,
                empleadoId,
                fechaInicio = fechaInicio?.ToString("yyyy-MM-dd"),
                fechaFin = fechaFin?.ToString("yyyy-MM-dd"),
                estatus
            };

            return View("ListaCotizaciones", listaPaginada);
        }

        // ===> Metodos usados para los botones Mas información del Dashboard <===
        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ListaCotVigentes(int pagina = 1)
        {
            int tamañoPagina = 10;
            // Cargar listas para filtros desplegables
            ViewBag.Clientes = _context.Clientes
                .Select(c => new SelectListItem
                {
                    Value = c.Id_cliente.ToString(),
                    Text = c.Razon_Social
                })
                .ToList();

            ViewBag.Empleados = _context.Empleados
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                })
                .ToList();

            
            var query = _context.Cotizaciones
                .Include(c => c.Cliente)
                .Include(c => c.Empleado)
                .Where(c => c.FechaVigencia >= DateTime.Now)
                .OrderByDescending(c => c.Fecha)
                .AsQueryable();

            // Aplicar paginación directamente sobre la consulta EF
            var listaPaginada = await Paginacion<Cotizacion>.CrearAsync(query, pagina, tamañoPagina);
           
            return View("ListaCotizaciones", listaPaginada);
        }


        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ListaCotVencidas(int pagina = 1)
        {
            int tamañoPagina = 10;
            // Cargar listas para filtros desplegables
            ViewBag.Clientes = _context.Clientes
                .Select(c => new SelectListItem
                {
                    Value = c.Id_cliente.ToString(),
                    Text = c.Razon_Social
                })
                .ToList();

            ViewBag.Empleados = _context.Empleados
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                })
                .ToList();

            var query = _context.Cotizaciones
               .Include(c => c.Cliente)
               .Include(c => c.Empleado)
               .Where(c => c.FechaVigencia <= DateTime.Now)
               .OrderByDescending(c => c.Fecha)
               .AsQueryable();

            
            var listaPaginada = await Paginacion<Cotizacion>.CrearAsync(query, pagina, tamañoPagina);            
            
            return View("ListaCotizaciones", listaPaginada); // Retorna la vista con la lista de cotizaciones vigentes
        }

        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ListaCotAsignadas(int pagina = 1)
        {
            int tamañoPagina = 10;
            // Cargar listas para filtros desplegables
            ViewBag.Clientes = _context.Clientes
                .Select(c => new SelectListItem
                {
                    Value = c.Id_cliente.ToString(),
                    Text = c.Razon_Social
                })
                .ToList();

            ViewBag.Empleados = _context.Empleados
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                })
                .ToList();

            // Obtener las cotizaciones Asignadas
            var query = _context.Cotizaciones
               .Include(c => c.Cliente)
               .Include(c => c.Empleado)
               .Where(c => c.Estatus == "pendiente")
               .OrderByDescending(c => c.Fecha)
               .AsQueryable();
            var listaPaginada = await Paginacion<Cotizacion>.CrearAsync(query, pagina, tamañoPagina);
            return View("ListaCotizaciones", listaPaginada); // Retorna la vista con la lista de cotizaciones Asignadas
        }

        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ListaCotCerradas(int pagina = 1)
        {
            int tamañoPagina = 10;
            // Cargar listas para filtros desplegables
            ViewBag.Clientes = _context.Clientes
                .Select(c => new SelectListItem
                {
                    Value = c.Id_cliente.ToString(),
                    Text = c.Razon_Social
                })
                .ToList();

            ViewBag.Empleados = _context.Empleados
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                })
                .ToList();
            // Obtener las cotizaciones Cerradas
            var query = _context.Cotizaciones
               .Include(c => c.Cliente)
               .Include(c => c.Empleado)
               .Where(c => c.Estatus == "aceptada")
               .OrderByDescending(c => c.Fecha)
               .AsQueryable();
            var listaPaginada = await Paginacion<Cotizacion>.CrearAsync(query, pagina, tamañoPagina);
            

            return View("ListaCotizaciones", listaPaginada); // Retorna la vista con la lista de cotizaciones Cerradas
        }

        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult VistaCotizacion(Cotizacion cotizacion)
        {
            cotizacion.Cliente = _context.Clientes.FirstOrDefault(c => c.Id_cliente == cotizacion.IdCliente);
            cotizacion.Empleado = _context.Empleados.FirstOrDefault(e => e.Id == cotizacion.IdEmpleado2);
            cotizacion.Detalles = _context.DetalleCotizacionCliente
                .Where(d => d.IdCotizacion == cotizacion.IdCotizacion)
                .Include(d => d.Producto)
                .ToList();
            cotizacion.Contacto = _context.Contactos_cliente.FirstOrDefault(ct => ct.Id_contacto == cotizacion.IdContacto);

            if (cotizacion == null)
            {
                return NotFound();
            }

            return View("VistaCotizacion", cotizacion);
        }

        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult VistaCotizacionPorId(int id)
        {
            var cotizacion = _context.Cotizaciones
                .Include(c => c.Cliente)
                .Include(c => c.Empleado)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefault(c => c.IdCotizacion == id);
            if (cotizacion == null)
            {
                return NotFound();
            }
            cotizacion.Contacto = _context.Contactos_cliente.FirstOrDefault(ct => ct.Id_contacto == cotizacion.IdContacto);
            return View("VistaCotizacion", cotizacion);
        }

        //Usando QuestPDF para generar PDF de la cotización
        public IActionResult DescargarPDF(int id)
        {
            QuestPDF.Settings.EnableDebugging = true; // Habilita el modo de depuración para QuestPDF
            var cotizacion = _context.Cotizaciones // Carga la cotización con sus detalles
                .Include(c => c.Cliente)
                .Include(c => c.Empleado)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefault(c => c.IdCotizacion == id);

            // Obtener los Contacto_cliente asociados al Cliente de la cotización
            
            if (cotizacion.IdContacto != null)
            {
                cotizacion.Contacto = _context.Contactos_cliente.FirstOrDefault(c => c.Id_contacto == cotizacion.IdContacto);
            }    

            if (cotizacion == null)
                return NotFound();

            var documento = new CotizacionDocumento(cotizacion ,cotizacion.Contacto); // Crea una instancia del documento de cotización

            var stream = new MemoryStream();
            documento.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream, "application/pdf", $"Cotizacion_{cotizacion.IdCotizacion}.pdf");
        }

        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(int? id)
        {
            // Verificar si el id es nulo
            if (id == null)
                return NotFound();
            // Buscar la cotización por id y cargar los detalles
            var cotizacion = _context.Cotizaciones
                .Include(c => c.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefault(c => c.IdCotizacion == id);
            if (cotizacion == null)
                return NotFound();
            //// Cargar los datos necesarios para el formulario
            ViewBag.listaClientes = _context.Clientes.ToList(); // Lista de clientes
            ViewBag.Contactos = _context.Contactos_cliente.ToList(); // Lista de contactos

            // Cargar el contacto asociado si existe
            if (cotizacion.IdContacto != null)
            {
                cotizacion.Contacto = _context.Contactos_cliente.FirstOrDefault(ct => ct.Id_contacto == cotizacion.IdContacto);
            }
            else
            {
                TempData["ContacoCotizacion"] = "Sin datos.";
            }

                // Retornar la vista de edición con el modelo de cotización
                return View(cotizacion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(Cotizacion cotizacion)
        {
            if (ModelState.IsValid)
            {
                var cotizacionExistente = _context.Cotizaciones
                    .Include(c => c.Detalles)
                    .FirstOrDefault(c => c.IdCotizacion == cotizacion.IdCotizacion);

                if (cotizacionExistente == null)
                {
                    return NotFound();
                }

                // Actualizar campos simples
                cotizacionExistente.IdCliente = cotizacion.IdCliente;
                cotizacionExistente.IdEmpleado2 = cotizacion.IdEmpleado2;
                cotizacionExistente.Fecha = cotizacion.Fecha;
                cotizacionExistente.FechaVigencia = cotizacion.FechaVigencia;                
                cotizacionExistente.TipoMoneda = cotizacion.TipoMoneda;
                cotizacionExistente.CondicionPago = cotizacion.CondicionPago;
                cotizacionExistente.Comentario = cotizacion.Comentario;
                cotizacionExistente.Estatus = cotizacion.Estatus;

                // Procesar detalles
                foreach (var detalle in cotizacion.Detalles)
                {
                    if (detalle.Eliminar)
                    {
                        var detalleExistente = cotizacionExistente.Detalles.FirstOrDefault(d => d.IdDetalle == detalle.IdDetalle);
                        if (detalleExistente != null)
                            _context.DetalleCotizacionCliente.Remove(detalleExistente);
                    }
                    else if (detalle.IdDetalle == 0)
                    {
                        // nuevo detalle
                        detalle.IdCotizacion = cotizacion.IdCotizacion;
                        _context.DetalleCotizacionCliente.Add(detalle);
                    }
                    else
                    {
                        // actualizar existente
                        var existente = cotizacionExistente.Detalles.FirstOrDefault(d => d.IdDetalle == detalle.IdDetalle);
                        if (existente != null)
                        {
                            existente.IdProducto = detalle.IdProducto;
                            existente.Cantidad = detalle.Cantidad;
                            existente.PrecioUnitario = detalle.PrecioUnitario;
                        }
                    }
                }

                _context.SaveChanges();
                TempData["MensajeAlertExito"] = "Cotización actualizada correctamente.";
                return RedirectToAction("Consultar"); 
            }
            //// Cargar los datos necesarios para el formulario
            ViewBag.listaClientes = _context.Clientes.ToList(); // Lista de clientes
            ViewBag.Contactos = _context.Contactos_cliente.ToList(); // Lista de contactos
            ViewBag.listaEmpleados = _context.Empleados.Select(e => new
            {
                Id = e.Id,
                NombreCompleto = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
            }).ToList();
            // Cargar el contacto asociado si existe
            if (cotizacion.IdContacto != null)
            {
                cotizacion.Contacto = _context.Contactos_cliente.FirstOrDefault(ct => ct.Id_contacto == cotizacion.IdContacto);
            }
            else
            {
                TempData["ContacoCotizacion"] = "Sin datos.";
            }
            TempData["MensajeAlertFalla"] = "Ocurrió un error. Verifica los datos.";
            return View(cotizacion);
        }


        //Metodo eliminar detalle de la cotizacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarDetalle(int? idDetalle)
        {
            if (idDetalle == null)
            {
                TempData["MensajeAlertFalla"] = "Intenta eliminar un detalle que no existe.";
                return RedirectToAction("ListaCotizaciones");
            }
            var detalle = _context.DetalleCotizacionCliente.Find(idDetalle);
            if (detalle != null)
            {
                _context.DetalleCotizacionCliente.Remove(detalle);
                _context.SaveChanges();
                TempData["MensajeAlertExito"] = "Detalle eliminado correctamente.";
            }
            else
            {
                TempData["MensajeAlertFalla"] = "Detalle no encontrado.";
            }
            return RedirectToAction("Editar", new { id = detalle.IdCotizacion });
        }

        [HttpGet]
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Eliminar(int? id)
        {
            if (id == null)
                return NotFound();

            var cotizacion = await _context.Cotizaciones
                .Include(c => c.Detalles)
                .Include(c => c.Cliente)
                .Include(c => c.Empleado)                
                .FirstOrDefaultAsync(c => c.IdCotizacion == id);

            if (cotizacion == null)
                return NotFound();
            // Cargar el contacto asociado si existe
            if (cotizacion.IdContacto != null)
            {
                cotizacion.Contacto = _context.Contactos_cliente.FirstOrDefault(ct => ct.Id_contacto == cotizacion.IdContacto);
            }
            else
            {
                TempData["ContacoCotizacion"] = "Sin datos.";
            }
            return View(cotizacion);
        }

        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCotizacion(int id)
        {
            var cotizacion = await _context.Cotizaciones // Carga la cotización con sus detalles
                .Include(c => c.Detalles)
                .FirstOrDefaultAsync(c => c.IdCotizacion == id);

            if (cotizacion != null)
            {
                _context.DetalleCotizacionCliente.RemoveRange(cotizacion.Detalles); // Elimina los detalles asociados a la cotización
                _context.Cotizaciones.Remove(cotizacion);
                await _context.SaveChangesAsync();

                TempData["MensajeAlertExito"] = "Cotización eliminada correctamente.";
            }
            else
            {
                TempData["MensajeAlertFalla"] = "Cotización no encontrada.";
            }

            return RedirectToAction("Consultar");
        }
    }
}