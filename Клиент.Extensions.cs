using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema; // нужно чтобы заработало [NotMapped]

namespace ComputerServiceManager
{
    public partial class Клиент
    {
        /// <summary>
        /// Вычисляемое свойство для полного имени клиента (ФИО)
        /// </summary>
        [NotMapped] // Указывает EF, что это свойство не является столбцом в БД
        public string ФИО
        {
            get
            {
                // Собираем ФИО, избегая лишних пробелов
                var fullName = $"{Фамилия?.Trim()} {Имя?.Trim()} {Отчество?.Trim()}".Trim();
                // Убираем двойные пробелы, если одно изое
                while (fullName.Contains("  "))
                {
                    fullName = fullName.Replace("  ", " ");
                }
                return fullName;
            }
        }
    }
}
