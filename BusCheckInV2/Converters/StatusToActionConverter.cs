using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace BusCheckInV2.Converters
{
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