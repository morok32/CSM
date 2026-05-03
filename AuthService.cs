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

        /// <summary>
        /// Текущий авторизованный пользователь
        /// </summary>
        public static Пользователь CurrentUser => _currentUser;

        /// <summary>
        /// ID текущего пользователя
        /// </summary>
        public static int? CurrentUserId => _currentUser?.idПользователь;

        /// <summary>
        /// Наименование роли текущего пользователя
        /// </summary>
        public static string CurrentUserRole => _currentUser?.Роль?.Наименование;

        /// <summary>
        /// ID роли текущего пользователя
        /// </summary>
        public static int? CurrentRoleId => _currentUser?.idРоль;

        /// <summary>
        /// Проверка, авторизован ли пользователь
        /// </summary>
        public static bool IsAuthenticated => _currentUser != null;

        /// <summary>
        /// Проверка, является ли текущий пользователь администратором
        /// </summary>
        public static bool IsAdmin => string.Equals(CurrentUserRole, "Администратор", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Проверка, является ли текущий пользователь техником
        /// </summary>
        public static bool IsTechnician => string.Equals(CurrentUserRole, "Техник", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Аутентификация пользователя по логину и паролю
        /// </summary>
        /// <param name="login">Логин</param>
        /// <param name="password">Пароль</param>
        /// <returns>Пользователь если аутентификация успешна, иначе null</returns>
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
