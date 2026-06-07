namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Контракт клиента канала диспетчеризации (Control Feedback).
/// Потребитель — только АРМ Администратора, инициирующий команды.
/// </summary>
public interface IControlClient
{
    /// <summary>
    /// Событие изменения жизненного цикла команды.
    /// </summary>
    /// <param name="json">
    /// JSON-объект CommandStatusChanged: { commandId, status, errorMessage }.
    /// Фронтенд фильтрует события по commandId инициированной команды.
    /// </param>
    Task CommandStatusChanged(string json);
}
