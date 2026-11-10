using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Проверяет введённые значения перед подстановкой в командную строку git.
    /// </summary>
    internal static class GitInput
    {
        private static readonly Regex RepositoryUrl = new Regex(Constants.RepositoryUrlPattern);

        private static readonly Regex PackageAddress = new Regex(Constants.PackageAddressPattern);

        private static readonly Regex Revision = new Regex(Constants.RevisionPattern);

        /// <summary>
        /// Проверяет адрес репозитория.
        /// </summary>
        /// <param name="url">Адрес.</param>
        /// <returns><c>true</c>, если адрес можно передать git.</returns>
        public static bool IsRepositoryUrl(string url)
            => RepositoryUrl.IsMatch(url);

        /// <summary>
        /// Проверяет адрес пакета, в том числе с подпапкой.
        /// </summary>
        /// <param name="address">Адрес.</param>
        /// <returns><c>true</c>, если адрес можно передать Package Manager.</returns>
        public static bool IsPackageAddress(string address)
            => PackageAddress.IsMatch(address);

        /// <summary>
        /// Проверяет хеш, тег или ветку.
        /// </summary>
        /// <param name="revision">Значение из поля ввода.</param>
        /// <returns><c>true</c>, если значение можно передать git.</returns>
        public static bool IsRevision(string revision)
            => Revision.IsMatch(revision);
    }
}
