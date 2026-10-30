namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Коды находок ядра. Диапазоны: RCM001–RCM009 — извлечение и чтение снимка (Error); RCM010–RCM099 — ворота генератора против аннотаций
	/// (Generation, см. design); RCM100–RCM199 — сравнение двух снимков (RenderCoreLockDiff). Уровень у кода сравнения зависит от того,
	/// затронут ли предлагаемый вариант: изменения невостребованных вариантов всегда Info. Breaking здесь только то, что ворота генератора
	/// тоже не пропустят (указан код ворот): иначе Update Lock записал бы снимок, который Check и проверка сборки считают Breaking.
	/// </summary>
	public static class RenderCoreFindingCodes
	{
		/// <summary>Файл не найден, найден несколько раз или не читается.</summary>
		public const string Locate = "RCM001";

		/// <summary>Синтаксис вне поддержанного подмножества (YAML, enum, Properties, #if, метод).</summary>
		public const string Parse = "RCM002";

		/// <summary>Снимок испорчен: неизвестная запись, не та версия формата, отпечаток не сходится со строками.</summary>
		public const string LockFormat = "RCM003";

		/// <summary>Новый кандидат (вариант карт с данными в asset). Info.</summary>
		public const string VariantAdded = "RCM100";

		/// <summary>Кандидат пропал. Breaking для предлагаемого варианта.</summary>
		public const string VariantRemoved = "RCM101";

		/// <summary>Id варианта. Stale.</summary>
		public const string VariantId = "RCM102";

		/// <summary>Очередь (база или смещение). Stale.</summary>
		public const string VariantQueue = "RCM103";

		/// <summary>RenderType. Stale.</summary>
		public const string VariantRenderType = "RCM104";

		/// <summary>additionalKeywords. Stale.</summary>
		public const string VariantKeywords = "RCM105";

		/// <summary>Метка записи asset (name). Breaking, если перестала совпадать с именем enum; иначе Info.</summary>
		public const string VariantLabel = "RCM106";

		/// <summary>Число записей asset с этим id (дубли, первая выигрывает). Info.</summary>
		public const string VariantEntries = "RCM107";

		/// <summary>Члены enum RenderType (имя или число): тег RenderType материала пишется именем члена. Breaking (ворота RCM022).</summary>
		public const string RenderTypes = "RCM108";

		/// <summary>Свойство добавлено в вариант. Stale.</summary>
		public const string PropertyAdded = "RCM110";

		/// <summary>Свойство убрано из варианта. Stale.</summary>
		public const string PropertyRemoved = "RCM111";

		/// <summary>Тип свойства в asset. Stale: несовместимый со слотом тип не пропускают ворота (RCM032).</summary>
		public const string PropertyType = "RCM112";

		/// <summary>label, tooltip, vectorLabels. Info.</summary>
		public const string PropertyText = "RCM113";

		/// <summary>Порядок свойств. Info.</summary>
		public const string PropertyOrder = "RCM114";

		/// <summary>Тип свойства в Properties шейдера. Stale: несовместимый тип не пропускают ворота (RCM032).</summary>
		public const string ShaderPropertyType = "RCM120";

		/// <summary>[Gamma] свойства шейдера. Breaking для текстуры предлагаемого варианта (ворота RCM050), иначе Stale.</summary>
		public const string ShaderPropertyGamma = "RCM121";

		/// <summary>Значение по умолчанию в Properties шейдера. Info.</summary>
		public const string ShaderPropertyDefault = "RCM122";

		/// <summary>Файл Defs варианта. Breaking (ключ раскладки, RCM040).</summary>
		public const string Defs = "RCM130";

		/// <summary>MATERIALFEATURE_* / PACKED_MESH*. Breaking (ключ раскладки, RCM040).</summary>
		public const string Features = "RCM131";

		/// <summary>Прочие define'ы (SCREEN_SPACE_REFLECTIONS_ALLOWED и т.п.). Info.</summary>
		public const string OtherDefines = "RCM132";

		/// <summary>
		/// Вершинные потоки INPUT_NEED_*. Stale: requiredStreams перегенерируется; поток шире мешей модов или неизвестный не пропускают ворота
		/// (RCM060/RCM062).
		/// </summary>
		public const string Streams = "RCM133";

		/// <summary>Декодирование: отпечаток библиотек Definitions и активных строк Func — Breaking (RCM041); только имя файла Func — Info.</summary>
		public const string Decode = "RCM134";

		/// <summary>Include'ы библиотек в активных ветках Definitions. Breaking (входят в отпечаток декодирования, RCM041).</summary>
		public const string Libraries = "RCM135";

		/// <summary>
		/// Вид объявления keyword'а в проходе (multi_compile*, shader_feature*, absent). Stale: то, от чего зависит вариант (multi_compile* в
		/// Forward и ALPHATEST в проходах выреза, нет shader_feature*, своя/общая ветка выреза), проверяют ворота (RCM020, RCM040).
		/// </summary>
		public const string PassKind = "RCM140";

		/// <summary>Конфигурация прохода выреза альфа-теста (DepthOnly/ShadowCaster). Breaking для предлагаемого варианта (ворота RCM045).</summary>
		public const string PassConfig = "RCM141";

		/// <summary>Тело EditorUtils.SetupMaterialVariant. Breaking.</summary>
		public const string Setup = "RCM150";

		/// <summary>Пути входных файлов. Info.</summary>
		public const string Files = "RCM160";

		/// <summary>Члены enum без данных или вне кандидатов. Info.</summary>
		public const string EnumEntries = "RCM161";

		/// <summary>Область (исключённые префиксы и имена) снимка не та, с которой извлекали. Stale.</summary>
		public const string Scope = "RCM170";
	}
}
