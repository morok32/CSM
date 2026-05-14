using System.ComponentModel.DataAnnotations.Schema;
using ComputerServiceManager.Extensions;

namespace ComputerServiceManager
{
    public partial class Клиент
    {
        /// <summary>
        /// Вычисляемое свойство для полного имени клиента (ФИО)
        /// </summary>
        [NotMapped] // Указывает EF, что это свойство не является столбцом в БД
        public string ФИО => StringExtensions.ToFullName(Фамилия, Имя, Отчество);
    }
}
