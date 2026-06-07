namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Универсальный контейнер страницы результатов с метаданными пагинации.
/// Отделяет полезную нагрузку (Items) от служебной информации, чтобы клиент
/// мог построить пагинатор, не делая отдельный запрос за общим числом строк.
/// </summary>
/// <typeparam name="T">Тип элемента страницы.</typeparam>
/// <param name="Items">Элементы текущей страницы.</param>
/// <param name="Total">Общее число строк, удовлетворяющих фильтру (без учёта пагинации).</param>
/// <param name="Page">Номер текущей страницы (1-based).</param>
/// <param name="PageSize">Размер страницы.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize);
