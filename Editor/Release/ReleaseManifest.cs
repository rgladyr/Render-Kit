using System;
using System.Collections.Generic;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Манифест выпуска: файлы пакета с хешами и объекты основы. Лежит в папке пакета и попадает в игру вместе с ним.
    /// </summary>
    /// <remarks>
    /// Имена полей совпадают с ключами JSON, как требует JsonUtility.
    /// </remarks>
    [Serializable]
    internal sealed class ReleaseManifest
    {
        /// <summary>
        /// Имя файла манифеста в папке пакета.
        /// </summary>
        public const string FileName = "release-manifest.json";

        public string package;

        public string version;

        public string renderKitVersion;

        public List<FileEntry> files = new List<FileEntry>();

        public List<ObjectEntry> objects = new List<ObjectEntry>();

        /// <summary>
        /// Файл пакета и хеш его содержимого.
        /// </summary>
        [Serializable]
        public sealed class FileEntry
        {
            public string path;

            public string hash;
        }

        /// <summary>
        /// Объект префаба основы и его идентификатор, по которому вариант в игре связан с основой.
        /// </summary>
        [Serializable]
        public sealed class ObjectEntry
        {
            public string prefab;

            public string path;

            public long fileId;
        }
    }
}
