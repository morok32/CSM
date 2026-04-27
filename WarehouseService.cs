using ComputerServiceManager.Models;
using ComputerServiceManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComputerServiceManager.Services
{
    /// <summary>
    /// Сервис для управления складскими операциями
    /// Реализует новую логику учета материалов:
    /// - Склад.Количество = физическое наличие на полке (уменьшается при нажатии кнопки "Списать")
    /// - Доступно = КоличествоНаПолке - СУММА(Все резервы где СтатусПозиции=1)
    /// - При добавлении материала в заказ: СтатусПозиции=true (резервирование), физ. количество НЕ меняется
    /// - При нажатии кнопки "Списать": СтатусПозиции остается=true, физ. количество УМЕНЬШАЕТСЯ
    /// </summary>
    public class WarehouseService : IDisposable
    {
        private readonly ComputerServiceManagerEntities _context;

        public WarehouseService()
        {
            _context = new ComputerServiceManagerEntities();
        }

        public WarehouseService(ComputerServiceManagerEntities context)
        {
            _context = context;
        }

        /// <summary>
        /// Получить доступное количество материала (физическое минус все активные резервы)
        /// </summary>
        /// <param name="materialId">ID материала</param>
        /// <returns>Доступное количество</returns>
        public decimal GetAvailableQuantity(int materialId)
        {
            // Получаем физическое количество на складе
            var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == materialId);
            decimal physicalQuantity = stock?.Количество ?? 0;

            // Считаем сумму всех активных резервов (idСтатус=8 или СтатусПозиции=true) по этому материалу
            decimal? totalReservedNullable = _context.СоставЗаказа_Материалы
                .Where(m => m.idМатериал == materialId && (m.idСтатус == 8))
                .Sum(m => (decimal?)m.Количество);
            decimal totalReserved = totalReservedNullable ?? 0;

            // Доступно = физическое - зарезервировано
            return physicalQuantity - totalReserved;
        }

        /// <summary>
        /// Получить физическое количество материала на складе
        /// </summary>
        /// <param name="materialId">ID материала</param>
        /// <returns>Физическое количество</returns>
        public decimal GetPhysicalQuantity(int materialId)
        {
            var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == materialId);
            return stock?.Количество ?? 0;
        }

        /// <summary>
        /// Проверка доступности материалов на складе
        /// Учитывает все активные резервы по другим заказам
        /// </summary>
        /// <param name="materials">Список материалов для проверки</param>
        /// <returns>Результат проверки с информацией о недостаточных материалах</returns>
        public (bool IsAvailable, List<string> Errors) CheckAvailability(List<OrderMaterialItem> materials)
        {
            var errors = new List<string>();

            foreach (var item in materials)
            {
                if (!item.IsReserved && item.idСтатус != 8) continue;

                // Получаем доступное количество с учетом всех резервов
                decimal available = GetAvailableQuantity(item.idМатериала);

                if (available < item.Количество)
                {
                    decimal physical = _context.Склад
                        .Where(s => s.idМатериал == item.idМатериала)
                        .Select(s => (decimal?)s.Количество)
                        .FirstOrDefault() ?? 0;

                    decimal? reservedNullable = _context.СоставЗаказа_Материалы
                        .Where(m => m.idМатериал == item.idМатериала && (m.idСтатус == 8))
                        .Sum(m => (decimal?)m.Количество);
                    decimal reserved = reservedNullable ?? 0;

                    errors.Add($"Недостаточно материала '{item.Наименование}'. Доступно: {available}, требуется: {item.Количество} (на складе: {physical}, зарезервировано: {reserved})");
                }
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Списание материалов со склада для заказа
        /// Уменьшает физическое количество на складе (Склад.Количество)
        /// СтатусПозиции=true устанавливается автоматически при сохранении заказа
        /// </summary>
        /// <param name="orderId">ID заказа</param>
        /// <param name="materials">Список материалов для списания</param>
        public void DeductMaterials(int orderId, List<OrderMaterialItem> materials)
        {
            // Физическое списание: уменьшаем Количество в таблице Склад
            foreach (var item in materials)
            {
                if (!item.IsNew && item.idПозиции > 0)
                {
                    var entity = _context.СоставЗаказа_Материалы.Find(item.idПозиции);
                    if (entity != null && entity.idСтатус == 8)
                    {
                        // Находим складскую запись и уменьшаем физическое количество
                        var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == entity.idМатериал);
                        if (stock != null)
                        {
                            if (stock.Количество >= entity.Количество)
                            {
                                stock.Количество -= entity.Количество;
                                entity.idСтатус = 9; // Помечаем как списанное
                            }
                            else
                            {
                                throw new Exception($"Недостаточно материала '{item.Наименование}' на складе. Доступно: {stock.Количество}, требуется: {entity.Количество}");
                            }
                        }
                    }
                }
            }
            _context.SaveChanges();
        }

        /// <summary>
        /// Возврат материалов на склад при отмене заказа
        /// Восстанавливает физическое количество на складе (Склад.Количество)
        /// и снимает флаг СтатусПозиции (устанавливает в false)
        /// </summary>
        /// <param name="orderId">ID заказа</param>
        /// <param name="materials">Список материалов для возврата</param>
        public void ReturnMaterials(int orderId, List<OrderMaterialItem> materials)
        {
            // Возврат: восстанавливаем физическое количество и снимаем флаг списания
            var orderMaterials = _context.СоставЗаказа_Материалы
                .Where(m => m.idЗаказ == orderId)
                .ToList();

            foreach (var entity in orderMaterials)
            {
                if (entity.idСтатус == 9)
                {
                    // Восстанавливаем физическое количество на складе
                    var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == entity.idМатериал);
                    if (stock != null)
                    {
                        stock.Количество += entity.Количество;
                    }
                    // Снимаем флаг списания
                    entity.idСтатус = 8;
                }
            }
            _context.SaveChanges();
        }

        /// <summary>
        /// Возврат материала на склад при удалении из заказа (когда заказ в работе)
        /// В новой логике: просто ничего не делает, т.к. при удалении позиции из заказа
        /// она удаляется из таблицы СоставЗаказа_Материалы и автоматически перестает быть резервом
        /// </summary>
        /// <param name="orderId">ID заказа</param>
        /// <param name="materialId">ID материала</param>
        /// <param name="quantity">Количество для возврата</param>
        /// <param name="materialName">Наименование материала</param>
        public void ReturnSingleMaterial(int orderId, int materialId, decimal quantity, string materialName)
        {
            // В новой архитектуре при удалении позиции из заказа она исчезает из таблицы резервов
            // Поэтому материал автоматически становится доступным - никаких дополнительных действий не требуется
        }

        /// <summary>
        /// Отмена возврата материалов (при возобновлении заказа из статуса "Отменен")
        /// Снова списывает материалы со склада и восстанавливает флаг СтатусПозиции=true
        /// </summary>
        /// <param name="orderId">ID заказа</param>
        /// <param name="materials">Список материалов</param>
        public void ReverseCancelReturn(int orderId, List<OrderMaterialItem> materials)
        {
            // Возобновление: снова списываем материалы и устанавливаем флаг СтатусПозиции=true
            var orderMaterials = _context.СоставЗаказа_Материалы
                .Where(m => m.idЗаказ == orderId)
                .ToList();

            foreach (var entity in orderMaterials)
            {
                if (entity.idСтатус == 8)
                {
                    // Снова списываем со склада
                    var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == entity.idМатериал);
                    if (stock != null)
                    {
                        if (stock.Количество >= entity.Количество)
                        {
                            stock.Количество -= entity.Количество;
                            entity.idСтатус = 9;
                        }
                        else
                        {
                            throw new Exception($"Недостаточно материала для возобновления заказа. Доступно: {stock.Количество}, требуется: {entity.Количество}");
                        }
                    }
                }
            }
            _context.SaveChanges();
        }

        /// <summary>
        /// Очистка записей журнала действий для заказа
        /// Удалено: ЖурналДействий больше не используется в новой архитектуре
        /// </summary>
        /// <param name="orderId">ID заказа</param>
        public void ClearJournalForOrder(int orderId)
        {
            // В новой архитектуре журнал действий не используется
            // Все данные о резервах хранятся в СтатусПозиции таблицы СоставЗаказа_Материалы
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}