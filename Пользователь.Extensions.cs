using System.ComponentModel.DataAnnotations.Schema;
using ComputerServiceManager.Extensions;

namespace ComputerServiceManager
{
    public partial class Пользователь
    {
        /// <summary>
        /// Вычисляемое свойство для полного имени пользователя (ФИО)
        /// </summary>
        [NotMapped]
        public string ФИО => StringExtensions.ToFullName(Фамилия, Имя, Отчество);
    }
}
