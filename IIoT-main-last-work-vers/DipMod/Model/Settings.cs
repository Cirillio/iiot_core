using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

/// <summary>
/// Класс настроек порта.
/// Отвечает за хранение параметров калибровки и типа датчика для каждого порта.
/// Реализует INotifyPropertyChanged для обновления UI при изменении настроек.
/// </summary>
public class PortSettings : INotifyPropertyChanged
{
    public int PortId { get; set; }                    // ID порта (1-8)
    public string SelectedParameter { get; set; }       // Выбранный тип измерения (например, "Температура")
    public List<string> AvailableParameters { get; set; } // Список доступных типов измерений

    // Диапазон входного напряжения (обычно -10В до +10В)
    private double _inputMin = -10;
    private double _inputMax = 10;
    
    // Диапазон выходных физических величин (например, 0°C - 100°C)
    private double _outputMin = 0;
    private double _outputMax = 100;
    
    // Смещение для коррекции (калибровка нуля)
    private double _offset = 0;

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Минимальное входное напряжение.
    /// </summary>
    public double inputMin
    {
        get => _inputMin;
        set
        {
            if (_inputMin != value)
            {
                _inputMin = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Максимальное входное напряжение.
    /// </summary>
    public double inputMax
    {
        get => _inputMax;
        set
        {
            if (_inputMax != value)
            {
                _inputMax = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Минимальное значение физической величины (соответствует inputMin).
    /// </summary>
    public double outputMin
    {
        get => _outputMin;
        set
        {
            if (_outputMin != value)
            {
                _outputMin = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Максимальное значение физической величины (соответствует inputMax).
    /// </summary>
    public double outputMax
    {
        get => _outputMax;
        set
        {
            if (_outputMax != value)
            {
                _outputMax = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Смещение значения (добавляется к результату).
    /// </summary>
    public double offset
    {
        get => _offset;
        set
        {
            if (_offset != value)
            {
                _offset = value;
                OnPropertyChanged();
            }
        }
    }

    public PortSettings(int portId, List<string> availableParameters)
    {
        PortId = portId;
        AvailableParameters = availableParameters;
        SelectedParameter = availableParameters.FirstOrDefault();
    }

    /// <summary>
    /// Преобразование входного напряжения в физическую величину согласно настройкам.
    /// Формула линейной интерполяции:
    /// Result = (value - inputMin) / (inputMax - inputMin) * (outputMax - outputMin) + outputMin + offset
    /// </summary>
    /// <param name="value">Входное значение напряжения</param>
    /// <returns>Преобразованное значение</returns>
    public double ConverttoParam(double value)
    {
        return (value - inputMin) / (inputMax - inputMin) * (outputMax - outputMin) + outputMin + offset;
    }
}