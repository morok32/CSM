using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerServiceManager
{
    public partial class Пользователь
    {
        /// <summary>
        /// Вычисляемое свойство для полного имени пользователя (ФИО)
        /// </summary>
        [NotMapped]
        public string ФИО
        {
            get
            {
                var fullName = $"{Фамилия?.Trim()} {Имя?.Trim()} {Отчество?.Trim()}".Trim();
                while (fullName.Contains("  "))
                {
                    fullName = fullName.Replace("  ", " ");
                }
                return fullName;
            }
        }
    }
}
