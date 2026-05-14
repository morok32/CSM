using System;
using System.Text.RegularExpressions;

namespace ComputerServiceManager.Extensions
{
    /// <summary>
    /// Методы расширения для работы со строками
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Удаляет лишние пробелы и объединяет части ФИО
        /// </summary>
        public static string ToFullName(string surname, string name, string patronymic)
        {
            var parts = new[] { surname, name, patronymic };
            var fullName = string.Join(" ", parts);

            // Заменяем множественные пробелы на один и убираем пробелы по краям
            return Regex.Replace(fullName, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Безопасное получение строки (защита от null)
        /// </summary>
        public static string OrEmpty(this string str)
        {
            return str ?? string.Empty;
        }
    }
}