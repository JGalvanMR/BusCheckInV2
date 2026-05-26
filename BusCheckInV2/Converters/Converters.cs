using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace BusCheckInV2.Converters
{
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int count && count > 0;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Convierte el estado de sincronización en un color (rojo para "Pendiente", verde para "Sincronizado")
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.Contains("Pendiente", StringComparison.OrdinalIgnoreCase) ? Colors.Red : Colors.Green;
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
                return status switch
                {
                    "Pendiente" => "▶️",
                    "Iniciado" => "⏸️",
                    "Inconcluso" => "🔁",
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
}
