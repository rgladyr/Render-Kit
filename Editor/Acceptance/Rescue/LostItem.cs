using System.Collections.Generic;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Вид потерянной правки экземпляра префаба.
    /// </summary>
    internal enum LostItemKind
    {
        /// <summary>
        /// Изменённое свойство (m_Modifications).
        /// </summary>
        Property,

        /// <summary>
        /// Удалённый в игре компонент основы (m_RemovedComponents).
        /// </summary>
        RemovedComponent,

        /// <summary>
        /// Удалённый в игре объект основы (m_RemovedGameObjects).
        /// </summary>
        RemovedGameObject,

        /// <summary>
        /// Добавленный в игре компонент (m_AddedComponents).
        /// </summary>
        AddedComponent,

        /// <summary>
        /// Добавленный в игре дочерний объект (m_AddedGameObjects).
        /// </summary>
        AddedGameObject,
    }

    /// <summary>
    /// Правка экземпляра префаба, которая указывает на объект, которого больше нет в основе.
    /// </summary>
    internal sealed class LostItem
    {
        /// <summary>
        /// Создаёт описание потерянной правки.
        /// </summary>
        /// <param name="kind">Вид правки.</param>
        /// <param name="instance">Документ PrefabInstance, в котором записана правка.</param>
        /// <param name="entry">Строки элемента списка правок.</param>
        /// <param name="target">Объект основы, на который указывает правка.</param>
        public LostItem(
            LostItemKind kind,
            UnityYamlDocument instance,
            List<string> entry,
            YamlReference target)
        {
            this.Kind = kind;
            this.Instance = instance;
            this.Entry = entry;
            this.Target = target;
        }

        /// <summary>
        /// Вид правки.
        /// </summary>
        public LostItemKind Kind { get; }

        /// <summary>
        /// Документ PrefabInstance, в котором записана правка.
        /// </summary>
        public UnityYamlDocument Instance { get; }

        /// <summary>
        /// Строки элемента списка правок.
        /// </summary>
        public List<string> Entry { get; }

        /// <summary>
        /// Объект основы, на который указывает правка.
        /// </summary>
        public YamlReference Target { get; }

        /// <summary>
        /// Добавленный объект или компонент; <c>null</c> для остальных видов.
        /// </summary>
        public UnityYamlDocument Added { get; set; }

        /// <summary>
        /// Путь объекта, в который переименован потерянный; <c>null</c>, если объект удалён.
        /// </summary>
        public string RenamedPath { get; set; }

        /// <summary>
        /// Имя «кармана», куда переносится добавленное; <c>null</c>, если перенести нельзя или объект переименован.
        /// </summary>
        public string HolderName { get; set; }

        /// <summary>
        /// Строка отчёта.
        /// </summary>
        public string Description { get; set; }
    }
}
