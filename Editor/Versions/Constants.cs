namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Пути, имена и шаблоны окна версий локаций.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Заголовок окна.
        /// </summary>
        internal const string WindowTitle = "Версии локаций";

        /// <summary>
        /// Пункт меню, открывающий окно.
        /// </summary>
        internal const string MenuPath = "PaperFly/Приём/Версии локаций";

        /// <summary>
        /// Начало имени git-пакетов, которые показывает окно.
        /// </summary>
        internal const string PackagePrefix = "com.paperfly.";

        /// <summary>
        /// Папка с копиями истории репозиториев. Лежит в Library, поэтому не попадает в git.
        /// </summary>
        internal const string HistoryCacheDirectory = "Library/PaperFly/LocationHistory";

        /// <summary>
        /// Сколько ждать ответа git. Дольше считаем, что он завис.
        /// </summary>
        internal const int GitTimeoutMilliseconds = 120000;

        /// <summary>
        /// Длина короткого хеша коммита.
        /// </summary>
        internal const int ShortHashLength = 7;

        /// <summary>
        /// Адрес репозитория: HTTPS или SSH, без ветки и подпапки.
        /// </summary>
        internal const string RepositoryUrlPattern = @"^(https://|ssh://|git@)[^\s""'#?]+$";

        /// <summary>
        /// Адрес пакета: адрес репозитория и, если пакет лежит в подпапке, ?path=/папка.
        /// </summary>
        internal const string PackageAddressPattern = @"^(https://|ssh://|git@)[^\s""'#?]+(\?path=/[^\s""'#?]*)?$";

        /// <summary>
        /// Хеш, тег или ветка. Без пробелов и символов, которые git понимает как опции или операторы.
        /// </summary>
        internal const string RevisionPattern = @"^[0-9A-Za-z][0-9A-Za-z._/\-]*$";
    }
}
