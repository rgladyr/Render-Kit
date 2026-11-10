using System;
using System.Collections.Generic;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Манифест выпуска: файлы пакета с хешами и объекты префабов и моделей. Лежит в папке пакета и попадает в игру вместе с ним.
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

        /// <summary>
        /// Текущий формат. Манифест старого формата нельзя сверить с правками в игре, выпуск нужно подготовить заново.
        /// </summary>
        public const int CurrentFormat = 2;

        /// <summary>
        /// Тип объекта, по которому определяются удалённые и переименованные объекты.
        /// </summary>
        public const string GameObjectType = "GameObject";

        public int format;

        public string package;

        public string version;

        public string renderKitVersion;

        public List<FileEntry> files = new List<FileEntry>();

        public List<ObjectEntry> objects = new List<ObjectEntry>();

        /// <summary>
        /// Файл пакета, хеш его содержимого и GUID, по которому на него ссылаются файлы игры.
        /// </summary>
        [Serializable]
        public sealed class FileEntry
        {
            public string path;

            public string guid;

            public string hash;
        }

        /// <summary>
        /// Объект префаба или модели и его идентификатор, по которому правки в игре связаны с объектом пакета.
        /// </summary>
        [Serializable]
        public sealed class ObjectEntry
        {
            public string guid;

            public string asset;

            public string path;

            public string type;

            public long fileId;
        }
    }
}
