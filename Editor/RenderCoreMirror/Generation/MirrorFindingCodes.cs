namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Коды ворот генератора (RCM010–RCM099): снимок против аннотаций. Находки — RenderCoreFinding. Любая Breaking или Error блокирует
	/// генерацию: таблица не пишется (fail closed). Info — только сообщение. Коды сравнения снимков — RenderCoreFindingCodes (RCM1xx).
	/// </summary>
	public static class MirrorFindingCodes
	{
		/// <summary>Предлагаемого варианта нет среди кандидатов снимка (переименован или удалён). Breaking.</summary>
		public const string OfferedMissing = "RCM010";

		/// <summary>Область аннотаций не совпадает с областью снимка: снимок извлекали для другой области. Stale.</summary>
		public const string Scope = "RCM011";

		/// <summary>Два аннотированных варианта (строки таблицы, доступные и недоступные) с одним id. Error.</summary>
		public const string DuplicateId = "RCM012";

		/// <summary>В asset несколько записей с id варианта, взята первая. Info.</summary>
		public const string DuplicateEntries = "RCM013";

		/// <summary>Метка записи asset не совпадает с именем enum (keyword всегда берётся из enum). Breaking.</summary>
		public const string LabelMismatch = "RCM014";

		/// <summary>Вариант аннотирован дважды. Breaking.</summary>
		public const string DuplicateAnnotation = "RCM015";

		/// <summary>Ревизия меньше 1. Breaking.</summary>
		public const string Revision = "RCM016";

		/// <summary>Keyword варианта или ALPHATEST объявлен не multi_compile* там, где нужен, или shader_feature* в каком-то проходе. Breaking.</summary>
		public const string PassKind = "RCM020";

		/// <summary>ALPHATEST в additionalKeywords и RenderType AlphaTest расходятся (или RenderType неизвестен). Breaking.</summary>
		public const string AlphaTest = "RCM021";

		/// <summary>
		/// Члены enum RenderType в rendercore (имена и числа) не равны MirrorRenderType/ModRenderCoreRenderType: rendercore пишет тег RenderType
		/// именем члена, клиент — именем своего enum. Breaking.
		/// </summary>
		public const string RenderTypeNames = "RCM022";

		/// <summary>Свойство, которое не покрывает ни правило слотов, ни переопределение. Breaking.</summary>
		public const string UnknownSlot = "RCM031";

		/// <summary>Тип свойства (asset или Properties шейдера) не тот, что ждёт слот, или свойства нет в Properties. Breaking.</summary>
		public const string SlotType = "RCM032";

		/// <summary>Переопределение слота без причины, для отсутствующего свойства или невыразимое в таблице. Breaking.</summary>
		public const string Override = "RCM033";

		/// <summary>Ключ раскладки (Defs, MATERIALFEATURE_*/PACKED_MESH*, проходы альфа-теста) не совпадает с аннотацией. Breaking.</summary>
		public const string LayoutKey = "RCM040";

		/// <summary>Отпечаток декодирования (библиотеки Definitions и активные строки Func) не совпадает с проверенным. Breaking.</summary>
		public const string DecodeHash = "RCM041";

		/// <summary>Неизвестный MATERIALFEATURE_* или файл Defs у предлагаемого варианта: правила не знают, что он делает. Breaking.</summary>
		public const string UnknownFeature = "RCM042";

		/// <summary>Каналы аннотации противоречат выведенным из define'ов. Breaking.</summary>
		public const string Channels = "RCM043";

		/// <summary>Каналы аннотированы без packed map или packed map без каналов (или не четыре канала). Breaking.</summary>
		public const string ChannelsShape = "RCM044";

		/// <summary>
		/// Конфигурация прохода выреза (DepthOnly/ShadowCaster) варианта альфа-теста не совпадает с проверенной (MirrorPassReview), её нет в
		/// снимке или нет проверки. Breaking. Неиспользуемая проверка — Info.
		/// </summary>
		public const string PassConfig = "RCM045";

		/// <summary>[Gamma] текстуры в шейдере расходится с замороженным правилом префикса u_sTexture. Breaking.</summary>
		public const string Gamma = "RCM050";

		/// <summary>Запекаемое содержимое изменилось при той же ревизии: поднять ревизию и дописать контракт. Breaking.</summary>
		public const string ContractChanged = "RCM052";

		/// <summary>Свойство, перечисленное в контракте текущей ревизии, пропало: нужна новая ревизия. Breaking.</summary>
		public const string ContractPropertyRemoved = "RCM053";

		/// <summary>Нет контракта для (вариант, ревизия) аннотации. Breaking.</summary>
		public const string ContractMissing = "RCM054";

		/// <summary>Два контракта для одной пары (вариант, ревизия). Breaking.</summary>
		public const string ContractDuplicate = "RCM055";

		/// <summary>Варианту нужны потоки, которых нет у мешей модов, а причины недоступности в аннотации нет. Breaking.</summary>
		public const string Streams = "RCM060";

		/// <summary>Использование цвета вершин, выведенное из define'ов, не совпадает с аннотацией. Breaking.</summary>
		public const string VertexColors = "RCM061";

		/// <summary>Неизвестный INPUT_NEED_*: неясно, какой поток он требует. Breaking.</summary>
		public const string UnknownStream = "RCM062";

		/// <summary>Тело EditorUtils.SetupMaterialVariant не совпадает с проверенным (сверить клиент и обновить отпечаток). Breaking.</summary>
		public const string Setup = "RCM070";

		/// <summary>Подсказка Export from material ссылается на неаннотированный или недоступный вариант. Breaking.</summary>
		public const string Suggestion = "RCM080";

		/// <summary>Кандидат снимка без аннотации: в таблицу не попадает. Info.</summary>
		public const string NotAnnotated = "RCM090";

		/// <summary>Закоммиченная таблица не равна generate(снимок, аннотации). Stale.</summary>
		public const string TableOutdated = "RCM091";

		/// <summary>Закоммиченный снимок не проходит ворота генератора: таблицу из него не построить. Breaking.</summary>
		public const string TableBlocked = "RCM092";
	}
}
