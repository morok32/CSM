namespace ComputerServiceManager.Models
{
    /// <summary>
    /// Статусы заказа. Соответствуют idСтатус в БД.
    /// </summary>
    public enum OrderStatus
    {
        New = 1,              // Новый
        Diagnostics = 2,      // В диагностике
        WaitingParts = 3,     // Ожидание запчастей
        InWork = 4,           // В работе
        Ready = 5,            // Готов к выдаче
        Delivered = 6,        // Выдан клиенту
        Cancelled = 7         // Отменен
    }

    /// <summary>
    /// Статусы материалов в заказе. Соответствуют idСтатус в ПозицииЗаказа.
    /// </summary>
    public enum MaterialStatus
    {
        Proposed = 1,         // Предложен (обычно не используется явно, но для полноты)
        Approved = 2,         // Согласован
        Reserved = 8,         // Зарезервирован на складе
        WrittenOff = 9        // Списан (установлен/потерян)
    }

    /// <summary>
    /// Расширения для проверки логики статусов
    /// </summary>
    public static class OrderStatusExtensions
    {
        /// <summary>
        /// Можно ли редактировать заказ? (Только если не выдан и не отменен)
        /// </summary>
        public static bool CanEdit(this OrderStatus status)
        {
            return status != OrderStatus.Delivered && status != OrderStatus.Cancelled;
        }

        /// <summary>
        /// Можно ли списывать материалы? (Только в работе или ожидании запчастей)
        /// </summary>
        public static bool CanWriteOffMaterials(this OrderStatus status)
        {
            return status == OrderStatus.InWork || status == OrderStatus.WaitingParts;
        }

        /// <summary>
        /// Можно ли менять статус на "Готов к выдаче"?
        /// </summary>
        public static bool CanBeReady(this OrderStatus currentStatus)
        {
            return currentStatus == OrderStatus.InWork || currentStatus == OrderStatus.WaitingParts;
        }
    }
}
