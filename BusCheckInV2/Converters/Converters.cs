using BusCheckInV2.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace BusCheckInV2.Converters
{
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int count && count > 0;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Convierte el estado de sincronización en un color.
    // FIX 2026-06-03 (Opción A): ahora recibimos directamente el
    // EstadoCalculado del backend (uno de: Activo, En curso, Pendiente,
    // Finalizado, Cancelado). Mapeo directo a colores:
    //   Activo / En curso / Pendiente → Rojo (requiere atención del chofer)
    //   Finalizado                     → Verde (listo, todo OK)
    //   Cancelado                      → Gris (cerrado sin éxito)
    //
    // Mantenemos fallback a los códigos 1 char (P/I/A/F/C) y a los
    // strings legacy por si alguna parte del pipeline no se actualizó.
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                var s = status.Trim();

                // Camino 1 (preferido): EstadoCalculado del backend nuevo
                if (s.Equals("Activo", StringComparison.OrdinalIgnoreCase))
                    return Colors.Green;
                if (s.Equals("En curso", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("Pendiente", StringComparison.OrdinalIgnoreCase))
                    return Colors.Red;
                if (s.Equals("Finalizado", StringComparison.OrdinalIgnoreCase))
                    return Colors.Green;
                if (s.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                    return Colors.Gray;

                // Camino 2: códigos 1 char (Status='P'/'I'/'A'/'F'/'C')
                if (s == "A")
                    return Colors.Green;
                if (s == "P" || s == "I")
                    return Colors.Red;
                if (s == "F")
                    return Colors.Green;
                if (s == "C")
                    return Colors.Gray;

                // Camino 3: fallback a strings legacy
                if (s.Contains("Activo", StringComparison.OrdinalIgnoreCase))
                    return Colors.Green;
                if (s.Contains("Pendiente", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Iniciado", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Inconcluso", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Aprobado", StringComparison.OrdinalIgnoreCase))
                    return Colors.Red;
                if (s.Contains("Finalizado", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Completado", StringComparison.OrdinalIgnoreCase))
                    return Colors.Green;
                if (s.Contains("Cancelado", StringComparison.OrdinalIgnoreCase))
                    return Colors.Gray;
            }
            return Colors.Gray;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Anonimiza el número de nómina mostrando solo los últimos 4 dígitos
    public class AnonymizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int nomina)
            {
                var nominaStr = nomina.ToString();
                return nominaStr.Length > 4 ? $"***{nominaStr.Substring(nominaStr.Length - 4)}" : nominaStr;
            }
            return value?.ToString() ?? string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Calcula la altura de la CollectionView según el número de elementos
    public class CountToHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && parameter is string param)
            {
                double itemHeight = double.TryParse(param, out var height) ? height : 50;
                return Math.Min(count * itemHeight + 20, 300); // Máximo 300px para evitar overflow
            }
            return 180; // Fallback a altura por defecto
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // No se necesita
        }
    }

    public class ConnectionStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Conectado" : "Offline";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ConnectionColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Colors.Green : Colors.Orange;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToActionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                var s = status.Trim();

                // FIX 2026-06-03 (Opción A): mapeo de los estados finos.
                //   Activo / En curso / Pendiente → ▶️  (ver detalles / seguir trabajando)
                //   Finalizado                     → ✅  (todo OK)
                //   Cancelado                      → ❌  (cerrado sin éxito)
                if (s.Equals("Activo", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("En curso", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("Pendiente", StringComparison.OrdinalIgnoreCase))
                    return "▶️";
                if (s.Equals("Finalizado", StringComparison.OrdinalIgnoreCase))
                    return "✅";
                if (s.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                    return "❌";

                // Fallback a códigos 1 char
                if (s == "P" || s == "I" || s == "A")
                    return "▶️";
                if (s == "F")
                    return "✅";
                if (s == "C")
                    return "❌";

                // Fallback a strings legacy
                return s switch
                {
                    "Pendiente" => "▶️",
                    "Iniciado" => "▶️",
                    "Inconcluso" => "▶️",
                    "Completado" => "✅",
                    "Cancelado" => "❌",
                    _ => "👁️"
                };
            }
            return "👁️";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseIntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Corrección: no usar int? directamente en el patrón is
            if (value is int intValue)
            {
                // Para int: retorna false si es mayor que 0
                return !(intValue > 0);
            }

            // Para int nullable, primero verificar si es null
            if (value == null)
            {
                return true;
            }

            // Si no es null y es del tipo correcto
            if (value.GetType() == typeof(int) || Nullable.GetUnderlyingType(value.GetType()) == typeof(int))
            {
                try
                {
                    int? nullableValue = (int?)value;
                    return !(nullableValue.HasValue && nullableValue.Value > 0);
                }
                catch
                {
                    return true;
                }
            }

            // Para cualquier otro tipo, retorna true
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
    }
    public class DiaSemanaColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // El value debe ser el objeto FletePendienteUI completo
            if (value is FletePendienteUI flete)
            {
                // Determinar si es el día de hoy
                bool esHoy = flete.FechaHora.Date == DateTime.Today;

                // Color según el día de la semana (valores oscuros y saturados)
                Color color = flete.FechaHora.DayOfWeek switch
                {
                    DayOfWeek.Monday => Color.FromArgb("#1E3A8A"),     // Azul oscuro
                    DayOfWeek.Tuesday => Color.FromArgb("#991B1B"),    // Rojo oscuro
                    DayOfWeek.Wednesday => Color.FromArgb("#065F46"),  // Verde oscuro
                    DayOfWeek.Thursday => Color.FromArgb("#7C3AED"),   // Púrpura
                    DayOfWeek.Friday => Color.FromArgb("#B45309"),     // Naranja oscuro
                    DayOfWeek.Saturday => Color.FromArgb("#0E7490"),   // Cian oscuro
                    DayOfWeek.Sunday => Color.FromArgb("#4C0519"),     // Borgoña
                    _ => Color.FromArgb("#374151")                     // Gris por defecto
                };

                // Si es hoy, devolvemos un color llamativo (ámbar/dorado) para resaltar
                if (esHoy)
                {
                    return Color.FromArgb("#007AFF"); // Ámbar brillante
                }

                return color;
            }

            // Si no es el objeto esperado, devolvemos gris
            return Color.FromArgb("#6B7280");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
