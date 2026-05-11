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
        private readonly WarehouseService _warehouseService;

        public OrderService()
        {
            _context = new ComputerServiceManagerEntities();
            _warehouseService = new WarehouseService(_context);
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
        /// Завершить заказ со списанием материалов (статус "В работе")
        /// </summary>
        public void CompleteOrderWithDeduction(int orderId, List<OrderMaterialItem> materials)
        {
            _warehouseService.DeductMaterials(orderId, materials);
        }

        /// <summary>
        /// Отменить заказ с возвратом материалов на склад
        /// </summary>
        public void CancelOrderWithReturn(int orderId, List<OrderMaterialItem> materials)
        {
            _warehouseService.ReturnMaterials(orderId, materials);
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
            using (var context = new ComputerServiceManagerEntities())
            using (var transaction = context.Database.BeginTransaction())
            {
                try
                {
                    var order = context.Заказ.Find(orderId);
                    if (order == null) return;

                    // Загружаем все связанные данные перед удалением
                    context.Configuration.LazyLoadingEnabled = true;
                    order = context.Заказ
                        .Include("Счет")
                        .Include("Счет.Платеж")
                        .Include("СоставЗаказа_Материалы")
                        .Include("СоставЗаказа_Услуги")
                        .FirstOrDefault(z => z.idЗаказ == orderId);

                    if (order == null) return;

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