using ComputerServiceManager.Services;
using System;
using System.Linq;
using System.IO;

namespace ComputerServiceManager.Services
{
    /// <summary>
    /// Сервис для генерации печатных форм (чеки/накладные)
    /// </summary>
    public class DocumentService : IDisposable
    {
        private readonly ComputerServiceManagerEntities _context;

        public DocumentService()
        {
            _context = new ComputerServiceManagerEntities();
        }

        /// <summary>
        /// Генерирует HTML-документ заказа-наряда (чека/накладной)
        /// </summary>
        /// <param name="order">Заказ</param>
        /// <returns>Путь к сгенерированному HTML файлу</returns>
        public string GenerateInvoiceHtml(Заказ order)
        {
            if (order == null || order.idЗаказ <= 0)
                throw new ArgumentException("Заказ должен быть сохранен в БД");

            // Получаем актуальные данные о составе заказа из БД
            var materials = _context.СоставЗаказа_Материалы
                .Include("Материал")
                .Where(m => m.idЗаказ == order.idЗаказ)
                .Select(m => new OrderMaterialItem
                {
                    idПозиции = m.idПозиция,
                    idМатериала = m.idМатериал ?? 0,
                    Наименование = m.Материал != null ? m.Материал.Наименование : "Неизвестно",
                    Количество = m.Количество ?? 0,
                    ЦенаЗаЕдиницу = m.ЦенаЗаЕдиницу ?? 0,
                    СтоимостьПозиции = m.СтоимостьПозиции ?? 0,
                    idСтатус = m.idСтатус ?? 8,
                    IsNew = false
                }).ToList();

            var services = _context.СоставЗаказа_Услуги
                .Include("Услуги")
                .Where(s => s.idЗаказ == order.idЗаказ)
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

            decimal matTotal = materials.Sum(m => m.СтоимостьПозиции);
            decimal svcTotal = services.Sum(s => s.СтоимостьПозиции);
            decimal grandTotal = matTotal + svcTotal;

            string clientName = order.Клиент?.ФИО ?? "Не указан";
            string deviceModel = order.ИмяУстройства ?? "Не указано";
            string serialNumber = order.СерийныйНомер ?? "";
            string orderDate = order.ДатаЗаказа?.ToString("dd.MM.yyyy") ?? DateTime.Now.ToString("dd.MM.yyyy");
            string statusName = order.Статус?.Наименование ?? "Не указан";
            string technicianName = order.Пользователь?.ФИО ?? "Не назначен";
            string malfunction = order.Неисправность ?? "Не указана";

            // Формируем строки таблицы материалов
            string materialsRows = "";
            foreach (var m in materials)
            {
                materialsRows += $@"
                <tr>
                    <td>{m.Наименование}</td>
                    <td style='text-align:center;'>{m.Количество}</td>
                    <td style='text-align:right;'>{m.ЦенаЗаЕдиницу:F2} ₽</td>
                    <td style='text-align:right;'>{m.СтоимостьПозиции:F2} ₽</td>
                </tr>";
            }

            // Формируем строки таблицы услуг
            string servicesRows = "";
            foreach (var s in services)
            {
                servicesRows += $@"
                <tr>
                    <td>{s.Наименование}</td>
                    <td style='text-align:center;'>{s.Количество}</td>
                    <td style='text-align:right;'>{s.ЦенаЗаЕдиницу:F2} ₽</td>
                    <td style='text-align:right;'>{s.СтоимостьПозиции:F2} ₽</td>
                </tr>";
            }

            // Расшифровка суммы прописью
            string totalInWords = NumberToWordsRussian(grandTotal);

            string html = $@"
<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <title>Заказ-наряд №{order.idЗаказ}</title>
    <style>
        body {{ font-family: Arial, sans-serif; padding: 20px; font-size: 14px; }}
        .header {{ text-align: center; border-bottom: 2px solid #333; padding-bottom: 10px; margin-bottom: 20px; }}
        .header h1 {{ margin: 5px 0; font-size: 18px; }}
        .info {{ margin-bottom: 20px; }}
        .info table {{ width: 100%; border-collapse: collapse; }}
        .info td {{ padding: 5px; }}
        .section-title {{ font-weight: bold; margin-top: 15px; margin-bottom: 5px; background-color: #f0f0f0; padding: 5px; }}
        table.items {{ width: 100%; border-collapse: collapse; margin-bottom: 15px; }}
        table.items th, table.items td {{ border: 1px solid #ccc; padding: 6px; }}
        table.items th {{ background-color: #f5f5f5; text-align: left; }}
        .total {{ text-align: right; font-size: 16px; font-weight: bold; margin-top: 10px; }}
        .total-words {{ text-align: right; font-size: 14px; font-style: italic; margin-top: 5px; color: #555; }}
        .footer {{ margin-top: 30px; border-top: 1px solid #ccc; padding-top: 10px; }}
        .signature {{ margin-top: 20px; }}
        @media print {{
            body {{ padding: 0; }}
            .no-print {{ display: none; }}
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Заказ-наряд №{order.idЗаказ}</h1>
        <p>от {orderDate}</p>
    </div>

    <div class='info'>
        <table>
            <tr>
                <td style='width: 50%;'><strong>Клиент:</strong> {clientName}</td>
                <td><strong>Статус:</strong> {statusName}</td>
            </tr>
            <tr>
                <td><strong>Устройство:</strong> {deviceModel} {(!string.IsNullOrEmpty(serialNumber) ? $"(S/N: {serialNumber})" : "")}</td>
                <td><strong>Техник:</strong> {technicianName}</td>
            </tr>
        </table>
    </div>

    {(materials.Count > 0 ? $@"
    <div class='section-title'>Материалы</div>
    <table class='items'>
        <thead>
            <tr>
                <th>Наименование</th>
                <th style='width: 60px; text-align:center;'>Кол-во</th>
                <th style='text-align:right;'>Цена</th>
                <th style='text-align:right;'>Сумма</th>
            </tr>
        </thead>
        <tbody>{materialsRows}
        </tbody>
    </table>" : "")}

    {(services.Count > 0 ? $@"
    <div class='section-title'>Услуги</div>
    <table class='items'>
        <thead>
            <tr>
                <th>Наименование</th>
                <th style='width: 60px; text-align:center;'>Кол-во</th>
                <th style='text-align:right;'>Цена</th>
                <th style='text-align:right;'>Сумма</th>
            </tr>
        </thead>
        <tbody>{servicesRows}
        </tbody>
    </table>" : "")}

    <div class='total'>
        Итого к оплате: {grandTotal:F2} ₽
    </div>
    <div class='total-words'>
        ({totalInWords})
    </div>

    <div class='footer'>
        <p>Неисправность: {malfunction}</p>
    </div>

    <div class='signature'>
        <p>Подпись мастера: _________________ / {technicianName}</p>
        <p style='margin-top: 15px;'>Подпись клиента: _________________ / {clientName}</p>
    </div>

    <div class='no-print' style='margin-top: 20px; text-align: center;'>
        <button onclick='window.print()' style='padding: 10px 20px; font-size: 14px; cursor: pointer;'>🖨️ Печать / Сохранить в PDF</button>
    </div>
</body>
</html>";

            string path = Path.Combine(Path.GetTempPath(), $"Invoice_{order.idЗаказ}.html");
            File.WriteAllText(path, html, System.Text.Encoding.UTF8);
            return path;
        }

        /// <summary>
        /// Переводит число в сумму прописью на русском языке
        /// </summary>
        private string NumberToWordsRussian(decimal amount)
        {
            int rubles = (int)Math.Floor(amount);
            int kopecks = (int)Math.Round((amount - rubles) * 100);

            string rublesText = ConvertNumberToWords(rubles);
            string kopecksText = kopecks.ToString("D2");

            return $"{rublesText} рублей {kopecksText} копеек";
        }

        /// <summary>
        /// Преобразует число в слова (рубли)
        /// </summary>
        private string ConvertNumberToWords(int number)
        {
            if (number == 0) return "ноль";

            string[] ones = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
            string[] teens = { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
            string[] tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
            string[] hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

            if (number < 10) return ones[number];
            if (number < 20) return teens[number - 10];
            if (number < 100)
            {
                int t = number / 10;
                int o = number % 10;
                return o == 0 ? tens[t] : tens[t] + " " + ones[o];
            }
            if (number < 1000)
            {
                int h = number / 100;
                int rest = number % 100;
                string restText = rest > 0 ? " " + ConvertNumberToWords(rest) : "";
                return hundreds[h] + restText;
            }
            if (number < 1000000)
            {
                int th = number / 1000;
                int rest = number % 1000;
                string thText;
                if (th == 1) thText = "одна тысяча";
                else if (th >= 2 && th <= 4) thText = "две тысячи";
                else thText = ConvertNumberToWords(th) + " тысяч";
                string restText = rest > 0 ? " " + ConvertNumberToWords(rest) : "";
                return (thText + " " + restText).Trim();
            }
            if (number < 1000000000)
            {
                int mil = number / 1000000;
                int rest = number % 1000000;
                string milText;
                if (mil == 1) milText = "один миллион";
                else if (mil >= 2 && mil <= 4) milText = "два миллиона";
                else milText = ConvertNumberToWords(mil) + " миллионов";
                string restText = rest > 0 ? " " + ConvertNumberToWords(rest) : "";
                return (milText + " " + restText).Trim();
            }

            return number.ToString();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }

    /// <summary>
    /// Вспомогательный класс для отображения материалов в составе заказа
    /// </summary>
    public class OrderMaterialItem
    {
        public int idПозиции { get; set; }
        public int idМатериала { get; set; }
        public string Наименование { get; set; }
        public int Количество { get; set; }
        public decimal ЦенаЗаЕдиницу { get; set; }
        public decimal СтоимостьПозиции { get; set; }
        public int idСтатус { get; set; }
        public bool IsNew { get; set; }
    }

    /// <summary>
    /// Вспомогательный класс для отображения услуг в составе заказа
    /// </summary>
    public class OrderServiceItem
    {
        public int idПозиции { get; set; }
        public int idУслуги { get; set; }
        public string Наименование { get; set; }
        public int Количество { get; set; }
        public decimal ЦенаЗаЕдиницу { get; set; }
        public decimal СтоимостьПозиции { get; set; }
        public bool IsNew { get; set; }
    }
}