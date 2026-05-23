using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Реализация репозитория для работы с датчиками через Dapper.
/// </summary>
public class SensorRepository(DapperContext context) : ISensorRepository
{
    private readonly DapperContext _context = context;

    /// <inheritdoc />
    public async Task<IEnumerable<SensorSettings>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                sensor_id, device_id, port_number, name, slug, 
                data_type, register_address, register_type, register_count, 
                unit, input_min, input_max, output_min, output_max, 
                offset_val, formula, ui_config as UiConfigJson, updated_at
            FROM sensor_settings 
            ORDER BY sensor_id";
        using var connection = _context.CreateConnection();
        return await connection.QueryAsync<SensorSettings>(sql);
    }

    /// <inheritdoc />
    public async Task<SensorSettings?> GetByIdAsync(int sensorId)
    {
        const string sql = @"
            SELECT 
                sensor_id, device_id, port_number, name, slug, 
                data_type, register_address, register_type, register_count, 
                unit, input_min, input_max, output_min, output_max, 
                offset_val, formula, ui_config as UiConfigJson, updated_at
            FROM sensor_settings 
            WHERE sensor_id = @Id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<SensorSettings>(
            sql,
            new { Id = sensorId }
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SensorSettings>> GetByDeviceIdAsync(int deviceId)
    {
        const string sql = @"
            SELECT 
                sensor_id, device_id, port_number, name, slug, 
                data_type, register_address, register_type, register_count, 
                unit, input_min, input_max, output_min, output_max, 
                offset_val, formula, ui_config as UiConfigJson, updated_at
            FROM sensor_settings 
            WHERE device_id = @DeviceId 
            ORDER BY register_address, port_number";
        using var connection = _context.CreateConnection();
        return await connection.QueryAsync<SensorSettings>(sql, new { DeviceId = deviceId });
    }

    /// <inheritdoc />
    public async Task<int> AddAsync(SensorSettings sensor)
    {
        const string sql =
            @"
            INSERT INTO sensor_settings (
                device_id, port_number, name, slug, data_type, 
                register_address, register_type, unit, 
                input_min, input_max, output_min, output_max, 
                offset_val, formula, ui_config, updated_at
            ) VALUES (
                @DeviceId, @PortNumber, @Name, @Slug, @DataTypeStr::sensor_data_type, 
                @RegisterAddress, @RegisterTypeStr::modbus_register_type, @Unit, 
                @InputMin, @InputMax, @OutputMin, @OutputMax, 
                @OffsetVal, @Formula, @UiConfigJson, @UpdatedAt
            ) RETURNING sensor_id";

        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<int>(sql, new
        {
            sensor.DeviceId,
            sensor.PortNumber,
            sensor.Name,
            sensor.Slug,
            DataTypeStr = ToSnakeCase(sensor.DataType.ToString()),
            sensor.RegisterAddress,
            RegisterTypeStr = ToSnakeCase(sensor.RegisterType.ToString()),
            sensor.Unit,
            sensor.InputMin,
            sensor.InputMax,
            sensor.OutputMin,
            sensor.OutputMax,
            sensor.OffsetVal,
            sensor.Formula,
            sensor.UiConfigJson,
            sensor.UpdatedAt
        });
    }

    /// <inheritdoc />
    public async Task UpdateAsync(SensorSettings sensor)
    {
        const string sql =
            @"
            UPDATE sensor_settings SET
                device_id = @DeviceId,
                port_number = @PortNumber,
                name = @Name,
                slug = @Slug,
                data_type = @DataTypeStr::sensor_data_type,
                register_address = @RegisterAddress,
                register_type = @RegisterTypeStr::modbus_register_type,
                unit = @Unit,
                input_min = @InputMin,
                input_max = @InputMax,
                output_min = @OutputMin,
                output_max = @OutputMax,
                offset_val = @OffsetVal,
                formula = @Formula,
                ui_config = @UiConfigJson,
                updated_at = @UpdatedAt
            WHERE sensor_id = @SensorId";

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            sensor.DeviceId,
            sensor.PortNumber,
            sensor.Name,
            sensor.Slug,
            DataTypeStr = ToSnakeCase(sensor.DataType.ToString()),
            sensor.RegisterAddress,
            RegisterTypeStr = ToSnakeCase(sensor.RegisterType.ToString()),
            sensor.Unit,
            sensor.InputMin,
            sensor.InputMax,
            sensor.OutputMin,
            sensor.OutputMax,
            sensor.OffsetVal,
            sensor.Formula,
            sensor.UiConfigJson,
            sensor.UpdatedAt,
            sensor.SensorId
        });
    }

    private static string ToSnakeCase(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "(?<!^)([A-Z])", "_$1").ToUpper();

    /// <inheritdoc />
    public async Task DeleteAsync(int sensorId)
    {
        const string sql = "DELETE FROM sensor_settings WHERE sensor_id = @Id";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = sensorId });
    }
}
