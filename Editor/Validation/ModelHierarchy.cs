using System.Collections.Generic;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Обход иерархии импортированной модели.
    /// </summary>
    internal static class ModelHierarchy
    {
        /// <summary>
        /// Строит путь от корня модели до объекта: Root/Group/Object.
        /// </summary>
        /// <param name="target">Объект модели.</param>
        /// <param name="root">Корень модели.</param>
        /// <returns>Путь через косую черту.</returns>
        public static string GetPath(
            Transform target,
            Transform root)
        {
            var names = new List<string>();
            var current = target;

            while (current != null
                && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Add(root.name);
            names.Reverse();

            return string.Join(
                "/",
                names);
        }

        /// <summary>
        /// Возвращает все объекты модели, включая корень и выключенные.
        /// </summary>
        /// <param name="root">Корень модели.</param>
        /// <returns>Трансформы всех объектов.</returns>
        public static Transform[] GetAll(GameObject root)
            => root.GetComponentsInChildren<Transform>(true);

        /// <summary>
        /// Возвращает все меши модели вместе с объектами, которые их рисуют.
        /// </summary>
        /// <param name="root">Корень модели.</param>
        /// <returns>Пары «объект — меш», без пустых ссылок.</returns>
        public static IEnumerable<(Transform Owner, Mesh Mesh)> GetMeshes(GameObject root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    yield return (filter.transform, filter.sharedMesh);
                }
            }

            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh != null)
                {
                    yield return (skinned.transform, skinned.sharedMesh);
                }
            }
        }
    }
}
