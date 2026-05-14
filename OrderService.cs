using ComputerServiceManager.Models;
using ComputerServiceManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;

namespace ComputerServiceManager.Services
{
    public class OrderService : IDisposable
    {
        private readonly ComputerServiceManagerEntities _context;

        public OrderService()
        {
            _context = new ComputerServiceManagerEntities();
        }

        // Для обратной совместимости (старый код, который создает WarehouseService)
        public OrderService(ComputerServiceManagerEntities context)
        {
            _context = context;
        }

        public List<Заказ> GetOrders(string filterText = null)
        {
            var query = _context.Заказ
                .Include(z => z.Клиент)
                .Include(z => z.Статус)
                .Include(z => z.ТипУстройства)
                .Include(z => z.Пользователь)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                var lowerFilter = filterText.ToLower();
                query = query.Where(z =>
                    (z.Клиент != null &&
                    (z.Клиент.Фамилия.ToLower().Contains(lowerFilter) ||
                    z.Клиент.Имя.ToLower().Contains(lowerFilter) ||
                    z.Клиент.Отчество.ToLower().Contains(lowerFilter))) ||
                    z.ИмяУстройства.ToLower().Contains(lowerFilter) ||
                    z.СерийныйНомер.ToLower().Contains(lowerFilter));
            }

            return query.OrderByDescending(z => z.ДатаЗаказа).ToList();
        }

        public (List<OrderMaterialItem> Materials, List<OrderServiceItem> Services) GetOrderItems(int orderId)
        {
            var materials = _context.СоставЗаказа_Материалы
                .Include("Материал")
                .Where(m => m.idЗаказ == orderId)
                .Select(m => new OrderMaterialItem
                {
                    idПозиции = m.idПозиция,
                    idМатериала = m.idМатериал ?? 0,
                    Наименование = m.Материал != null ? m.Материал.Наименование : "Неизвестно",
                    Количество = m.Количество ?? 0,
                    ЦенаЗаЕдиницу = m.ЦенаЗаЕдиницу ?? 0,
                    СтоимостьПозиции = m.СтоимостьПозиции ?? 0,
                    idСтатус = m.idСтатус ?? 8, // По умолчанию статус=Резерв (8), если статус не установлен
                    IsNew = false
                }).ToList();

            var services = _context.СоставЗаказа_Услуги
                .Include("Услуги")
                .Where(s => s.idЗаказ == orderId)
                .Select(s => new OrderServiceItem
                {
                    idПозиции = s.idПозиция,
                    idУслуги = s.idУслуга ?? 0,
                    Наименование = s.Услуги != null ? s.Услуги.Наименование : "Неизвестно",
                    Количество = s.Количество ?? 1,
                    ЦенаЗаЕдиницу = s.ЦенаЗаЕдиницу ?? 0,
                    СтоимостьПозиции = s.СтоимостьПозиции ?? 0,
                    IsNew = false
                }).ToList();

            return (materials, services);
        }

        public void SaveOrder(Заказ order, List<OrderMaterialItem> materials, List<OrderServiceItem> services, bool isNew)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    int? previousStatusId = null;

                    if (isNew)
                    {
                        // Сначала сохраняем клиента, если он есть
                        if (order.Клиент != null)
                        {
                            _context.Клиент.Add(order.Клиент);
                            _context.SaveChanges(); // Сохраняем, чтобы получить idКлиент
                            order.idКлиент = order.Клиент.idКлиент; // Присваиваем ID клиента заказу
                        }

                        order.Пользователь = null;
                        order.Статус = null;
                        order.ТипУстройства = null;
                        _context.Заказ.Add(order);
                    }
                    else
                    {
                        var existingOrder = _context.Заказ.Find(order.idЗаказ);
                        previousStatusId = existingOrder.idСтатус;

                        existingOrder.ИмяУстройства = order.ИмяУстройства;
                        existingOrder.СерийныйНомер = order.СерийныйНомер;
                        existingOrder.idТипУстройства = order.idТипУстройства;
                        existingOrder.Неисправность = order.Неисправность;
                        existingOrder.idПользователь = order.idПользователь;
                        existingOrder.ДатаЗаказа = order.ДатаЗаказа;
                        existingOrder.idСтатус = order.idСтатус;

                        if (order.Клиент != null && existingOrder.idКлиент.HasValue)
                        {
                            var existingClient = _context.Клиент.Find(existingOrder.idКлиент.Value);
                            if (existingClient != null)
                            {
                                existingClient.Фамилия = order.Клиент.Фамилия;
                                existingClient.Имя = order.Клиент.Имя;
                                existingClient.Отчество = order.Клиент.Отчество;
                                existingClient.НомерТелефона = order.Клиент.НомерТелефона;
                            }
                        }

                        order = existingOrder;
                    }

                    _context.Configuration.AutoDetectChangesEnabled = true;
                    _context.SaveChanges();

                    SyncMaterials(order.idЗаказ, materials, previousStatusId, order.idСтатус);
                    SyncServices(order.idЗаказ, services);

                    _context.SaveChanges();

                    foreach (var item in materials)
                    {
                        if (item.IsNew && item.DbEntity != null && item.DbEntity.idПозиция > 0)
                        {
                            item.idПозиции = item.DbEntity.idПозиция;
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Ошибка сохранения: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Списание материалов для заказа, выданного клиенту
        /// Вызывается автоматически при смене статуса на "Выдан клиенту" (6)
        /// </summary>
        public void WriteOffMaterialsForDeliveredOrder(int orderId, List<OrderMaterialItem> materials)
        {
            foreach (var item in materials)
            {
                if (!item.IsNew && item.idПозиции > 0)
                {
                    var entity = _context.СоставЗаказа_Материалы.Find(item.idПозиции);
                    if (entity != null && entity.idСтатус == 8) // Только резервированные материалы
                    {
                        var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == entity.idМатериал);
                        if (stock != null)
                        {
                            if (stock.Количество >= entity.Количество)
                            {
                                stock.Количество -= entity.Количество;
                                entity.idСтатус = 9; // Списан
                                item.idСтатус = 9;   // Обновляем UI модель
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
        /// Завершить заказ со списанием материалов (статус "В работе")
        /// Устаревший метод, используется для обратной совместимости
        /// </summary>
        [Obsolete("Используйте WriteOffMaterialsForDeliveredOrder")]
        public void CompleteOrderWithDeduction(int orderId, List<OrderMaterialItem> materials)
        {
            DeductMaterials(orderId, materials);
        }

        /// <summary>
        /// Отменить заказ с возвратом материалов на склад (снятием резерва)
        /// Возвращает зарезервированные материалы в доступное количество на складе
        /// </summary>
        public void CancelOrderWithReturn(int orderId, List<OrderMaterialItem> materials)
        {
            var orderMaterials = _context.СоставЗаказа_Материалы
                .Where(m => m.idЗаказ == orderId)
                .ToList();

            foreach (var entity in orderMaterials)
            {
                // Возвращаем только зарезервированные материалы (idСтатус == 8)
                // Списанные материалы (idСтатус == 9) уже физически отсутствуют на складе
                if (entity.idСтатус == 8)
                {
                    // Снимаем резерв - увеличиваем физическое количество на складе
                    var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == entity.idМатериал);
                    if (stock != null)
                    {
                        stock.Количество += entity.Количество;
                    }
                    // Меняем статус на неактивный (снят с резерва)
                    entity.idСтатус = null; 
                }
            }
            _context.SaveChanges();
        }

        /// <summary>
        /// Получить доступное количество материала (физическое минус все активные резервы)
        /// </summary>
        public decimal GetAvailableQuantity(int materialId)
        {
            var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == materialId);
            decimal physicalQuantity = stock?.Количество ?? 0;

            decimal? totalReservedNullable = _context.СоставЗаказа_Материалы
                .Where(m => m.idМатериал == materialId && (m.idСтатус == 8))
                .Sum(m => (decimal?)m.Количество);
            decimal totalReserved = totalReservedNullable ?? 0;

            return physicalQuantity - totalReserved;
        }

        /// <summary>
        /// Списание материалов со склада для заказа
        /// Уменьшает физическое количество на складе (Склад.Количество)
        /// </summary>
        private void DeductMaterials(int orderId, List<OrderMaterialItem> materials)
        {
            foreach (var item in materials)
            {
                if (!item.IsNew && item.idПозиции > 0)
                {
                    var entity = _context.СоставЗаказа_Материалы.Find(item.idПозиции);
                    if (entity != null && entity.idСтатус == 8)
                    {
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
        /// Восстанавливает физическое количество на складе и снимает флаг списания
        /// </summary>
        private void ReturnMaterials(int orderId, List<OrderMaterialItem> materials)
        {
            var orderMaterials = _context.СоставЗаказа_Материалы
                .Where(m => m.idЗаказ == orderId)
                .ToList();

            foreach (var entity in orderMaterials)
            {
                if (entity.idСтатус == 9)
                {
                    var stock = _context.Склад.FirstOrDefault(s => s.idМатериал == entity.idМатериал);
                    if (stock != null)
                    {
                        stock.Количество += entity.Количество;
                    }
                    entity.idСтатус = 8;
                }
            }
            _context.SaveChanges();
        }

        /// <summary>
        /// Проверка доступности материалов на складе
        /// </summary>
        public (bool IsAvailable, List<string> Errors) CheckAvailability(List<OrderMaterialItem> materials)
        {
            var errors = new List<string>();

            foreach (var item in materials)
            {
                if (item.idСтатус != 8) continue;

                decimal available = GetAvailableQuantity(item.idМатериала);

                if (available < item.Количество)
                {
                    var stockInfo = _context.Склад
                        .Where(s => s.idМатериал == item.idМатериала)
                        .Select(s => new { Physical = s.Количество })
                        .FirstOrDefault();

                    decimal physical = stockInfo?.Physical ?? 0;
                    decimal reserved = physical - available;

                    errors.Add($"Недостаточно материала '{item.Наименование}'. Доступно: {available}, требуется: {item.Количество} (на складе: {physical}, зарезервировано: {reserved})");
                }
            }

            return (errors.Count == 0, errors);
        }

        private void SyncMaterials(int orderId, List<OrderMaterialItem> items, int? previousStatusId = null, int? currentStatusId = null)
        {
            var existingItems = _context.СоставЗаказа_Материалы.Where(x => x.idЗаказ == orderId).ToList();

            foreach (var item in items)
            {
                if (item.IsNew)
                {
                    // РЕШЕНИЕ ПРОБЛЕМЫ №1: Новые материалы автоматически резервируются при сохранении
                    // idСтатус = 8 означает, что материал зарезервирован для этого заказа
                    var newEntity = new СоставЗаказа_Материалы
                    {
                        idЗаказ = orderId,
                        idМатериал = item.idМатериала,
                        Количество = item.Количество,
                        ЦенаЗаЕдиницу = item.ЦенаЗаЕдиницу,
                        СтоимостьПозиции = item.СтоимостьПозиции,
                        idСтатус = 8, // Автоматически резервируем новый материал (статус "Резерв")
                    };
                    _context.СоставЗаказа_Материалы.Add(newEntity);
                    item.DbEntity = newEntity;
                    item.idСтатус = 8; // Обновляем статус в UI модели
                }
                else
                {
                    var entity = existingItems.FirstOrDefault(x => x.idПозиция == item.idПозиции);
                    if (entity != null)
                    {
                        entity.Количество = item.Количество;
                        entity.ЦенаЗаЕдиницу = item.ЦенаЗаЕдиницу;
                        entity.СтоимостьПозиции = item.СтоимостьПозиции;
                        entity.idСтатус = item.idСтатус ?? entity.idСтатус;
                        entity.idМатериал = item.idМатериала;
                    }
                }
            }

            var idsToKeep = items.Where(i => !i.IsNew).Select(i => i.idПозиции).ToList();
            var toRemove = existingItems.Where(x => !idsToKeep.Contains(x.idПозиция)).ToList();

            foreach (var rem in toRemove)
            {
                // При удалении позиции из заказа она исчезает из таблицы резервов
                // Материал автоматически становится доступным
                _context.СоставЗаказа_Материалы.Remove(rem);
            }
            _context.SaveChanges();
        }

        private void SyncServices(int orderId, List<OrderServiceItem> items)
        {
            var existingItems = _context.СоставЗаказа_Услуги.Where(x => x.idЗаказ == orderId).ToList();

            foreach (var item in items)
            {
                if (item.IsNew)
                {
                    _context.СоставЗаказа_Услуги.Add(new СоставЗаказа_Услуги
                    {
                        idЗаказ = orderId,
                        idУслуга = item.idУслуги,
                        Количество = item.Количество,
                        ЦенаЗаЕдиницу = item.ЦенаЗаЕдиницу,
                        СтоимостьПозиции = item.СтоимостьПозиции,
                    });
                }
                else
                {
                    var entity = existingItems.FirstOrDefault(x => x.idПозиция == item.idПозиции);
                    if (entity != null)
                    {
                        entity.Количество = item.Количество;
                        entity.ЦенаЗаЕдиницу = item.ЦенаЗаЕдиницу;
                        entity.СтоимостьПозиции = item.СтоимостьПозиции;
                    }
                }
            }

            var idsToKeep = items.Where(i => !i.IsNew).Select(i => i.idПозиции).ToList();
            var toRemove = existingItems.Where(x => !idsToKeep.Contains(x.idПозиция)).ToList();
            foreach (var rem in toRemove)
            {
                _context.СоставЗаказа_Услуги.Remove(rem);
            }
        }

        public void DeleteOrder(int orderId)
        {
            DeleteOrders(new List<int> { orderId });
        }

        public void DeleteOrders(List<int> orderIds)
        {
            using (var context = new ComputerServiceManagerEntities())
            using (var transaction = context.Database.BeginTransaction())
            {
                try
                {
                    foreach (var orderId in orderIds)
                    {
                        // Загружаем все связанные данные перед удалением
                        var order = context.Заказ
                            .Include("Счет")
                            .Include("Счет.Платеж")
                            .Include("СоставЗаказа_Материалы")
                            .Include("СоставЗаказа_Услуги")
                            .FirstOrDefault(z => z.idЗаказ == orderId);

                        if (order == null) continue;

                        foreach (var mat in order.СоставЗаказа_Материалы.ToList())
                        {
                            // Если материал зарезервирован (idСтатус == 8), просто удаляем запись
                            // Физическое количество не меняется, т.к. резерв еще не был списан
                            if (mat.idСтатус == 8)
                            {
                                // Просто удаляем запись - резерв снимается автоматически
                            }
                        }
                        context.SaveChanges();

                        // Удаляем платежи по всем счетам
                        foreach (var invoice in order.Счет.ToList())
                        {
                            foreach (var payment in invoice.Платеж.ToList())
                            {
                                context.Платеж.Remove(payment);
                            }
                        }

                        // Удаляем счета
                        foreach (var invoice in order.Счет.ToList())
                        {
                            context.Счет.Remove(invoice);
                        }

                        // Удаляем позиции материалов (связи)
                        foreach (var mat in order.СоставЗаказа_Материалы.ToList())
                        {
                            context.СоставЗаказа_Материалы.Remove(mat);
                        }

                        // Удаляем позиции услуг (связи)
                        foreach (var serv in order.СоставЗаказа_Услуги.ToList())
                        {
                            context.СоставЗаказа_Услуги.Remove(serv);
                        }

                        // Сохраняем изменения перед удалением заказа
                        context.SaveChanges();

                        // Теперь удаляем сам заказ
                        context.Заказ.Remove(order);
                    }

                    context.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Ошибка удаления: {ex.Message}", ex);
                }
            }
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}