using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Acceptance.Rescue;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Помнит ссылки игры, которые очистила кнопка сохранения потерянных правок, пока разработчик их не исправит.
    /// </summary>
    /// <remarks>
    /// После сохранения в поле записана пустая ссылка, и по самому файлу её уже не отличить от поля, пустого нарочно.
    /// Поэтому список хранится отдельно, в репозитории игры, и виден всей команде. Запись уходит сама, когда ссылку
    /// назначили, правку отменили или удалили компонент, объект или файл; её можно и пропустить вручную.
    /// </remarks>
    internal static class LostReferenceStore
    {
        /// <summary>
        /// Файл списка.
        /// </summary>
        public const string FilePath = "ProjectSettings/PaperFly/LostReferences.json";

        private static Store Load()
        {
            if (!File.Exists(FilePath))
            {
                return new Store();
            }

            try
            {
                return JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store();
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[Приём локаций] Не удалось прочитать {FilePath}: {exception.Message}");

                return new Store();
            }
        }

        private static void Save(Store store)
        {
            if (store.Entries.Count == 0)
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            File.WriteAllText(
                FilePath,
                JsonUtility.ToJson(
                    store,
                    true));
        }

        private static bool IsStillEmpty(Entry entry)
        {
            var path = AssetDatabase.GUIDToAssetPath(entry.FileGuid);

            if (string.IsNullOrEmpty(path)
             || !File.Exists(path))
            {
                return false;
            }

            return LostReferenceFinder.IsStillEmpty(
                UnityYamlFile.Parse(File.ReadAllText(path)),
                entry.DocumentId,
                entry.Field,
                entry.ModificationTarget);
        }

        /// <summary>
        /// Запоминает очищенные ссылки файла.
        /// </summary>
        /// <param name="path">Путь к сцене или префабу игры.</param>
        /// <param name="packageName">Имя пакета локации, на объекты которой вели ссылки.</param>
        /// <param name="references">Ссылки, которые разработчик решил исправить вручную.</param>
        public static void Add(
            string path,
            string packageName,
            IEnumerable<LostReferenceFinder.LostReference> references)
        {
            var store = Load();
            var guid = AssetDatabase.AssetPathToGUID(path);

            foreach (var reference in references)
            {
                store.Entries.Add(
                    new Entry(
                        guid,
                        reference.Document.FileId,
                        reference.Field,
                        reference.ModificationTarget,
                        reference.Description,
                        packageName));
            }

            Save(store);
        }

        /// <summary>
        /// Возвращает ссылки, которые всё ещё пустые, и забывает исправленные.
        /// </summary>
        /// <returns>Записи об оставшихся пустыми ссылках.</returns>
        public static List<Entry> Refresh()
        {
            var store = Load();
            var remaining = store.Entries.Where(IsStillEmpty).ToList();

            if (remaining.Count != store.Entries.Count)
            {
                store.Entries.Clear();
                store.Entries.AddRange(remaining);

                Save(store);
            }

            return remaining;
        }

        /// <summary>
        /// Забывает ссылку: разработчик решил, что она не нужна.
        /// </summary>
        /// <param name="entry">Запись из <see cref="Refresh"/>.</param>
        public static void Skip(Entry entry)
        {
            var store = Load();

            store.Entries.RemoveAll(candidate => candidate.IsSame(entry));

            Save(store);
        }

        /// <summary>
        /// Очищенная ссылка, которую нужно назначить заново.
        /// </summary>
        [Serializable]
        internal sealed class Entry
        {
            [SerializeField]
            private string fileGuid;

            [SerializeField]
            private long documentId;

            [SerializeField]
            private string field;

            [SerializeField]
            private string modificationTarget;

            [SerializeField]
            private string description;

            [SerializeField]
            private string packageName;

            /// <summary>
            /// Создаёт запись.
            /// </summary>
            /// <param name="fileGuid">GUID сцены или префаба игры.</param>
            /// <param name="documentId">Идентификатор компонента или экземпляра префаба в файле.</param>
            /// <param name="field">Поле компонента или путь свойства правки.</param>
            /// <param name="modificationTarget">Цель правки «guid:fileId»; <c>null</c> для поля компонента.</param>
            /// <param name="description">Что и на что ссылалось.</param>
            /// <param name="packageName">Пакет локации.</param>
            public Entry(
                string fileGuid,
                long documentId,
                string field,
                string modificationTarget,
                string description,
                string packageName)
            {
                this.fileGuid = fileGuid;
                this.documentId = documentId;
                this.field = field;
                this.modificationTarget = modificationTarget ?? string.Empty;
                this.description = description;
                this.packageName = packageName;
            }

            // Для JsonUtility при чтении файла.
            private Entry()
            {
            }

            /// <summary>
            /// GUID сцены или префаба игры.
            /// </summary>
            public string FileGuid => this.fileGuid;

            /// <summary>
            /// Путь к сцене или префабу игры.
            /// </summary>
            public string FilePath => AssetDatabase.GUIDToAssetPath(this.fileGuid);

            /// <summary>
            /// Идентификатор компонента или экземпляра префаба в файле.
            /// </summary>
            public long DocumentId => this.documentId;

            /// <summary>
            /// Поле компонента или путь свойства правки.
            /// </summary>
            public string Field => this.field;

            /// <summary>
            /// Цель правки «guid:fileId»; <c>null</c> для поля компонента.
            /// </summary>
            public string ModificationTarget
                => string.IsNullOrEmpty(this.modificationTarget)
                    ? null
                    : this.modificationTarget;

            /// <summary>
            /// Что и на что ссылалось.
            /// </summary>
            public string Description => this.description;

            /// <summary>
            /// Пакет локации.
            /// </summary>
            public string PackageName => this.packageName;

            /// <summary>
            /// Проверяет, описывают ли записи одну и ту же ссылку.
            /// </summary>
            /// <param name="other">Другая запись.</param>
            /// <returns><c>true</c> для одного файла, компонента и поля.</returns>
            public bool IsSame(Entry other)
                => string.Equals(
                       this.fileGuid,
                       other.fileGuid,
                       StringComparison.Ordinal)
                && this.documentId == other.documentId
                && string.Equals(
                       this.field,
                       other.field,
                       StringComparison.Ordinal)
                && string.Equals(
                       this.modificationTarget,
                       other.modificationTarget,
                       StringComparison.Ordinal);
        }

        [Serializable]
        private sealed class Store
        {
            [SerializeField]
            private List<Entry> entries = new List<Entry>();

            public List<Entry> Entries => this.entries;
        }
    }
}
