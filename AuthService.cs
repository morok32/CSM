using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComputerServiceManager
{
    /// <summary>
    /// Сервис аутентификации и хранения текущего сеанса пользователя
    /// </summary>
    public static class AuthService
    {
        private static Пользователь _currentUser;

        /// Текущий авторизованный пользователь
        public static Пользователь CurrentUser => _currentUser;
        /// ID текущего пользователя
        public static int? CurrentUserId => _currentUser?.idПользователь;
        /// Наименование роли текущего пользователя
        public static string CurrentUserRole => _currentUser?.Роль?.Наименование;
        /// ID роли текущего пользователя
        public static int? CurrentRoleId => _currentUser?.idРоль;
        /// Проверка, авторизован ли пользователь
        public static bool IsAuthenticated => _currentUser != null;
        /// Проверка, является ли текущий пользователь администратором
        public static bool IsAdmin => string.Equals(CurrentUserRole, "Администратор", StringComparison.OrdinalIgnoreCase);
        /// Проверка, является ли текущий пользователь техником
        public static bool IsTechnician => string.Equals(CurrentUserRole, "Техник", StringComparison.OrdinalIgnoreCase);
        public static Пользователь Authenticate(string login, string password)
        {
            using (var context = ComputerServiceManagerEntities.GetContext())
            {
                var user = context.Пользователь
                    .Include("Роль")
                    .FirstOrDefault(u => u.Логин == login && u.Пароль == password && u.Активность == true);

                if (user != null)
                {
                    _currentUser = user;
                }

                return user;
            }
        }

        /// <summary>
        /// Выход из системы
        /// </summary>
        public static void Logout()
        {
            _currentUser = null;
        }

        /// <summary>
        /// Проверка права доступа к функционалу
        /// </summary>
        /// <param name="requiredRole">Требуемая роль</param>
        /// <returns>true если доступ разрешён</returns>
        public static bool HasAccess(string requiredRole)
        {
            if (!IsAuthenticated)
                return false;

            return string.Equals(CurrentUserRole, requiredRole, StringComparison.OrdinalIgnoreCase) || IsAdmin;
        }
    }
}
