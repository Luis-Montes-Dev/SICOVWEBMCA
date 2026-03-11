
using AspNetCoreGeneratedDocument;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SICOVWEB_MCA.Models;
using System.Globalization;
using static QuestPDF.Helpers.Colors;

namespace SICOVWEB_MCA.PDFDocs
{
    

    public class CotizacionDocumento : IDocument
    {
        private readonly Cotizacion _cotizacion;
        private readonly Contacto_cliente? _contacto;
        private readonly ApplicationDbContext _dbContext;

        public CotizacionDocumento(Cotizacion cotizacion , Contacto_cliente? contacto) // Constructor
        {
            _cotizacion = cotizacion;
            _contacto = contacto;            
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default; // Metadatos del documento

        public void Compose(IDocumentContainer container) // Composición del documento
        {
            var total = Math.Round(_cotizacion.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario), 2);
            var iva = Math.Round(total * 0.16M, 2);
            var totalConIVA = total + iva;

            // Diseño de la página
            container.Page(page =>
            {
                page.Size(PageSizes.A4); // Tamaño de la página A4
                page.Margin(20); // Margen de 20 unidades
                page.DefaultTextStyle(x => x.FontSize(10)); // Estilo de texto por defecto
                page.PageColor(White); // Color de fondo blanco

                // Encabezado
                page.Header().Column(header =>
                {
                    header.Spacing(5); // Espaciado entre elementos del encabezado

                    // Logo
                    
                    header.Item().AlignCenter().Width(550).MaxHeight(80).Image(File.ReadAllBytes("wwwroot/images/Logos3.jpg"));

                    // Información de la cotización
                    header.Item().Text($"COTIZACIÓN: {_cotizacion.IdCotizacion}")
                                 .FontSize(16).Bold().AlignCenter();

                    header.Item().Text($"Fecha: {_cotizacion.Fecha:dd/MM/yyyy}");
                    header.Item().Text($"Empresa: {_cotizacion.Cliente.Razon_Social}");
                    header.Item().Text($"Correo: {_cotizacion.Cliente.Correo}");
                    header.Item().Text($"Teléfono: {_cotizacion.Cliente.Telefono}");

                    // Mostrar nombre del contacto si está disponible
                    if (_contacto != null)
                        header.Item().Text($"Persona contacto: {_contacto.Nombre} {_contacto.Apellido_paterno} {_contacto.Apellido_materno}           Tel: {_contacto.Telefono}      Correo: {_contacto.Correo}");
                    else
                        header.Item().Text($"Persona contacto: Sin datos.");
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    content.Spacing(10);

                    // Tabla
                    content.Item().Element(BuildTable);

                    // Totales
                    content.Item().AlignRight().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text($"Subtotal: ${total:N2}");
                        col.Item().Text($"IVA 16%: ${iva:N2}");
                        col.Item().Text($"Total {_cotizacion.TipoMoneda}: ${totalConIVA:N2}").Bold();
                    });

                    // Datos finales
                    content.Item().PaddingTop(10).Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text($"Fecha de vigencia: {_cotizacion.FechaVigencia:dd/MM/yyyy}");
                        col.Item().Text($"Pago: {_cotizacion.Cliente.Condicion_Pago}");
                        
                        col.Item().Text($"Elaboró: {_cotizacion.Empleado.Nombre} {_cotizacion.Empleado.Apellido_Paterno} {_cotizacion.Empleado.Apellido_Materno}");
                        col.Item().Text($"Tel: {_cotizacion.Empleado.Telefono}");
                        col.Item().Text($"Correo: {_cotizacion.Empleado.Correo}");
                    });
                });
            });
        }

        private void BuildTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    
                    columns.ConstantColumn(30);  // PART
                    columns.ConstantColumn(60);  // PRODUCTO
                    columns.ConstantColumn(40);  // MARCA
                    columns.RelativeColumn(2);   // DESCRIPCIÓN
                    columns.ConstantColumn(50);  // TIEMPO ENTREGA
                    columns.ConstantColumn(30);  // UNIDAD
                    columns.ConstantColumn(40);  // CANT                    
                    columns.ConstantColumn(60);  // P. UNITARIO
                    columns.ConstantColumn(60);  // SUBTOTAL
                });

                // Encabezado
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("PART").Bold();
                    header.Cell().Element(CellStyle).Text("PRODUCTO").Bold();
                    header.Cell().Element(CellStyle).Text("MARCA").Bold();
                    header.Cell().Element(CellStyle).Text("DESCRIPCIÓN").Bold();
                    header.Cell().Element(CellStyle).Text("TIEMPO ENTREGA").Bold();
                    header.Cell().Element(CellStyle).Text("UDS").Bold();
                    header.Cell().Element(CellStyle).Text("CANT").Bold();                    
                    header.Cell().Element(CellStyle).Text("P. UNITARIO").Bold();
                    header.Cell().Element(CellStyle).Text("SUBTOTAL").Bold();

                    static IContainer CellStyle(IContainer container) =>
                        container.Padding(1).Background(Grey.Lighten3).Border(1).AlignCenter();
                });

                // Filas dinámicas
                int index = 1;
                foreach (var item in _cotizacion.Detalles)
                {
                    if (item.Unidad != null && item.Unidad.Equals("Svc") )
                    {
                        //Datos editados del detalleCotizacionCliente
                        
                        table.Cell().Element(Cell).Text(index++.ToString());
                        table.Cell().Element(Cell).Text(item.Producto.Nombre ?? "");
                        table.Cell().Element(Cell).Text("N/A");
                        table.Cell().Element(Cell).Text(item.Descripcion ?? "");
                        table.Cell().Element(Cell).Text(item.TiempoEntrega ?? "");
                        table.Cell().Element(Cell).Text(item.Unidad ?? "");
                        table.Cell().Element(Cell).Text(item.Cantidad.ToString());
                        table.Cell().Element(Cell).Text($"${item.PrecioUnitario:N2}");
                        table.Cell().Element(Cell).Text($"${item.PrecioTotal:N2}");
                    }
                    //Datos tomados del producto
                    else
                    {
                        table.Cell().Element(Cell).Text(index++.ToString());
                        table.Cell().Element(Cell).Text(item.Producto?.Nombre ?? "");
                        table.Cell().Element(Cell).Text(item.Producto?.Marca ?? "");
                        table.Cell().Element(Cell).Text(item.Producto?.Descripcion ?? "");
                        table.Cell().Element(Cell).Text(item.TiempoEntrega ?? "");
                        table.Cell().Element(Cell).Text(item.Producto?.Unidad ?? "");
                        table.Cell().Element(Cell).Text(item.Cantidad.ToString());
                        table.Cell().Element(Cell).Text($"${item.Producto?.Precio_Venta:N2}");
                        table.Cell().Element(Cell).Text($"${item.PrecioTotal:N2}");
                    }
                    
                }

                static IContainer Cell(IContainer container) =>
                    container.BorderBottom(1).Padding(5).AlignCenter();
            });
        }

    }

}
