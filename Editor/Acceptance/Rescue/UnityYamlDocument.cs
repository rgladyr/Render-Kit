using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Один объект файла Unity YAML: заголовок «--- !u!класс &amp;идентификатор» и строки под ним.
    /// </summary>
    internal sealed class UnityYamlDocument
    {
        private const string EmptyList = " []";

        private readonly string header;

        /// <summary>
        /// Создаёт документ.
        /// </summary>
        /// <param name="classId">Номер класса Unity: 1 — GameObject, 4 — Transform, 1001 — PrefabInstance.</param>
        /// <param name="fileId">Идентификатор объекта в файле.</param>
        /// <param name="isStripped">Объект-заглушка экземпляра префаба: только ссылка на объект основы.</param>
        /// <param name="header">Строка заголовка.</param>
        /// <param name="lines">Строки под заголовком; первая — имя типа.</param>
        public UnityYamlDocument(
            int classId,
            long fileId,
            bool isStripped,
            string header,
            List<string> lines)
        {
            this.ClassId = classId;
            this.FileId = fileId;
            this.IsStripped = isStripped;
            this.header = header;
            this.Lines = lines;
        }

        /// <summary>
        /// Номер класса Unity.
        /// </summary>
        public int ClassId { get; }

        /// <summary>
        /// Идентификатор объекта в файле.
        /// </summary>
        public long FileId { get; }

        /// <summary>
        /// Объект-заглушка экземпляра префаба.
        /// </summary>
        public bool IsStripped { get; }

        /// <summary>
        /// Строки под заголовком.
        /// </summary>
        public List<string> Lines { get; }

        /// <summary>
        /// Имя типа: GameObject, Transform, MonoBehaviour и т. д.
        /// </summary>
        public string TypeName
            => this.Lines.Count == 0
                ? string.Empty
                : this.Lines[0].TrimEnd(':');

        private static string GetPrefix(
            string key,
            int indent)
            => new string(
                   ' ',
                   indent)
             + key
             + ":";

        private static bool IsEntryStart(
            string line,
            int indent)
            => line.Length > indent + 1
            && line.Take(indent).All(symbol => symbol == ' ')
            && line[indent] == '-'
            && line[indent + 1] == ' ';

        private static bool IsEntryContinuation(
            string line,
            int indent)
            => line.Trim().Length == 0
            || (line.Length > indent + 1
             && line.Take(indent + 1).All(symbol => symbol == ' '));

        private int FindKey(
            string key,
            int indent)
        {
            var prefix =
                GetPrefix(
                    key,
                    indent);

            return this.Lines.FindIndex(line => line.StartsWith(
                prefix,
                StringComparison.Ordinal));
        }

        /// <summary>
        /// Строки всего документа вместе с заголовком.
        /// </summary>
        /// <returns>Строки для записи в файл.</returns>
        public IEnumerable<string> GetAllLines()
            => new[] { this.header }.Concat(this.Lines);

        /// <summary>
        /// Значение поля верхнего уровня.
        /// </summary>
        /// <param name="key">Имя поля, например m_GameObject.</param>
        /// <returns>Текст после двоеточия без пробелов по краям; <c>null</c>, если поля нет.</returns>
        public string GetValue(string key)
        {
            var index =
                this.FindKey(
                    key,
                    2);

            if (index < 0)
            {
                return null;
            }

            var prefix =
                GetPrefix(
                    key,
                    2);

            return this.Lines[index]
                .Substring(prefix.Length)
                .Trim();
        }

        /// <summary>
        /// Меняет значение поля верхнего уровня.
        /// </summary>
        /// <param name="key">Имя поля.</param>
        /// <param name="value">Новое значение.</param>
        /// <returns><c>false</c>, если поля нет.</returns>
        public bool SetValue(
            string key,
            string value)
        {
            var index =
                this.FindKey(
                    key,
                    2);

            if (index < 0)
            {
                return false;
            }

            this.Lines[index] =
                GetPrefix(
                    key,
                    2)
              + " "
              + value;

            return true;
        }

        /// <summary>
        /// Элементы списка.
        /// </summary>
        /// <param name="key">Имя списка, например m_AddedComponents.</param>
        /// <param name="indent">Отступ имени списка: 2 у полей объекта, 4 внутри m_Modification.</param>
        /// <returns>Строки каждого элемента; пустой список, если элементов или самого списка нет.</returns>
        public List<List<string>> GetEntries(
            string key,
            int indent)
        {
            var entries = new List<List<string>>();

            var index =
                this.FindKey(
                    key,
                    indent);

            if (index < 0)
            {
                return entries;
            }

            for (var lineIndex = index + 1; lineIndex < this.Lines.Count; lineIndex++)
            {
                var line = this.Lines[lineIndex];

                if (IsEntryStart(
                        line,
                        indent))
                {
                    entries.Add(new List<string> { line });
                }
                else if (entries.Count > 0
                      && IsEntryContinuation(
                             line,
                             indent))
                {
                    entries[entries.Count - 1].Add(line);
                }
                else
                {
                    break;
                }
            }

            return entries;
        }

        /// <summary>
        /// Заменяет элементы списка.
        /// </summary>
        /// <param name="key">Имя списка.</param>
        /// <param name="indent">Отступ имени списка.</param>
        /// <param name="entries">Новые элементы; пустой набор записывается как «[]».</param>
        /// <returns><c>false</c>, если списка нет.</returns>
        public bool SetEntries(
            string key,
            int indent,
            IReadOnlyCollection<IReadOnlyCollection<string>> entries)
        {
            var index =
                this.FindKey(
                    key,
                    indent);

            if (index < 0)
            {
                return false;
            }

            var oldLineCount =
                this.GetEntries(
                        key,
                        indent)
                    .Sum(entry => entry.Count);

            this.Lines.RemoveRange(
                index + 1,
                oldLineCount);

            this.Lines.InsertRange(
                index + 1,
                entries.SelectMany(entry => entry));

            var prefix =
                GetPrefix(
                    key,
                    indent);

            this.Lines[index] =
                entries.Count == 0
                    ? prefix + EmptyList
                    : prefix;

            return true;
        }
    }
}
