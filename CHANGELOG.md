# Changelog

Лог изменений модуля. Каждый PR сопровождается записью в этом файле, новая запись — первой в своём разделе.

## [Unreleased]

### Added
- Формат BinaryModData версии 2: длина у каждого поля, пропуск незнакомых полей, отдельный legacy-читатель версии 1, понятная ошибка на незнакомую версию (DR3C-4465)
- ModPathResolver — единое безопасное разрешение путей из данных мода внутри каталога мода (без "..", UNC и чужих абсолютных путей) (DR3C-4465)
- Тесты совместимости BinaryModData на замороженных документах v1/v2 и тесты ModPathResolver (DR3C-4465)
- ExportImporterSettings: временное изменение и восстановление настроек импорта текстур и моделей на время экспорта (DR3C-4465)

### Changed
- ModResourceFiles кэширует открытый BinaryModArchive по пути (сброс при смене файла и через ClearCache) (DR3C-4465)
- UnityGoObjExporter разнесён на отдельные классы без partial; RebuildAndSafeAll переименован в RebuildAndSaveAll, CancellationToken в RebuildAndSaveAsync — последний параметр (DR3C-4465)
- MeshExportUtility: стабильные строковые id (GUID+localFileId, для сгенерированных мешей — хэш содержимого) вместо GetHashCode; модель и .mtl перезаписываются, а не пропускаются/дописываются (DR3C-4465)
- Runtime/Publishing вынесен в Editor-only сборку CarX.Modding.Creator.Publishing; link.xml сохраняет только пространство имён формата (DR3C-4465)
- ModPublishingDefines больше не синхронизирует дефайны на загрузке домена — только из ModPublisherSession или меню ModSystem/Sync Publishing Defines (DR3C-4465)
- MetaProvider отклоняет мета-документы с незнакомой версией формата понятной ошибкой (DR3C-4465)
- Кодстайл модуля: s_/m_ для полей, табы, BinaryTexturePreparation/MtlBinaryMaterialReader/IAsyncModPacking в отдельных файлах (DR3C-4465)

### Fixed
- Утечки Texture2D при экспорте (Blit/распаковка), материал и RT Blit освобождаются; кэш путей проверяется до Blit (DR3C-4465)
- Экспорт больше не оставляет изменёнными настройки импорта исходных ассетов и имена загруженных текстур (DR3C-4465)
