# Changelog

Лог изменений модуля. Каждый PR сопровождается записью в этом файле, новая запись — первой в своём разделе.

## [Unreleased]

### Added
- Формат BinaryModData версии 2: длина у каждого поля, пропуск незнакомых полей, отдельный legacy-читатель версии 1, понятная ошибка на незнакомую версию (DR3C-4465)
- ModPathResolver — единое безопасное разрешение путей из данных мода внутри каталога мода (без "..", UNC и чужих абсолютных путей) (DR3C-4465)
- Тесты совместимости BinaryModData на замороженных документах v1/v2 и тесты ModPathResolver (DR3C-4465)
- ExportImporterSettings: временное изменение и восстановление настроек импорта текстур и моделей на время экспорта (DR3C-4465)

### Changed
- Оптимизация сцены при экспорте: один меш на ячейку с сабмешем на материал (вместо меша на материал×ячейку) и общие группы материалов на блок секторов; коллайдеры — свой сектор (colliderSectorSize 128 м, colliderMaxTriangles 32768) и общие файлы colliders_N вместо файла на коллайдер; куски меньше minSectorTriangles (128) переносятся в соседний сектор; уступка редактору раз в 50 мс вместо Task.Delay(1) на объект/группу; в лог — меши с LOD, сабмеши, группы и файлы коллайдеров. Формат и рантайм не менялись (DR3C-4465)
- BinaryModArchive читает ресурсы через один открытый дескриптор на время параллельных чтений (под блокировкой — только чтение блока, распаковка и CRC вне её), добавлен TryRead; CRC32 — slicing-by-8; DefaultFileProvider перепроверяет файл архива не чаще раза в секунду и с пула потоков читает синхронно (DR3C-4465)
- ModResourceFiles кэширует открытый BinaryModArchive по пути (сброс при смене файла и через ClearCache) (DR3C-4465)
- UnityGoObjExporter разнесён на отдельные классы без partial; RebuildAndSafeAll переименован в RebuildAndSaveAll, CancellationToken в RebuildAndSaveAsync — последний параметр (DR3C-4465)
- MeshExportUtility: стабильные строковые id (GUID+localFileId, для сгенерированных мешей — хэш содержимого) вместо GetHashCode; модель и .mtl перезаписываются, а не пропускаются/дописываются (DR3C-4465)
- Runtime/Publishing вынесен в Editor-only сборку CarX.Modding.Creator.Publishing; link.xml сохраняет только пространство имён формата (DR3C-4465)
- ModPublishingDefines больше не синхронизирует дефайны на загрузке домена — только из ModPublisherSession или меню ModSystem/Sync Publishing Defines (DR3C-4465)
- MetaProvider отклоняет мета-документы с незнакомой версией формата понятной ошибкой (DR3C-4465)
- Кодстайл модуля: s_/m_ для полей, табы, BinaryTexturePreparation/MtlBinaryMaterialReader/IAsyncModPacking в отдельных файлах (DR3C-4465)

### Fixed
- Экспорт прозрачности и Alpha Remapping HDRP Layered Lit: Surface Type Transparent пишется в .pbr.json полем surface (ModPbrSurfaceType.AlphaBlend; Blending Mode Additive/Premultiply — как Alpha с предупреждением), Alpha Remapping слоя (_AlphaRemapMin{i}/_AlphaRemapMax{i}) у прозрачного и альфа-тестового материала запекается по формуле HDRP lerp(min, max, a·_BaseColor{i}.a) в альфу копии diffuse (суффикс _a<min>_<max>[_<a>], color.a = 1), без карты — в color.a слоя; бинарный контейнер получает поле и текстуры тем же путём (DR3C-4465)
- Экспорт Double-Sided Normal Mode HDRP Lit/Layered Lit: ds 3 — Flip (раньше Flip и Mirror писались как ds 1 — Mirror), ds 1 — Mirror, ds 2 — None; BinaryMaterial.doubleSidedNormalMode (flipNormals остаётся для старых клиентов), VertexAnimationSurface.doubleSidedNormalMode для VAT; общий ModDoubleSidedNormalMode и MtlDoubleSidedCode (DR3C-4465)
- Экспорт прозрачных материалов HDRP Lit учитывает Alpha Remapping (_AlphaRemapMin/_AlphaRemapMax): ремап запекается в map_d (суффикс _a<min>_<max>[_<a>] в имени), d считается по формуле HDRP; бинарный контейнер запекает d в альфу packed map (DR3C-4465)
- Утечки Texture2D при экспорте (Blit/распаковка), материал и RT Blit освобождаются; кэш путей проверяется до Blit (DR3C-4465)
- Экспорт больше не оставляет изменёнными настройки импорта исходных ассетов и имена загруженных текстур (DR3C-4465)
