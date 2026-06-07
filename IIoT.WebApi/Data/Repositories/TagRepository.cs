using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Реализация репозитория для работы с датчиками через Dapper.
/// </summary>
public class TagRepository(DapperContext context) : ITagRepository
{
    private readonly DapperContext _context = context;

    /// <inheritdoc />
    public async Task<IEnumerable<TagSettings>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                tag_id, device_id, port_number, name, slug, 
                data_type, register_address, register_type, register_count, raw_data_type, endianness,
                unit, input_min, input_max, output_min, output_max,
                offset_val, deadband_threshold, ui_config as UiConfigJson, updated_at
            FROM tags
            ORDER BY tag_id";
        using var connection = _context.CreateConnection();
        return await connection.QueryAsync<TagSettings>(sql);
    }

    /// <inheritdoc />
    public async Task<TagSettings?> GetByIdAsync(int tagId)
    {
        const string sql = @"
            SELECT 
                tag_id, device_id, port_number, name, slug, 
                data_type, register_address, register_type, register_count, raw_data_type, endianness,
                unit, input_min, input_max, output_min, output_max,
                offset_val, deadband_threshold, ui_config as UiConfigJson, updated_at
            FROM tags
            WHERE tag_id = @Id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TagSettings>(
            sql,
            new { Id = tagId }
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TagSettings>> GetByDeviceIdAsync(int deviceId)
    {
        const string sql = @"
            SELECT 
                tag_id, device_id, port_number, name, slug, 
                data_type, register_address, register_type, register_count, raw_data_type, endianness,
                unit, input_min, input_max, output_min, output_max,
                offset_val, deadband_threshold, ui_config as UiConfigJson, updated_at
            FROM tags
            WHERE device_id = @DeviceId
            ORDER BY register_address, port_number";
        using var connection = _context.CreateConnection();
        return await connection.QueryAsync<TagSettings>(sql, new { DeviceId = deviceId });
    }

    /// <inheritdoc />
    public async Task<int> AddAsync(TagSettings tag)
    {
        const string sql =
            @"
            INSERT INTO tags (
                device_id, port_number, name, slug, data_type,
                register_address, register_type, register_count, raw_data_type, endianness, unit,
                input_min, input_max, output_min, output_max,
                offset_val, deadband_threshold, ui_config, updated_at
            ) VALUES (
                @DeviceId, @PortNumber, @Name, @Slug, @DataTypeStr::tag_data_type,
                @RegisterAddress, @RegisterTypeStr::modbus_register_type, @RegisterCount, @RawDataTypeStr::modbus_raw_data_type, @EndiannessStr::modbus_endianness, @Unit,
                @InputMin, @InputMax, @OutputMin, @OutputMax,
                @OffsetVal, @DeadbandThreshold, @UiConfigJson, @UpdatedAt
            ) RETURNING tag_id";

        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<int>(sql, new
        {
            tag.DeviceId,
            tag.PortNumber,
            tag.Name,
            tag.Slug,
            DataTypeStr = ToSnakeCase(tag.DataType.ToString()),
            tag.RegisterAddress,
            RegisterTypeStr = ToSnakeCase(tag.RegisterType.ToString()),
            tag.RegisterCount,
            RawDataTypeStr = tag.RawDataType.ToString().ToUpperInvariant(),
            EndiannessStr = ToSnakeCase(tag.Endianness.ToString()),
            tag.Unit,
            tag.InputMin,
            tag.InputMax,
            tag.OutputMin,
            tag.OutputMax,
            tag.OffsetVal,
            tag.DeadbandThreshold,
            tag.UiConfigJson,
            tag.UpdatedAt
        });
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TagSettings tag)
    {
        const string sql =
            @"
            UPDATE tags SET
                device_id = @DeviceId,
                port_number = @PortNumber,
                name = @Name,
                slug = @Slug,
                data_type = @DataTypeStr::tag_data_type,
                register_address = @RegisterAddress,
                register_type = @RegisterTypeStr::modbus_register_type,
                register_count = @RegisterCount,
                raw_data_type = @RawDataTypeStr::modbus_raw_data_type,
                endianness = @EndiannessStr::modbus_endianness,
                unit = @Unit,
                input_min = @InputMin,
                input_max = @InputMax,
                output_min = @OutputMin,
                output_max = @OutputMax,
                offset_val = @OffsetVal,
                deadband_threshold = @DeadbandThreshold,
                ui_config = @UiConfigJson,
                updated_at = @UpdatedAt
            WHERE tag_id = @TagId";

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            tag.DeviceId,
            tag.PortNumber,
            tag.Name,
            tag.Slug,
            DataTypeStr = ToSnakeCase(tag.DataType.ToString()),
            tag.RegisterAddress,
            RegisterTypeStr = ToSnakeCase(tag.RegisterType.ToString()),
            tag.RegisterCount,
            RawDataTypeStr = tag.RawDataType.ToString().ToUpperInvariant(),
            EndiannessStr = ToSnakeCase(tag.Endianness.ToString()),
            tag.Unit,
            tag.InputMin,
            tag.InputMax,
            tag.OutputMin,
            tag.OutputMax,
            tag.OffsetVal,
            tag.DeadbandThreshold,
            tag.UiConfigJson,
            tag.UpdatedAt,
            tag.TagId
        });
    }

    private static string ToSnakeCase(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "(?<!^)([A-Z])", "_$1").ToUpper();

    /// <inheritdoc />
    public async Task DeleteAsync(int tagId)
    {
        const string sql = "DELETE FROM tags WHERE tag_id = @Id";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = tagId });
    }
}
