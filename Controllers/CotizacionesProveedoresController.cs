using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using SICOVWEB_MCA.Extensions;
using SICOVWEB_MCA.Helpers;
using SICOVWEB_MCA.PDFDocs;
using SICOVWEB_MCA.Models;
using SICOVWEB_MCA.Models.ViewModels;

namespace SICOVWEB_MCA.Controllers
{
    public class CotizacionesProveedoresController : Controller
    {
        private readonly ApplicationDbContext _context;//Permiso de solo lectura al contexto DB

        public CotizacionesProveedoresController(ApplicationDbContext context)//Constructor usa el contexto como parametro
        {
            _context = context;
        }

        [HttpGet]
        [Authorize]
        public IActionResult CrearCotizacionProveedor()
        {

            ViewBag.Proveedores = new SelectList(_context.Proveedores, "IdProveedor", "Razon_social");
            ViewBag.Productos = new SelectList(_context.Productos, "Id_producto", "Nombre");
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult CrearCotizacionProveedor(Cotizacion_proveedor cotizacion)
        {
            //Asignar el EmpleadoId del usuario autenticado           
            cotizacion.Id_Empleado3 = User.GetEmpleadoId().Value;

            // Sumar los subtotales de los detalles para obtener el precio total
            if (cotizacion.Detalles != null && cotizacion.Detalles.Count > 0)
            {
                cotizacion.Precio_total = (decimal)cotizacion.Detalles.Sum(d => d.Subtotal);
            }
            else
            {
                ModelState.AddModelError(string.Empty, "La cotización debe tener al menos un detalle.");
            }

            if (!ModelState.IsValid)
            {

                ViewBag.Proveedores = new SelectList(_context.Proveedores, "IdProveedor", "Razon_social");
                ViewBag.Productos = new SelectList(_context.Productos, "Id_producto", "Nombre");
                TempData["MensajeAlertFalla"] = "Error al crear la cotización. Por favor, revise los datos ingresados.";
                return View(cotizacion);
            }
            try
            {
                _context.Cotizaciones_proveedores.Add(cotizacion);
                _context.SaveChanges();
                TempData["MensajeAlertExito"] = "Cotización creada exitosamente.";
                return RedirectToAction("CrearCotizacionProveedor");
            }
            catch (Exception ex)
            {
                TempData["MensajeAlertFalla"] = "Error al crear la cotización: " + ex.Message;
                Console.WriteLine("==> EXCEPCION INTERNA : " + ex.InnerException);
                return View(cotizacion);
                throw;
            }
        }

        // Metodo para obtener los productos en formato JSON
        public JsonResult GetProductos()
        {
            var productos = _context.Productos.Select(p => new
            {
                IdProducto = p.Id_producto,
                NombreProducto = p.Nombre,
                Descripcion = p.Descripcion,
                Unidad = p.Unidad,
                PrecioCompra = p.Precio_Compra
            }).ToList();

            return Json(productos);
        }

        // Metodo GET para ver la vista de consulta de cotizaciones
        [HttpGet]
        public async Task<IActionResult> Buscar(int? idCotizacion, int? proveedorId, int? empleadoId, 
            DateTime? fechaInicio, DateTime? fechaFin, string estatus, int pagina = 1)
        {
            int tamanioPagina = 10; // Número de registros por página

            // Cargar listas para los filtros
            ViewBag.Proveedores = _context.Proveedores
                .Select(p => new SelectListItem
                {
                    Value = p.IdProveedor.ToString(),
                    Text = p.Razon_social
                })
                .ToList();

            ViewBag.Empleados = _context.Empleados
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Nombre + " " + e.Apellido_Paterno + " " + e.Apellido_Materno
                })
                .ToList();
            // Construir la consulta con los filtros aplicados
            var query = _context.Cotizaciones_proveedores
                .Include(c => c.Proveedor)
                .Include(c => c.Empleado)
                .AsQueryable();

            if (idCotizacion.HasValue)
                query = query.Where(c => c.Id_cotizacion == idCotizacion);

            if (proveedorId.HasValue)
                query = query.Where(c => c.Id_Proveedor == proveedorId);

            if (empleadoId.HasValue)
                query = query.Where(c => c.Id_Empleado3 == empleadoId);

            if (fechaInicio.HasValue)
                query = query.Where(c => c.Fecha >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(c => c.Fecha <= fechaFin.Value);

            if (!string.IsNullOrEmpty(estatus))
                query = query.Where(c => c.Estatus == estatus);

            query = query.OrderByDescending(c => c.Fecha);

            // Aplicar paginación
            var listaPaginada = await Paginacion<Cotizacion_proveedor>.CrearAsync(query, pagina, tamanioPagina);

            // Mantener los filtros seleccionados al cambiar de página
            ViewBag.Filtros = new
            {
                idCotizacion,
                proveedorId,
                empleadoId,
                fechaInicio = fechaInicio?.ToString("yyyy-MM-dd"),
                fechaFin = fechaFin?.ToString("yyyy-MM-dd"),
                estatus
            };


            return View("ConsultaCotizacionesProveedores", listaPaginada);
        }

        // Metodo GET para ver los detalles de una cotizacion
        [HttpGet]
        public IActionResult VistaCotizacionProveedor(int id)
        {
            var cotizacion = _context.Cotizaciones_proveedores
                .Include(c => c.Proveedor)
                .Include(c => c.Empleado)
                .Include(c => c.Detalles)
                .FirstOrDefault(c => c.Id_cotizacion == id);
            if (cotizacion == null)
            {
                return NotFound();
            }
            return View(cotizacion);
        }

        // Metodos para el comparador de cotizaciones de proveedores

        [HttpGet]
        public IActionResult CompararCotizaciones()
        {
            ViewBag.CotizacionesList = new SelectList(_context.Cotizaciones_proveedores
                .Include(c => c.Proveedor)
                .Select(c => new
                {
                    Id = c.Id_cotizacion,
                    Nombre = "Cotización #" + c.Id_cotizacion + " - " + c.Proveedor.Razon_social
                }),
                "Id", "Nombre");

            return View(new CompararCotizacionesVM());
        }

        [HttpPost]
        public IActionResult CompararCotizaciones(CompararCotizacionesVM model)
        {
            ViewBag.CotizacionesList = new SelectList(_context.Cotizaciones_proveedores
                .Include(c => c.Proveedor)
                .Select(c => new
                {
                    Id = c.Id_cotizacion,
                    Nombre = "Cotización #" + c.Id_cotizacion + " - " + c.Proveedor.Razon_social
                }),
                "Id", "Nombre");

            model.Cotizacion1 = _context.Cotizaciones_proveedores
                .Include(c => c.Proveedor)
                .Include(c => c.Empleado)
                .Include(c => c.Detalles)
                .FirstOrDefault(c => c.Id_cotizacion == model.IdCotizacion1);

            model.Cotizacion2 = _context.Cotizaciones_proveedores
                .Include(c => c.Proveedor)
                .Include(c => c.Empleado)
                .Include(c => c.Detalles)
                .FirstOrDefault(c => c.Id_cotizacion == model.IdCotizacion2);

            return View(model);
        }

        // Metodo para Generar un PDF de la orden de compra para una cotizacion elegida en el comparador
        [HttpPost]
        public IActionResult GenerarOrdenCompra(int IdCotizacionSeleccionada)
        {
            // Se obtiene la cotizacion de la DB con sus detalles
            var cotizacion = _context.Cotizaciones_proveedores
                .Include(c => c.Proveedor)
                .Include(c => c.Empleado)
                .Include(c => c.Detalles)
                .FirstOrDefault(c => c.Id_cotizacion == IdCotizacionSeleccionada);

            if (cotizacion == null)
            {
                TempData["MensajeAlertFalla"] = "Error: No se encontró la cotización seleccionada. Id buscado: " + IdCotizacionSeleccionada;
                return NotFound();
            }
            // Validar si la cotizacion ya tiene una compra asociada
            var compraExistente = _context.Compras.FirstOrDefault(c => c.Id_Cotizacion_prov == cotizacion.Id_cotizacion);
            if (compraExistente == null)
            {
                Console.WriteLine("No existe una compra para esa cotización, se procede a crearla.");
                TempData["MensajeAlertExito"] = "No existe una compra para esa cotización, se procede a crearla.";
                // Se agrega la compra a la base de datos
                CrearCompraDesdeCotizacion(cotizacion);
            }
            TempData["MensajeAlertExito"] = "Ya existe una compra para esa cotización, se procede a generar el documento PDF.";
            Console.WriteLine("Ya existe una compra para esa cotización");
            // Obtener la compra asociada a la cotización
            var compra = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Empleado)
                .Include(c => c.Detalles)
                .FirstOrDefault(c => c.Id_Cotizacion_prov == cotizacion.Id_cotizacion);
            if (compra == null)
            {
                TempData["MensajeAlertFalla"] = "Error: No se pudo crear o encontrar la compra asociada a la cotización.";
                return RedirectToAction("CompararCotizaciones");
            }
            Console.WriteLine("Informacion de la compra recuperada con exito se genera el documento PDF");
            // Se crea la orden de compra usando el generador de PDF QUESTPDF 
            var ordenCompra = new OrdenCompraPDF(compra);
            var stream = new MemoryStream();
            ordenCompra.GeneratePdf(stream);
            stream.Position = 0;
            TempData["MensajeAlertExito"] = "Orden de compra generado con exito.";
            return File(stream, "application/pdf", $"Orden_Compra_{cotizacion.Id_cotizacion}.pdf");
        }

        //Metod para crear una compra con sus detalles a partir de una cotizacion
        [HttpPost]
        public IActionResult CrearCompraDesdeCotizacion(Cotizacion_proveedor cotizacion)
        {
            try
            {
                // Crear la compra
                var compra = new Compra
                {
                    IdProveedor = cotizacion.Id_Proveedor,
                    Id_Cotizacion_prov = cotizacion.Id_cotizacion,
                    IdEmpleado = User.GetEmpleadoId().Value,
                    Fecha_compra = DateTime.Now,
                    Costo_Total = cotizacion.Precio_total,
                    Tipo_Moneda = cotizacion.Tipo_Moneda,
                    Condicion_Pago = cotizacion.Condiciones_Pago ?? "Contado"
                };
                _context.Compras.Add(compra);
                _context.SaveChanges();
                // Crear los detalles de la compra basados en los detalles de la cotización
                if (cotizacion.Detalles != null && cotizacion.Detalles.Count > 0)
                {
                    foreach (var detalleCotizacion in cotizacion.Detalles)
                    {
                        var detalleCompra = new DetalleCompra
                        {
                            Id_Compra = compra.Id_compra,
                            Nombre_producto = detalleCotizacion.Nombre_producto,
                            Descripcion = detalleCotizacion.Descripcion,
                            Cantidad = detalleCotizacion.Cantidad,
                            Precio_Unitario = detalleCotizacion.Precio_Unitario,
                            Subtotal = detalleCotizacion.Subtotal,
                            Tiempo_Entrega = detalleCotizacion.Tiempo_Entrega
                        };
                        _context.Detalles_compra.Add(detalleCompra);
                    }
                    _context.SaveChanges();
                }
                TempData["MensajeAlertExito"] = "Compra creada exitosamente desde la cotización.";
                Console.WriteLine("Compra creada exitosamente desde la cotización.");
                return RedirectToAction("Buscar");  // Se debe redirigir a la vista de compras o consultar compras
            }
            catch (Exception ex)
            {
                TempData["MensajeAlertFalla"] = "Error al crear la compra desde la cotización: " + ex.Message;
                Console.WriteLine("Error al crear la compra desde la cotización: " + ex.Message + ex.InnerException);
                return RedirectToAction("Buscar");
            }
        }
    }
}