/* Clase Middleware para validacion de datos y evitar SQL Injection por medio de los datos ingresados en formularios*/

using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text.RegularExpressions;

namespace SICOVWEB_MCA.Helpers
{
    public class ValidadorDatos
   
    {
        private readonly RequestDelegate _next; // Delegado para el siguiente middleware

        public ValidadorDatos(RequestDelegate next) // Constructor que recibe el siguiente middleware
        {
            _next = next;
        }
       

public async Task InvokeAsync(HttpContext context) // Invoca cada solicitud HTTP y realiza la validación de los datos del formulario
        {
            // Solo validar si es un POST con datos de formulario
            if (context.Request.Method == HttpMethods.Post && context.Request.HasFormContentType)
            {
                foreach (var campo in context.Request.Form)
                {
                    if (campo.Key == "g-recaptcha-response") 
                        continue; // omitir la validación de este campo

                    string? valor = campo.Value;

                    // Asegurarse que el valor no es nulo antes de pasarlo a ContieneInyeccionSQL
                    if (!string.IsNullOrEmpty(valor) && ContieneInyeccionSQL(valor))
                    {
                        Console.WriteLine("Valor no valido: " + valor);
                        context.Response.Redirect("/Login_Controlador/Login?entradaInvalida=true");
                        return; // corta la ejecución y no llega al controlador
                    }
                }
            }
            await _next(context); // pasa al siguiente middleware o controlador
        }
        private bool ContieneInyeccionSQL(string input)
        {
            string patron = @"('|--|;|/\*|\*/|xp_|exec|drop|insert|select|delete|update|union)"; //Caracteres y patrones No permitidos.
            return Regex.IsMatch(input, patron, RegexOptions.IgnoreCase);
        }
    }

}
