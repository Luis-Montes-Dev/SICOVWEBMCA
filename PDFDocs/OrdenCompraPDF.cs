
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
    public class OrdenCompraPDF : IDocument
    {
        private readonly Compra _compra;        
        public OrdenCompraPDF(Compra compra) // Constructor
        {
            _compra = compra;
        }
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default; // Metadatos del documento

        public void Compose(IDocumentContainer container) // Composición del documento
        {
            var iva = _compra.Costo_Total * 0.16m; // Cálculo del IVA (16%)
            var totalConIVA = _compra.Costo_Total + iva; // Total con IVA

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
                    // Información de MCA
                    header.Item().AlignCenter().Text("MC AUTOMATIZACIÓN S.A. DE C.V.")
                                 .FontSize(14).Bold();
                    // Informacion de laOrden de compra
                    header.Item().AlignCenter().Text("ORDEN DE COMPRA")
                                 .FontSize(14).Bold();


                    // Información de la cotización
                    header.Item().AlignCenter().Text($"No. Cotización: {_compra.Id_Cotizacion_prov}");

                    header.Item().Text($"Fecha de cotización: {_compra.Fecha_compra:dd/MM/yyyy}");
                    // Informacion del proveedor
                    header.Item().Text($"Proveedor: {_compra.Proveedor.Razon_social}");
                    header.Item().Text($"Correo: {_compra.Proveedor.Correo}");
                    header.Item().Text($"Teléfono: {_compra.Proveedor.Telefono}");
                    header.Item().Text($"Dirección: {_compra.Proveedor.Calle} {_compra.Proveedor.Numero}, " +
                        $"{_compra.Proveedor.Localidad},{_compra.Proveedor.CP}, {_compra.Proveedor.Estado} ");


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
                        col.Item().Text($"Subtotal: ${_compra.Costo_Total:N2}");
                        col.Item().Text($"IVA 16%: ${iva:N2}");
                        col.Item().Text($"Total {_compra.Tipo_Moneda}: ${totalConIVA:N2}").Bold();
                    });

                    // Datos finales
                    content.Item().PaddingTop(10).Column(col =>
                    {
                        col.Spacing(2);                        
                        col.Item().Text($"Pago: {_compra.Condicion_Pago}");
                        col.Item().Text($"Elaboró: {_compra.Empleado.Nombre} {_compra.Empleado.Apellido_Paterno} {_compra.Empleado.Apellido_Materno}");
                        col.Item().Text($"Tel: {_compra.Empleado.Telefono}");
                        col.Item().Text($"Correo: {_compra.Empleado.Correo}");
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

                    columns.ConstantColumn(40);  // # Item
                    columns.RelativeColumn(2);  // PRODUCTO
                    columns.RelativeColumn(2);   // DESCRIPCIÓN
                    columns.ConstantColumn(40);  // CANT  
                    columns.ConstantColumn(60);  // P. UNITARIO
                    columns.ConstantColumn(60);  // SUBTOTAL
                    columns.ConstantColumn(50);  // TIEMPO ENTREGA
                });

                // Encabezado
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("#").Bold();
                    header.Cell().Element(CellStyle).Text("PRODUCTO").Bold();
                    header.Cell().Element(CellStyle).Text("DESCRIPCIÓN").Bold();
                    header.Cell().Element(CellStyle).Text("CANT").Bold();
                    header.Cell().Element(CellStyle).Text("P. UNITARIO").Bold();
                    header.Cell().Element(CellStyle).Text("SUBTOTAL").Bold();
                    header.Cell().Element(CellStyle).Text("TIEMPO ENTREGA").Bold();

                    static IContainer CellStyle(IContainer container) =>
                        container.Padding(1).Background(Grey.Lighten3).Border(1).AlignCenter();
                });

                // Filas dinámicas
                int index = 1;
                foreach (var item in _compra.Detalles)
                {
                    //Datos editados del detalleCotizacionProveedor

                    table.Cell().Element(Cell).Text(index++.ToString());
                    table.Cell().Element(Cell).Text(item.Nombre_producto ?? "");
                    table.Cell().Element(Cell).Text(item.Descripcion ?? "");
                    table.Cell().Element(Cell).Text(item.Cantidad.ToString());
                    table.Cell().Element(Cell).Text($"${item.Precio_Unitario:N2}");
                    table.Cell().Element(Cell).Text($"${item.Subtotal:N2}");
                    table.Cell().Element(Cell).Text(item.Tiempo_Entrega.ToString());
                }

                static IContainer Cell(IContainer container) =>
                    container.BorderBottom(1).Padding(5).AlignCenter();
            });
        }

    }

}
