namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Пути, пороги и имена, общие для валидации локаций.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Заголовок окна отчёта.
        /// </summary>
        internal const string WindowTitle = "Валидация локаций";

        /// <summary>
        /// Версия обработчика импорта. Увеличение переимпортирует все модели и текстуры проекта.
        /// </summary>
        internal const uint PostprocessorVersion = 1;

        /// <summary>
        /// Пути к пресетам и признак файлов локации.
        /// </summary>
        internal static class Paths
        {
            /// <summary>
            /// Пресет импорта FBX локации.
            /// </summary>
            internal const string ModelPreset = "Packages/com.paperfly.renderkit/Presets/LocationModel.preset";

            /// <summary>
            /// Пресет импорта текстур локации.
            /// </summary>
            internal const string TexturePreset = "Packages/com.paperfly.renderkit/Presets/LocationTexture.preset";

            /// <summary>
            /// Фрагмент пути, по которому файл считается частью локации.
            /// </summary>
            internal const string LocationMarker = "/Locations/";
        }

        /// <summary>
        /// Пункты меню.
        /// </summary>
        internal static class Menu
        {
            /// <summary>
            /// Открывает окно отчёта.
            /// </summary>
            internal const string Report = "PaperFly/Валидация/Отчёт";

            /// <summary>
            /// Проверяет все локации проекта заново.
            /// </summary>
            internal const string ValidateLocations = "PaperFly/Валидация/Проверить локации";

            /// <summary>
            /// Открывает окно привязки материалов моделей.
            /// </summary>
            internal const string Materials = "PaperFly/Валидация/Материалы моделей";
        }

        /// <summary>
        /// Пороги сравнения трансформов.
        /// </summary>
        internal static class Tolerances
        {
            /// <summary>
            /// Отклонение масштаба от 1, которое считается точной единицей.
            /// </summary>
            internal const float ExactScale = 1e-6f;

            /// <summary>
            /// Отклонение масштаба от 1, до которого оно считается шумом экспорта (0.9999 вместо 1).
            /// </summary>
            internal const float NoisyScale = 1e-3f;

            /// <summary>
            /// Относительная точность сравнения с масштабом артефакта экспорта.
            /// </summary>
            internal const float ExportArtifactScale = 1e-3f;
        }

        /// <summary>
        /// Шаблоны имён.
        /// </summary>
        internal static class Names
        {
            /// <summary>
            /// Имена объектов, которые Blender даёт по умолчанию.
            /// </summary>
            internal const string BlenderDefaultObjectPattern =
                @"^(Cube|Plane|Circle|Cylinder|Cone|Sphere|Icosphere|Torus|Grid|Suzanne|Monkey|Empty|Camera|Light|Point|Sun|Spot|Area|Text|Mesh|Object|BezierCurve|BezierCircle|NurbsPath|Armature)(\.\d{3})?$";

            /// <summary>
            /// Имена материалов, которые Blender даёт по умолчанию.
            /// </summary>
            internal const string BlenderDefaultMaterialPattern = @"^Material(\.\d{3})?$";

            /// <summary>
            /// Суффикс .001, который Blender добавляет к копии объекта.
            /// </summary>
            internal const string DuplicateSuffixPattern = @"\.\d{3}$";

            /// <summary>
            /// Уровень LOD в конце имени: Tree_LOD0, Tree_LOD1.
            /// </summary>
            internal const string LodPattern = @"^(?<base>.+)_LOD(?<level>\d+)$";

            /// <summary>
            /// Версия в имени файла: Childhood.v2, Wall_v1.
            /// </summary>
            internal const string VersionPattern = @"[._\- ]v\d+";

            /// <summary>
            /// Последний код символа ASCII.
            /// </summary>
            internal const int MaxAsciiCode = 127;
        }

        /// <summary>
        /// Масштабы, которые экспортёр Blender записывает вложенным мешам при Apply Transform.
        /// </summary>
        internal static class ExportArtifact
        {
            /// <summary>
            /// Масштаб при переводе сантиметров в метры.
            /// </summary>
            internal const float SmallScale = 0.01f;

            /// <summary>
            /// Обратный масштаб.
            /// </summary>
            internal const float LargeScale = 100f;
        }

        /// <summary>
        /// Пределы размеров моделей и текстур.
        /// </summary>
        internal static class Limits
        {
            /// <summary>
            /// Предел координат по каждой оси. Дальше точности float не хватает, и на лайтмапах появляются полосы.
            /// </summary>
            internal const float MaxCoordinate = 2048f;

            /// <summary>
            /// Сколько самых дальних объектов перечислять в отчёте поимённо.
            /// </summary>
            internal const int MaxReportedOutliers = 20;

            /// <summary>
            /// Материалов на одном меше, больше которых — предупреждение. Каждый материал — отдельный вызов отрисовки.
            /// </summary>
            internal const int MaxMaterialsPerRenderer = 4;

            /// <summary>
            /// Вершин в одном меше, больше которых нужен 32-битный индекс.
            /// </summary>
            internal const int MaxVerticesPerMesh = 65535;

            /// <summary>
            /// Платформы, чьи переопределения максимального размера текстуры учитываются.
            /// </summary>
            internal static readonly string[] TexturePlatforms = { "Android", "iPhone" };
        }
    }
}
