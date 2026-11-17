using System.Collections.Generic;

using PaperFly.RenderKit.Acceptance.Rescue;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Потерянные правки одного файла игры и решения разработчика по каждой из них.
    /// </summary>
    internal sealed class LostPlan
    {
        /// <summary>
        /// Создаёт пустой план.
        /// </summary>
        /// <param name="file">Сцена или префаб игры.</param>
        /// <param name="packageName">Пакет локации, объекты которой пропали.</param>
        public LostPlan(
            string file,
            string packageName)
        {
            this.File = file;
            this.PackageName = packageName;
        }

        /// <summary>
        /// Сцена или префаб игры.
        /// </summary>
        public string File { get; }

        /// <summary>
        /// Пакет локации, объекты которой пропали.
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// Пропавшие объекты, на которых игра что-то добавила: это можно сохранить в «_Потерянное …» или удалить.
        /// </summary>
        public List<HolderChoice> Holders { get; } = new List<HolderChoice>();

        /// <summary>
        /// Правки пересозданных объектов: перейдут на новые объекты сами.
        /// </summary>
        public List<string> Recreated { get; } = new List<string>();

        /// <summary>
        /// Изменённые поля компонентов пропавших объектов: перенести некуда, попадут в отчёт.
        /// </summary>
        public List<string> Lost { get; } = new List<string>();

        /// <summary>
        /// Ссылки на пропавшие объекты: станут пустыми.
        /// </summary>
        public List<ReferenceChoice> References { get; } = new List<ReferenceChoice>();

        /// <summary>
        /// Разбирать нечего.
        /// </summary>
        public bool IsEmpty
            => this.Holders.Count == 0
            && this.Recreated.Count == 0
            && this.Lost.Count == 0
            && this.References.Count == 0;

        /// <summary>
        /// Добавленное игрой на один пропавший объект.
        /// </summary>
        internal sealed class HolderChoice
        {
            /// <summary>
            /// Создаёт выбор; по умолчанию добавленное сохраняется.
            /// </summary>
            /// <param name="name">Имя объекта «_Потерянное …».</param>
            public HolderChoice(string name)
            {
                this.Name = name;
            }

            /// <summary>
            /// Имя объекта «_Потерянное …».
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Что именно добавлено.
            /// </summary>
            public List<string> Items { get; } = new List<string>();

            /// <summary>
            /// Сохранить в «_Потерянное …»; иначе удалить.
            /// </summary>
            public bool Keep { get; set; } = true;
        }

        /// <summary>
        /// Ссылка на пропавший объект.
        /// </summary>
        internal sealed class ReferenceChoice
        {
            /// <summary>
            /// Создаёт выбор; по умолчанию ссылка запоминается до исправления.
            /// </summary>
            /// <param name="reference">Найденная ссылка.</param>
            public ReferenceChoice(LostReferenceFinder.LostReference reference)
            {
                this.Reference = reference;
            }

            /// <summary>
            /// Найденная ссылка.
            /// </summary>
            public LostReferenceFinder.LostReference Reference { get; }

            /// <summary>
            /// Напоминать ошибкой, пока ссылку не назначат заново; иначе просто очистить.
            /// </summary>
            public bool Track { get; set; } = true;
        }
    }
}
